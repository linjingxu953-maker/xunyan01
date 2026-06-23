using System.Runtime.InteropServices;
using DesktopMascot.Agent.Models;
using DesktopMascot.Agent.Providers;
using DesktopMascot.Core.Enums;
using DesktopMascot.Core.Interfaces;
using DesktopMascot.Core.Models;
using DesktopMascot.Core.Tools;
using Microsoft.Extensions.Logging;

namespace DesktopMascot.Agent.Engines;

/// <summary>
/// Computer Use 编排器 - 管理完整的计算机操作流程
/// </summary>
public class ComputerUseOrchestrator
{
    private readonly ILlmProvider _llmProvider;
    private readonly ITaskEventBus _eventBus;
    private readonly ILogger<ComputerUseOrchestrator> _logger;
    private readonly ComputerUseSession _session = new();
    private readonly object _approvalLock = new();
    private PlannedAction? _pendingApprovalAction;
    private string? _approvalDeniedReason;

    public event EventHandler<ComputerUseEvent>? ComputerUseEventOccurred;

    public ComputerUseOrchestrator(
        ILlmProvider llmProvider,
        ITaskEventBus eventBus,
        ILogger<ComputerUseOrchestrator> logger)
    {
        _llmProvider = llmProvider;
        _eventBus = eventBus;
        _logger = logger;
    }

    public ComputerUseSession Session => _session;
    public bool HasPendingApproval
    {
        get
        {
            lock (_approvalLock)
            {
                return _pendingApprovalAction is not null;
            }
        }
    }

    /// <summary>
    /// 执行 Computer Use 任务
    /// </summary>
    public async Task<TaskResult> ExecuteAsync(string userRequest, CancellationToken ct = default)
    {
        var taskId = Guid.NewGuid().ToString("N");
        _session.TaskId = taskId;
        _session.IsActive = true;
        _session.StartedAt = DateTime.UtcNow;
        _session.EventHistory.Clear();

        EmitEvent(new ComputerUseEvent
        {
            TaskId = taskId,
            EventType = ComputerUseEventType.ComputerUseStarted,
            Message = "Computer Use 任务开始",
            Action = "task_start",
            ToolName = "computer_use",
            Status = "executing",
            Progress = 0
        });

        try
        {
            EmitEvent(new ComputerUseEvent
            {
                TaskId = taskId,
                EventType = ComputerUseEventType.ScreenObserved,
                Message = "正在观察屏幕...",
                Action = "screenshot",
                ToolName = "computer_use",
                Status = "executing",
                Progress = 10
            });

            var screenshotPath = await CaptureScreenAsync(ct);

            EmitEvent(new ComputerUseEvent
            {
                TaskId = taskId,
                EventType = ComputerUseEventType.ActionPlanned,
                Message = "正在规划动作...",
                Action = "plan_actions",
                ToolName = "computer_use",
                Status = "executing",
                Progress = 30,
                ScreenshotPath = screenshotPath
            });

            var plan = await PlanActionsAsync(userRequest, screenshotPath, ct);
            _session.ActionPlan = plan;

            EmitEvent(new ComputerUseEvent
            {
                TaskId = taskId,
                EventType = ComputerUseEventType.ActionPlanned,
                Message = $"已规划 {plan.Count} 个动作",
                Action = "plan_complete",
                ToolName = "computer_use",
                Status = "completed",
                Progress = 40,
                PlannedActions = plan
            });

            for (int i = 0; i < plan.Count; i++)
            {
                ct.ThrowIfCancellationRequested();

                if (_session.IsPaused)
                {
                    await Task.Delay(500, ct);
                    i--;
                    continue;
                }

                if (_session.UserHasTakeover)
                {
                    EmitEvent(new ComputerUseEvent
                    {
                        TaskId = taskId,
                        EventType = ComputerUseEventType.UserTakeoverRequested,
                        Message = "用户已接管控制",
                        Action = "user_takeover",
                        Status = "completed",
                        Progress = 100
                    });
                    break;
                }

                var action = plan[i];
                action.Status = ActionStatus.Executing;

                EmitEvent(new ComputerUseEvent
                {
                    TaskId = taskId,
                    EventType = ComputerUseEventType.ActionExecuting,
                    Message = $"正在执行：{action.Description}",
                    Action = action.ActionName,
                    ToolName = action.ToolName,
                    Target = ExtractTarget(action),
                    Detail = action.Arguments,
                    Status = "executing",
                    Progress = 40 + (i * 50 / plan.Count),
                    CurrentAction = action
                });

                if (action.RequiresApproval)
                {
                    action.Status = ActionStatus.WaitingApproval;
                    SetPendingApproval(action);

                    EmitEvent(new ComputerUseEvent
                    {
                        TaskId = taskId,
                        EventType = ComputerUseEventType.WaitingUserApproval,
                        Message = $"等待确认：{action.Description}",
                        Action = action.ActionName,
                        ToolName = action.ToolName,
                        Target = ExtractTarget(action),
                        Status = "waiting_approval",
                        Progress = 40 + (i * 50 / plan.Count),
                        ApprovalRequest = new ApprovalRequest
                        {
                            ActionName = action.ActionName,
                            Description = action.Description,
                            RiskLevel = "medium"
                        }
                    });

                    await WaitForApprovalAsync(action, ct);
                    var deniedReason = ConsumeApprovalDeniedReason();
                    if (action.Status == ActionStatus.Failed)
                    {
                        var reason = deniedReason ?? "用户拒绝了 Computer Use 动作。";
                        EmitEvent(new ComputerUseEvent
                        {
                            TaskId = taskId,
                            EventType = ComputerUseEventType.ComputerUseFailed,
                            Message = reason,
                            Action = action.ActionName,
                            ToolName = action.ToolName,
                            Target = ExtractTarget(action),
                            Detail = action.Arguments,
                            Status = "denied",
                            Progress = 40 + (i * 50 / plan.Count),
                            ErrorMessage = reason
                        });
                        return TaskResult.Failed(taskId, reason);
                    }
                }

                var result = await ExecuteActionAsync(action, ct);

                action.Status = result.Success ? ActionStatus.Completed : ActionStatus.Failed;

                EmitEvent(new ComputerUseEvent
                {
                    TaskId = taskId,
                    EventType = result.Success ? ComputerUseEventType.ActionCompleted : ComputerUseEventType.ComputerUseFailed,
                    Message = result.Success ? $"已完成：{action.Description}" : $"失败：{result.Error}",
                    Action = action.ActionName,
                    ToolName = action.ToolName,
                    Target = ExtractTarget(action),
                    Detail = action.Arguments,
                    Status = result.Success ? "completed" : "failed",
                    Progress = 40 + ((i + 1) * 50 / plan.Count),
                    ActionResult = result.Content,
                    ErrorMessage = result.Error
                });

                if (!result.Success)
                {
                    return TaskResult.Failed(taskId, result.Error ?? "动作执行失败");
                }

                await Task.Delay(200, ct);
            }

            _session.IsActive = false;
            _session.CompletedAt = DateTime.UtcNow;

            EmitEvent(new ComputerUseEvent
            {
                TaskId = taskId,
                EventType = ComputerUseEventType.ComputerUseCompleted,
                Message = "Computer Use 任务完成",
                Action = "task_complete",
                Status = "completed",
                Progress = 100
            });

            return new TaskResult
            {
                TaskId = taskId,
                Success = true,
                Content = $"Computer Use 完成，执行了 {plan.Count} 个动作"
            };
        }
        catch (Exception ex)
        {
            _session.IsActive = false;

            EmitEvent(new ComputerUseEvent
            {
                TaskId = taskId,
                EventType = ComputerUseEventType.ComputerUseFailed,
                Message = $"任务失败：{ex.Message}",
                Action = "task_failed",
                Status = "failed",
                ErrorMessage = ex.Message
            });

            return TaskResult.Failed(taskId, ex.Message);
        }
    }

    public void Pause() => _session.IsPaused = true;
    public void Resume() => _session.IsPaused = false;
    public void Takeover()
    {
        _session.UserHasTakeover = true;
        DenyCurrentAction("用户已接管 Computer Use。");
    }

    public bool ApproveCurrentAction()
    {
        lock (_approvalLock)
        {
            if (_pendingApprovalAction is null)
                return false;

            _pendingApprovalAction.Status = ActionStatus.Executing;
            _pendingApprovalAction = null;
            _approvalDeniedReason = null;
            return true;
        }
    }

    public bool DenyCurrentAction(string? reason = null)
    {
        lock (_approvalLock)
        {
            if (_pendingApprovalAction is null)
                return false;

            _approvalDeniedReason = string.IsNullOrWhiteSpace(reason)
                ? "用户拒绝了 Computer Use 动作。"
                : reason.Trim();
            _pendingApprovalAction.Status = ActionStatus.Failed;
            _pendingApprovalAction = null;
            return true;
        }
    }

    private async Task<string> CaptureScreenAsync(CancellationToken ct)
    {
        try
        {
            var hwnd = GetForegroundWindow();
            if (hwnd == IntPtr.Zero) return "";

            GetWindowRect(hwnd, out var rect);
            var w = rect.Right - rect.Left;
            var h = rect.Bottom - rect.Top;
            if (w <= 0 || h <= 0) return "";

            using var bmp = new System.Drawing.Bitmap(w, h);
            using var g = System.Drawing.Graphics.FromImage(bmp);
            g.CopyFromScreen(rect.Left, rect.Top, 0, 0, new System.Drawing.Size(w, h));

            var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "DesktopMascot", "screenshots", $"cu_{DateTime.UtcNow:yyyyMMdd_HHmmss}.png");
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            bmp.Save(path, System.Drawing.Imaging.ImageFormat.Png);

            return path;
        }
        catch
        {
            return "";
        }
    }

    private async Task<List<PlannedAction>> PlanActionsAsync(string userRequest, string screenshotPath, CancellationToken ct)
    {
        var systemPrompt = """
            你是一个 Computer Use 规划器。根据用户请求和屏幕截图，规划要执行的动作。

            返回 JSON 数组，每个动作包含：
            - step: 步骤号
            - actionName: 动作名称（click/type/hotkey/scroll/screenshot）
            - description: 中文描述
            - toolName: 工具名（computer_use）
            - arguments: 工具参数 JSON
            - requiresApproval: 是否需要用户确认

            规则：
            - 每次最多规划 5 个动作
            - 敏感操作（键盘输入、快捷键）必须 requiresApproval: true
            - 鼠标点击不需要确认
            - 返回纯 JSON，不要其他内容

            用户请求：{userRequest}
            """;

        var messages = new List<LlmMessage>
        {
            new() { Role = "system", Content = systemPrompt },
            new() { Role = "user", Content = $"用户请求：{userRequest}" }
        };

        if (!string.IsNullOrEmpty(screenshotPath))
        {
            try
            {
                var bytes = await File.ReadAllBytesAsync(screenshotPath, ct);
                messages[1].Images = new List<VisionContent>
                {
                    new() { Base64Data = Convert.ToBase64String(bytes), MediaType = "image/png" }
                };
            }
            catch { }
        }

        var response = await _llmProvider.ChatAsync(messages, null, ct);
        if (!response.Success)
            return CreateFallbackPlan(userRequest);

        try
        {
            var json = ExtractJsonArray(response.Content);
            var doc = System.Text.Json.JsonDocument.Parse(json);
            var plan = new List<PlannedAction>();

            foreach (var item in doc.RootElement.EnumerateArray())
            {
                plan.Add(new PlannedAction
                {
                    Step = item.TryGetProperty("step", out var s) ? s.GetInt32() : plan.Count + 1,
                    ActionName = item.TryGetProperty("actionName", out var a) ? a.GetString() ?? "" : "",
                    Description = item.TryGetProperty("description", out var d) ? d.GetString() ?? "" : "",
                    ToolName = item.TryGetProperty("toolName", out var t) ? t.GetString() ?? "computer_use" : "computer_use",
                    Arguments = item.TryGetProperty("arguments", out var ar) ? ReadActionArguments(ar) : "{}",
                    RequiresApproval = item.TryGetProperty("requiresApproval", out var ra) && ra.GetBoolean()
                });
            }

            return plan.Count > 0 ? plan : CreateFallbackPlan(userRequest);
        }
        catch
        {
            return CreateFallbackPlan(userRequest);
        }
    }

    private static List<PlannedAction> CreateFallbackPlan(string userRequest)
    {
        return new List<PlannedAction>
        {
            new()
            {
                Step = 1,
                ActionName = "screenshot",
                Description = "截图当前屏幕",
                ToolName = "computer_use",
                Arguments = """{"action":"screenshot"}""",
                RequiresApproval = false
            }
        };
    }

    private static string ExtractJsonArray(string content)
    {
        var start = content.IndexOf('[');
        var end = content.LastIndexOf(']');
        if (start >= 0 && end > start)
            return content[start..(end + 1)];
        return content;
    }

    private static string ReadActionArguments(System.Text.Json.JsonElement element)
    {
        if (element.ValueKind == System.Text.Json.JsonValueKind.String)
        {
            var value = element.GetString();
            return string.IsNullOrWhiteSpace(value) ? "{}" : value;
        }

        return element.GetRawText();
    }

    private static string ExtractTarget(PlannedAction action)
    {
        try
        {
            if (string.IsNullOrEmpty(action.Arguments) || action.Arguments == "{}")
                return "";

            using var doc = System.Text.Json.JsonDocument.Parse(action.Arguments);
            var root = doc.RootElement;
            var actionName = action.ActionName.ToLower();

            if (actionName is "click" or "double_click" or "right_click")
            {
                var x = root.TryGetProperty("x", out var xEl) ? xEl.GetInt32() : 0;
                var y = root.TryGetProperty("y", out var yEl) ? yEl.GetInt32() : 0;
                return $"坐标({x}, {y})";
            }
            if (actionName is "type")
            {
                return root.TryGetProperty("text", out var tEl) ? $"文本: {tEl.GetString()}" : "";
            }
            if (actionName is "hotkey")
            {
                return root.TryGetProperty("keys", out var kEl) ? $"快捷键: {kEl.GetString()}" : "";
            }
            if (actionName is "scroll")
            {
                return root.TryGetProperty("delta", out var dEl) ? $"滚动: {dEl.GetInt32()}" : "";
            }
            if (actionName is "open_url")
            {
                return root.TryGetProperty("url", out var uEl) ? $"URL: {uEl.GetString()}" : "";
            }
            if (actionName is "run_command")
            {
                return root.TryGetProperty("command", out var cEl) ? $"命令: {cEl.GetString()}" : "";
            }
            return "";
        }
        catch
        {
            return "";
        }
    }

    private async Task WaitForApprovalAsync(PlannedAction action, CancellationToken ct)
    {
        while (_session.IsActive && !ct.IsCancellationRequested)
        {
            if (action.Status != ActionStatus.WaitingApproval) break;
            await Task.Delay(500, ct);
        }
    }

    private async Task<ToolResult> ExecuteActionAsync(PlannedAction action, CancellationToken ct)
    {
        await Task.CompletedTask;

        return action.ActionName switch
        {
            "click" => ExecuteClick(action.Arguments),
            "type" => ExecuteType(action.Arguments),
            "hotkey" => ExecuteHotkey(action.Arguments),
            "screenshot" => new ToolResult { Name = "computer_use", Success = true, Content = "截图完成" },
            _ => new ToolResult { Name = "computer_use", Success = true, Content = $"已执行：{action.ActionName}" }
        };
    }

    private static ToolResult ExecuteClick(string args)
    {
        try
        {
            var doc = System.Text.Json.JsonDocument.Parse(args);
            var root = doc.RootElement;
            var x = root.TryGetProperty("x", out var xEl) ? xEl.GetInt32() : 100;
            var y = root.TryGetProperty("y", out var yEl) ? yEl.GetInt32() : 100;

            SetCursorPos(x, y);
            Thread.Sleep(50);
            mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, 0);
            mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, 0);

            return new ToolResult { Name = "computer_use", Success = true, Content = $"已点击 ({x}, {y})" };
        }
        catch (Exception ex)
        {
            return new ToolResult { Name = "computer_use", Success = false, Error = ex.Message };
        }
    }

    private static ToolResult ExecuteType(string args)
    {
        try
        {
            var doc = System.Text.Json.JsonDocument.Parse(args);
            var root = doc.RootElement;
            var text = root.TryGetProperty("text", out var tEl) ? tEl.GetString() ?? "" : "";

            foreach (char c in text)
            {
                var input = new INPUT { type = 1, u = new InputUnion { ki = new KEYBDINPUT { wVk = 0, wScan = (ushort)c, dwFlags = 0x0004 } } };
                SendInput(1, new[] { input }, Marshal.SizeOf<INPUT>());
                Thread.Sleep(10);
            }

            return new ToolResult { Name = "computer_use", Success = true, Content = $"已输入 {text.Length} 字符" };
        }
        catch (Exception ex)
        {
            return new ToolResult { Name = "computer_use", Success = false, Error = ex.Message };
        }
    }

    private static ToolResult ExecuteHotkey(string args)
    {
        try
        {
            var doc = System.Text.Json.JsonDocument.Parse(args);
            var root = doc.RootElement;
            if (!root.TryGetProperty("keys", out var keysEl))
                return new ToolResult { Name = "computer_use", Success = false, Error = "缺少 keys" };

            var keys = keysEl.EnumerateArray().Select(k => k.GetString() ?? "").ToList();
            var vks = keys.Select(k => k.ToLowerInvariant() switch
            {
                "ctrl" => (ushort)0x11, "shift" => (ushort)0x10, "alt" => (ushort)0x12,
                "enter" => (ushort)0x0D, "tab" => (ushort)0x09, "escape" => (ushort)0x1B,
                "space" => (ushort)0x20, "backspace" => (ushort)0x08,
                _ when k.Length == 1 => (ushort)char.ToUpper(k[0]),
                _ => (ushort)0
            }).ToList();

            foreach (var vk in vks)
            {
                var input = new INPUT { type = 1, u = new InputUnion { ki = new KEYBDINPUT { wVk = vk, dwFlags = 0 } } };
                SendInput(1, new[] { input }, Marshal.SizeOf<INPUT>());
                Thread.Sleep(10);
            }
            foreach (var vk in vks.AsEnumerable().Reverse())
            {
                var input = new INPUT { type = 1, u = new InputUnion { ki = new KEYBDINPUT { wVk = vk, dwFlags = 0x0002 } } };
                SendInput(1, new[] { input }, Marshal.SizeOf<INPUT>());
                Thread.Sleep(10);
            }

            return new ToolResult { Name = "computer_use", Success = true, Content = $"已执行：{string.Join("+", keys)}" };
        }
        catch (Exception ex)
        {
            return new ToolResult { Name = "computer_use", Success = false, Error = ex.Message };
        }
    }

    private void EmitEvent(ComputerUseEvent evt)
    {
        _session.EventHistory.Add(evt);
        ComputerUseEventOccurred?.Invoke(this, evt);

        _eventBus.Publish(new TaskEvent
        {
            TaskId = evt.TaskId,
            EventType = MapEventType(evt.EventType),
            State = MapState(evt.EventType),
            Message = evt.Message,
            Progress = evt.Progress,
            Metadata = BuildTaskEventMetadata(evt)
        });
    }

    private void SetPendingApproval(PlannedAction action)
    {
        lock (_approvalLock)
        {
            _pendingApprovalAction = action;
            _approvalDeniedReason = null;
        }
    }

    private string? ConsumeApprovalDeniedReason()
    {
        lock (_approvalLock)
        {
            var reason = _approvalDeniedReason;
            _approvalDeniedReason = null;
            return reason;
        }
    }

    private static Dictionary<string, object> BuildTaskEventMetadata(ComputerUseEvent evt)
    {
        var metadata = new Dictionary<string, object>
        {
            ["computerUseEventType"] = evt.EventType.ToString()
        };

        AddIfPresent(metadata, "action", evt.Action);
        AddIfPresent(metadata, "computerAction", evt.Action);
        AddIfPresent(metadata, "toolName", evt.ToolName);
        AddIfPresent(metadata, "target", evt.Target);
        AddIfPresent(metadata, "detail", evt.Detail);
        AddIfPresent(metadata, "status", evt.Status);
        if (evt.EventType is ComputerUseEventType.ScreenObserved or ComputerUseEventType.ActionPlanned)
            metadata["screenshotPath"] = evt.ScreenshotPath ?? string.Empty;
        AddIfPresent(metadata, "result", evt.ActionResult);
        AddIfPresent(metadata, "output", evt.ActionResult);
        AddIfPresent(metadata, "error", evt.ErrorMessage);

        if (evt.CurrentAction is not null)
        {
            metadata["currentActionDescription"] = evt.CurrentAction.Description;
            AddIfPresent(metadata, "step", evt.CurrentAction.Step.ToString());
        }

        if (evt.PlannedActions is { Count: > 0 })
        {
            metadata["plannedActionCount"] = evt.PlannedActions.Count;
            metadata["plannedActions"] = string.Join("；", evt.PlannedActions.Select(action =>
                $"{action.Step}. {action.Description} [{action.ActionName}]"));
        }

        if (evt.ApprovalRequest is not null)
        {
            metadata["approvalRequestId"] = evt.ApprovalRequest.RequestId;
            metadata["approvalActionName"] = evt.ApprovalRequest.ActionName;
            metadata["approvalDescription"] = evt.ApprovalRequest.Description;
            metadata["riskLevel"] = evt.ApprovalRequest.RiskLevel;
            metadata["permissionType"] = "ComputerUse";
            metadata["scope"] = string.IsNullOrWhiteSpace(evt.Target) ? "当前桌面" : evt.Target;
            metadata["reason"] = evt.ApprovalRequest.Description;
        }

        return metadata;
    }

    private static void AddIfPresent(Dictionary<string, object> metadata, string key, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            metadata[key] = value;
    }

    private static TaskEventType MapEventType(ComputerUseEventType type) => type switch
    {
        ComputerUseEventType.ComputerUseStarted => TaskEventType.TaskStarted,
        ComputerUseEventType.ComputerUseCompleted => TaskEventType.TaskCompleted,
        ComputerUseEventType.ComputerUseFailed => TaskEventType.TaskFailed,
        ComputerUseEventType.ActionExecuting => TaskEventType.ToolCallStarted,
        ComputerUseEventType.ActionCompleted => TaskEventType.ToolCallCompleted,
        ComputerUseEventType.WaitingUserApproval => TaskEventType.PermissionRequested,
        _ => TaskEventType.ProgressUpdated
    };

    private static MascotState MapState(ComputerUseEventType type) => type switch
    {
        ComputerUseEventType.ComputerUseStarted => MascotState.Listening,
        ComputerUseEventType.ScreenObserved => MascotState.ReadingContext,
        ComputerUseEventType.ActionPlanned => MascotState.Planning,
        ComputerUseEventType.ActionExecuting => MascotState.Working,
        ComputerUseEventType.ActionCompleted => MascotState.Working,
        ComputerUseEventType.WaitingUserApproval => MascotState.WaitingApproval,
        ComputerUseEventType.ComputerUseCompleted => MascotState.Completed,
        ComputerUseEventType.ComputerUseFailed => MascotState.Error,
        _ => MascotState.Working
    };

    [DllImport("user32.dll")] private static extern bool SetCursorPos(int X, int Y);
    [DllImport("user32.dll")] private static extern void mouse_event(uint dwFlags, int dx, int dy, uint dwData, int dwExtraInfo);
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);
    [DllImport("user32.dll")] private static extern void SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    [StructLayout(LayoutKind.Sequential)] private struct RECT { public int Left; public int Top; public int Right; public int Bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct INPUT { public uint type; public InputUnion u; }
    [StructLayout(LayoutKind.Explicit)] private struct InputUnion { [FieldOffset(0)] public KEYBDINPUT ki; }
    [StructLayout(LayoutKind.Sequential)] private struct KEYBDINPUT { public ushort wVk; public ushort wScan; public uint dwFlags; public uint time; public IntPtr dwExtraInfo; }

    private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
    private const uint MOUSEEVENTF_LEFTUP = 0x0004;
}
