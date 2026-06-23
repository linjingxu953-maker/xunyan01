using System.Text.Json;
using System.Text.Json.Nodes;
using System.Runtime.CompilerServices;
using DesktopMascot.Agent.Context;
using DesktopMascot.Agent.Memory;
using DesktopMascot.Agent.Models;
using DesktopMascot.Agent.Providers;
using DesktopMascot.Agent.Tools;
using DesktopMascot.Core.Conversation;
using DesktopMascot.Core.Enums;
using DesktopMascot.Core.ErrorHandling;
using DesktopMascot.Core.Interfaces;
using DesktopMascot.Core.Learning;
using DesktopMascot.Core.Models;
using DesktopMascot.Core.Security;
using DesktopMascot.Core.Storage;
using DesktopMascot.Core.Summary;
using Microsoft.Extensions.Logging;

namespace DesktopMascot.Agent.Engines;

/// <summary>
/// Agent 编排器 - 实现 ReAct 循环
/// </summary>
public class AgentOrchestrator : IAgentEngine
{
    private readonly ILlmProvider _llmProvider;
    private readonly ToolRegistry _toolRegistry;
    private readonly ITaskEventBus _eventBus;
    private readonly ILogger<AgentOrchestrator> _logger;
    private readonly MemoryIntegrationService? _memoryService;
    private readonly ComputerUseOrchestrator? _computerUseOrchestrator;
    private readonly ITaskHistoryStore? _historyStore;
    private readonly IAuditLogStore? _auditLogStore;
    private readonly ErrorHandler? _errorHandler;
    private readonly ConversationManager _conversationManager;
    private readonly LearningEngine _learningEngine;
    private readonly int _maxIterations;

    private AgentPersonality _personality;

    /// <summary>主构造函数 — 通过 Options 对象注入所有依赖</summary>
    public AgentOrchestrator(AgentOrchestratorOptions options)
    {
        _llmProvider = options.LlmProvider;
        _toolRegistry = options.ToolRegistry;
        _eventBus = options.EventBus;
        _logger = options.Logger;
        _maxIterations = options.MaxIterations;
        _memoryService = options.MemoryService;
        _computerUseOrchestrator = options.ComputerUseOrchestrator;
        _historyStore = options.HistoryStore;
        _conversationManager = options.ConversationManager ?? new ConversationManager();
        _learningEngine = options.LearningEngine ?? new LearningEngine();
        _auditLogStore = options.AuditLogStore;
        _errorHandler = options.ErrorHandler;
        _personality = options.Personality ?? new AgentPersonality();
    }

    /// <summary>运行时更新角色人格（角色切换时调用）</summary>
    public void UpdatePersonality(AgentPersonality newPersonality)
    {
        _personality = newPersonality ?? throw new ArgumentNullException(nameof(newPersonality));
    }

    /// <summary>便捷构造函数 — 仅必选依赖，供测试和简单场景使用</summary>
    public AgentOrchestrator(
        ILlmProvider llmProvider,
        ToolRegistry toolRegistry,
        ITaskEventBus eventBus,
        ILogger<AgentOrchestrator> logger,
        int maxIterations = 10)
        : this(new AgentOrchestratorOptions
        {
            LlmProvider = llmProvider,
            ToolRegistry = toolRegistry,
            EventBus = eventBus,
            Logger = logger,
            MaxIterations = maxIterations
        })
    {
    }

    public async Task<TaskResult> ExecuteAsync(AgentTask task, CancellationToken ct = default)
    {
        _logger.LogInformation("Agent 开始执行任务: {Title}", task.Title);

        // 记录审计日志
        await LogAuditAsync(task.Id, "任务开始", $"用户请求: {task.Input}");

        // 添加用户消息到对话上下文
        _conversationManager.AddUserMessage(task.Input, new Dictionary<string, string>
        {
            ["taskType"] = task.Type.ToString(),
            ["taskId"] = task.Id
        });

        var historyRecord = await RecordTaskStartAsync(task, ct);

        try
        {
            MemoryContext? memoryContext = null;
            if (_memoryService != null)
            {
                memoryContext = await _memoryService.SearchRelevantMemoriesAsync(task, ct);
            }

            var taskType = ResolveTaskType(task);
            var contextProvider = ResolveContextProvider();

            TaskResult result;

            // 使用 SafeExecutor 包装执行，支持重试和错误恢复
            var retryPolicy = taskType == TaskType.Chat ? RetryPolicy.NoRetry : RetryPolicy.Default;

            var operationResult = await SafeExecutor.ExecuteAsync<TaskResult>(async (innerCt) =>
            {
                if (taskType == TaskType.SummarizePage && contextProvider != null)
                {
                    return await ExecuteSummarizePageAsync(task, contextProvider, innerCt);
                }
                else if (taskType == TaskType.ScreenUnderstand)
                {
                    return await ExecuteScreenUnderstandAsync(task, innerCt);
                }
                else if (taskType == TaskType.AnalyzeError)
                {
                    return await ExecuteWithSpecializedPromptAsync(task, GetAnalyzeErrorPrompt(), "报错分析", innerCt, memoryContext);
                }
                else if (taskType == TaskType.InspectProject)
                {
                    return await ExecuteWithSpecializedPromptAsync(task, GetInspectProjectPrompt(), "项目诊断", innerCt, memoryContext);
                }
                else if (taskType == TaskType.SolveProblem)
                {
                    return await ExecuteWithSpecializedPromptAsync(task, GetSolveProblemPrompt(), "题目解答", innerCt, memoryContext);
                }
                else if (taskType == TaskType.WriteFile)
                {
                    return await ExecuteWithSpecializedPromptAsync(task, GetWriteFilePrompt(), "文件生成", innerCt, memoryContext);
                }
                else if (taskType == TaskType.RunCommand)
                {
                    return await ExecuteWithSpecializedPromptAsync(task, GetRunCommandPrompt(), "命令执行", innerCt, memoryContext);
                }
                else if (taskType == TaskType.ComputerUse)
                {
                    return await ExecuteComputerUseAsync(task, innerCt);
                }
                else
                {
                    return await ExecuteWithSpecializedPromptAsync(task, GetChatPrompt(), "对话", innerCt, memoryContext);
                }
            }, retryPolicy, _errorHandler, ct);

            result = operationResult.Success
                ? operationResult.Value!
                : TaskResult.Failed(task.Id, operationResult.ErrorMessage ?? "执行失败");

            // 添加助手响应到对话上下文
            _conversationManager.AddAssistantMessage(result.Content);

            // 分析任务模式（自我进化）
            _learningEngine.AnalyzeTaskPattern(taskType.ToString(), result.Success);

            // 记录审计日志
            await LogAuditAsync(task.Id, "任务完成", result.Success ? "成功" : $"失败: {result.Error}");

            await RecordTaskEndAsync(historyRecord, result, ct);

            if (_memoryService != null && result.Success)
            {
                var proposals = await _memoryService.ProposeMemoriesAsync(task, result, ct);
                if (proposals.Count > 0)
                {
                    _eventBus.Publish(new TaskEvent
                    {
                        TaskId = task.Id,
                        State = MascotState.MemoryConfirm,
                        Message = $"检测到 {proposals.Count} 条可保存的记忆",
                        Progress = 95
                    });

                    await _memoryService.SaveProposedMemoriesAsync(proposals, ct);
                }
            }

            return result;
        }
        catch (OperationCanceledException)
        {
            var cancelledResult = TaskResult.Failed(task.Id, "任务已取消");
            await RecordTaskEndAsync(historyRecord, cancelledResult, ct);
            return cancelledResult;
        }
    }

    private async Task<TaskResult> ExecuteSummarizePageAsync(
        AgentTask task, IContextProvider contextProvider, CancellationToken ct)
    {
        _eventBus.Publish(new TaskEvent
        {
            TaskId = task.Id,
            State = MascotState.ReadingContext,
            Message = "正在读取浏览器页面...",
            Progress = 10
        });

        var snapshot = await contextProvider.GetActiveWindowContextAsync(ct);
        var screenshotPath = await contextProvider.CaptureScreenshotAsync(ct: ct);

        _eventBus.Publish(new TaskEvent
        {
            TaskId = task.Id,
            State = MascotState.Working,
            Message = "正在分析页面内容...",
            Progress = 40
        });

        var systemPrompt = """
            你是一个专业的网页内容分析助手。你的任务是根据提供的浏览器截图和窗口信息，为用户总结当前网页的核心内容。

            要求：
            1. 提取页面的主要标题和主题
            2. 列出 3-5 个核心要点
            3. 如果是文章，给出简要摘要（2-3 句话）
            4. 如果是工具/产品页面，说明其主要功能
            5. 用简洁清晰的中文回答
            6. 如果截图不清晰，请如实说明

            回答格式：
            **页面标题**: [标题]
            **核心内容**: [摘要]
            **关键要点**:
            - 要点1
            - 要点2
            - 要点3
            """;

        var userContent = $"窗口标题: {snapshot.ActiveWindowTitle}\n应用: {snapshot.ActiveApplication}";

        if (!string.IsNullOrEmpty(snapshot.BrowserUrl))
            userContent += $"\nURL: {snapshot.BrowserUrl}";

        if (!string.IsNullOrEmpty(snapshot.SelectedText))
            userContent += $"\n选中文本: {snapshot.SelectedText}";

        userContent += "\n\n请根据截图分析并总结这个页面的内容。";

        var messages = new List<LlmMessage>
        {
            new() { Role = "system", Content = systemPrompt },
            new() { Role = "user", Content = userContent }
        };

        if (!string.IsNullOrEmpty(screenshotPath) && !screenshotPath.StartsWith("["))
        {
            try
            {
                var imageBytes = await File.ReadAllBytesAsync(screenshotPath, ct);
                var base64 = Convert.ToBase64String(imageBytes);
                messages[1].Images = new List<VisionContent>
                {
                    new() { Base64Data = base64, MediaType = "image/png" }
                };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "读取截图失败，将不带截图进行摘要");
            }
        }

        _eventBus.Publish(new TaskEvent
        {
            TaskId = task.Id,
            State = MascotState.Working,
            Message = "AI 正在分析页面...",
            Progress = 60
        });

        var response = await _llmProvider.ChatAsync(messages, null, ct);

        if (!response.Success)
        {
            return TaskResult.Failed(task.Id, response.Error ?? "LLM 调用失败");
        }

        _eventBus.Publish(new TaskEvent
        {
            TaskId = task.Id,
            State = MascotState.Reporting,
            Message = "正在整理结果...",
            Progress = 90
        });

        return new TaskResult
        {
            TaskId = task.Id,
            Success = true,
            Content = response.Content
        };
    }

    private async Task<TaskResult> ExecuteScreenUnderstandAsync(AgentTask task, CancellationToken ct)
    {
        _eventBus.Publish(new TaskEvent
        {
            TaskId = task.Id,
            State = MascotState.ReadingContext,
            Message = "正在截取屏幕区域...",
            Progress = 10
        });

        var screenTool = _toolRegistry.GetTool("screen_understand") as ScreenUnderstandTool;
        if (screenTool == null)
        {
            return TaskResult.Failed(task.Id, "屏幕理解工具未注册");
        }

        var arguments = BuildScreenUnderstandArguments(task);

        _eventBus.Publish(new TaskEvent
        {
            TaskId = task.Id,
            State = MascotState.Working,
            Message = "AI 正在理解屏幕内容...",
            Progress = 40
        });

        var toolResult = await screenTool.ExecuteAsync(arguments, ct);

        if (!toolResult.Success)
        {
            return TaskResult.Failed(task.Id, toolResult.Error ?? "屏幕理解失败");
        }

        _eventBus.Publish(new TaskEvent
        {
            TaskId = task.Id,
            State = MascotState.Reporting,
            Message = "正在整理结果...",
            Progress = 90
        });

        return new TaskResult
        {
            TaskId = task.Id,
            Success = true,
            Content = toolResult.Content
        };
    }

    private static string BuildScreenUnderstandArguments(AgentTask task)
    {
        var payload = new JsonObject();

        if (task.Parameters.TryGetValue("Region", out var regionObj))
        {
            using var regionDoc = JsonDocument.Parse(JsonSerializer.Serialize(regionObj));
            var root = regionDoc.RootElement;

            if (root.ValueKind == JsonValueKind.Object)
            {
                if (LooksLikeScreenRegion(root))
                {
                    payload["region"] = JsonNode.Parse(root.GetRawText());
                }
                else
                {
                    foreach (var property in root.EnumerateObject())
                    {
                        payload[property.Name] = JsonNode.Parse(property.Value.GetRawText());
                    }
                }
            }
        }

        if (task.Parameters.TryGetValue("UserHint", out var hintObj)
            && hintObj is string hint
            && !string.IsNullOrWhiteSpace(hint))
        {
            payload["user_hint"] = hint;
        }

        return payload.Count == 0 ? "{}" : payload.ToJsonString();
    }

    private static bool LooksLikeScreenRegion(JsonElement element)
    {
        return element.TryGetProperty("x", out _)
            && element.TryGetProperty("y", out _)
            && element.TryGetProperty("width", out _)
            && element.TryGetProperty("height", out _);
    }

    private async Task<TaskResult> ExecuteGenericTaskAsync(AgentTask task, CancellationToken ct)
    {
        var messages = new List<LlmMessage>
        {
            new() { Role = "system", Content = GetSystemPrompt() },
            new() { Role = "user", Content = task.Input }
        };

        var tools = _toolRegistry.GetToolDefinitions().ToList();
        var iteration = 0;

        while (iteration < _maxIterations)
        {
            ct.ThrowIfCancellationRequested();
            iteration++;
            _logger.LogDebug("Agent 迭代 {Iteration}", iteration);

            _eventBus.Publish(new TaskEvent
            {
                TaskId = task.Id,
                State = MascotState.Working,
                Message = $"思考中... (第{iteration}轮)",
                Progress = Math.Min(90, iteration * 10)
            });

            var response = await _llmProvider.ChatAsync(messages, tools, ct);

            if (!response.Success)
            {
                return TaskResult.Failed(task.Id, response.Error ?? "LLM 调用失败");
            }

            if (string.IsNullOrEmpty(response.Content))
            {
                return new TaskResult
                {
                    TaskId = task.Id,
                    Success = true,
                    Content = "Agent 未返回有效响应"
                };
            }

            var toolCalls = response.ToolCalls ?? ParseToolCalls(response.Content);

            if (toolCalls.Count == 0)
            {
                return new TaskResult
                {
                    TaskId = task.Id,
                    Success = true,
                    Content = response.Content
                };
            }

            messages.Add(new LlmMessage { Role = "assistant", Content = response.Content });

            foreach (var toolCall in toolCalls)
            {
                _eventBus.Publish(new TaskEvent
                {
                    TaskId = task.Id,
                    State = MascotState.Working,
                    Message = $"执行工具: {toolCall.Name}",
                    Progress = Math.Min(90, iteration * 10 + 5)
                });

                if (_toolRegistry.RequiresConfirmation(toolCall.Name))
                {
                    _eventBus.Publish(TaskEvent.PermissionRequested(
                        task.Id,
                        toolCall.Name,
                        "tool_execution",
                        _toolRegistry.GetConfirmationMessage(toolCall.Name)));
                }

                var toolResult = await _toolRegistry.ExecuteToolAsync(toolCall, ct);
                messages.Add(new LlmMessage
                {
                    Role = "tool",
                    Content = JsonSerializer.Serialize(toolResult)
                });
            }
        }

        return TaskResult.Failed(task.Id, $"达到最大迭代次数 ({_maxIterations})");
    }

    private static TaskType ResolveTaskType(AgentTask task)
    {
        if (task.Parameters.TryGetValue("TaskType", out var typeObj) && typeObj is TaskType tt)
            return tt;
        return task.Type;
    }

    private IContextProvider? ResolveContextProvider()
    {
        if (_toolRegistry is Agent.Tools.ToolRegistry agentRegistry)
        {
            return agentRegistry.GetContextProvider();
        }
        return null;
    }

    private async Task<TaskHistoryRecord?> RecordTaskStartAsync(AgentTask task, CancellationToken ct)
    {
        if (_historyStore == null) return null;

        try
        {
            var record = new TaskHistoryRecord
            {
                Id = task.Id,
                Title = task.Title,
                Input = task.Input,
                Type = ResolveTaskType(task),
                Status = AppTaskStatus.Running,
                CreatedAt = DateTime.UtcNow
            };
            return await _historyStore.SaveTaskAsync(record, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "记录任务开始失败");
            return null;
        }
    }

    private async Task RecordTaskEndAsync(TaskHistoryRecord? record, TaskResult result, CancellationToken ct)
    {
        if (_historyStore == null || record == null) return;

        try
        {
            record.Status = result.Success ? AppTaskStatus.Completed : AppTaskStatus.Failed;
            record.Result = result.Content;
            record.Error = result.Error;
            record.CompletedAt = DateTime.UtcNow;
            await _historyStore.UpdateTaskAsync(record, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "记录任务结束失败");
        }
    }

    private async Task LogAuditAsync(string taskId, string operation, string details)
    {
        if (_auditLogStore == null) return;

        try
        {
            await _auditLogStore.SaveAsync(new AuditLogEntry
            {
                TaskId = taskId,
                Operation = operation,
                Details = details,
                Timestamp = DateTime.UtcNow
            }, CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "记录审计日志失败");
        }
    }

    private string GetSystemPrompt()
    {
        var toolNames = string.Join(", ", _toolRegistry.GetToolDefinitions().Select(t => t.Name));
        return _personality.BuildSystemPrompt(toolNames, hasContext: true);
    }

    private List<ToolCall> ParseToolCalls(string content)
    {
        var toolCalls = new List<ToolCall>();

        try
        {
            // 策略1：整个内容是 JSON 数组 [{name, arguments}, ...]
            var trimmed = content.Trim();
            if (trimmed.StartsWith('[') && trimmed.EndsWith(']'))
            {
                using var doc = JsonDocument.Parse(trimmed);
                foreach (var element in doc.RootElement.EnumerateArray())
                {
                    if (!element.TryGetProperty("name", out var nameEl)) continue;
                    toolCalls.Add(new ToolCall
                    {
                        Name = nameEl.GetString() ?? "",
                        Arguments = element.TryGetProperty("arguments", out var argsEl)
                            ? argsEl.ToString() : "{}"
                    });
                }
                if (toolCalls.Count > 0) return toolCalls;
            }

            // 策略2：整个内容是单个 JSON 对象含 name/arguments
            if (trimmed.StartsWith('{') && trimmed.EndsWith('}'))
            {
                using var doc = JsonDocument.Parse(trimmed);
                var root = doc.RootElement;
                if (root.TryGetProperty("name", out var nameEl))
                {
                    toolCalls.Add(new ToolCall
                    {
                        Name = nameEl.GetString() ?? "",
                        Arguments = root.TryGetProperty("arguments", out var argsEl)
                            ? argsEl.ToString() : "{}"
                    });
                    return toolCalls;
                }
            }

            // 策略3：从 content 中提取所有 JSON 对象（兼容 tool_call 包裹格式）
            var braceDepth = 0;
            var jsonStart = -1;
            for (int i = 0; i < content.Length; i++)
            {
                if (content[i] == '{') { if (braceDepth == 0) jsonStart = i; braceDepth++; }
                else if (content[i] == '}') { braceDepth--; if (braceDepth == 0 && jsonStart >= 0) { ParseJsonBlock(content.Substring(jsonStart, i - jsonStart + 1), toolCalls); jsonStart = -1; } }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "解析工具调用 JSON 失败");
        }

        return toolCalls;
    }

    /// <summary>解析单个 JSON 块，可能是 {name, arguments} 或含 tool_calls 包装</summary>
    private static void ParseJsonBlock(string json, List<ToolCall> toolCalls)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // 解包 tool_call / tool_calls 外层
            if (root.TryGetProperty("tool_call", out var wrapper)) root = wrapper;
            if (root.TryGetProperty("tool_calls", out var array))
            {
                foreach (var e in array.EnumerateArray())
                    ParseSingleToolCall(e, toolCalls);
                return;
            }

            ParseSingleToolCall(root, toolCalls);
        }
        catch { /* 忽略无法解析的块 */ }
    }

    private static void ParseSingleToolCall(JsonElement element, List<ToolCall> toolCalls)
    {
        if (!element.TryGetProperty("name", out var nameEl)) return;
        toolCalls.Add(new ToolCall
        {
            Name = nameEl.GetString() ?? "",
            Arguments = element.TryGetProperty("arguments", out var argsEl) ? argsEl.ToString() : "{}"
        });
    }

    private async Task<TaskResult> ExecuteWithSpecializedPromptAsync(
        AgentTask task, string systemPrompt, string taskLabel, CancellationToken ct, MemoryContext? memoryContext = null)
    {
        var contextProvider = ResolveContextProvider();
        string? screenshotPath = null;
        string? windowTitle = null;

        if (contextProvider != null)
        {
            _eventBus.Publish(new TaskEvent
            {
                TaskId = task.Id,
                State = MascotState.ReadingContext,
                Message = $"正在读取当前上下文...",
                Progress = 10
            });

            var snapshot = await contextProvider.GetActiveWindowContextAsync(ct);
            windowTitle = snapshot.ActiveWindowTitle;
            screenshotPath = await contextProvider.CaptureScreenshotAsync(ct: ct);
        }

        _eventBus.Publish(new TaskEvent
        {
            TaskId = task.Id,
            State = MascotState.Working,
            Message = $"AI 正在{taskLabel}...",
            Progress = 40
        });

        var userContent = task.Input;
        if (!string.IsNullOrEmpty(windowTitle))
        {
            userContent = $"当前窗口: {windowTitle}\n\n用户请求: {task.Input}";
        }

        if (memoryContext != null && memoryContext.HasRelevantMemories)
        {
            userContent += memoryContext.ToPromptContext();
        }

        var messages = new List<LlmMessage>
        {
            new() { Role = "system", Content = systemPrompt },
            new() { Role = "user", Content = userContent }
        };

        if (!string.IsNullOrEmpty(screenshotPath) && !screenshotPath.StartsWith("["))
        {
            try
            {
                var imageBytes = await File.ReadAllBytesAsync(screenshotPath, ct);
                var base64 = Convert.ToBase64String(imageBytes);
                messages[1].Images = new List<VisionContent>
                {
                    new() { Base64Data = base64, MediaType = "image/png" }
                };
            }
            catch { }
        }

        var response = await _llmProvider.ChatAsync(messages, null, ct);

        if (!response.Success)
        {
            return TaskResult.Failed(task.Id, response.Error ?? "LLM 调用失败");
        }

        _eventBus.Publish(new TaskEvent
        {
            TaskId = task.Id,
            State = MascotState.Reporting,
            Message = "正在整理结果...",
            Progress = 90
        });

        return new TaskResult
        {
            TaskId = task.Id,
            Success = true,
            Content = response.Content
        };
    }

    private static string GetAnalyzeErrorPrompt() => Prompts.AnalyzeError;
    private static string GetInspectProjectPrompt() => Prompts.InspectProject;
    private static string GetSolveProblemPrompt() => Prompts.SolveProblem;
    private static string GetChatPrompt() => Prompts.Chat;
    private static string GetWriteFilePrompt() => Prompts.WriteFile;
    private static string GetRunCommandPrompt() => Prompts.RunCommand;

    /// <summary>
    /// 流式执行任务 - 实时推送 LLM 输出
    /// </summary>
    public async IAsyncEnumerable<string> ExecuteStreamingAsync(
        AgentTask task, [EnumeratorCancellation] CancellationToken ct = default)
    {
        _logger.LogInformation("Agent 流式执行任务: {Title}", task.Title);

        MemoryContext? memoryContext = null;
        if (_memoryService != null)
        {
            memoryContext = await _memoryService.SearchRelevantMemoriesAsync(task, ct);
        }

        var contextProvider = ResolveContextProvider();
        string? screenshotPath = null;
        string? windowTitle = null;

        if (contextProvider != null)
        {
            _eventBus.Publish(new TaskEvent
            {
                TaskId = task.Id,
                State = MascotState.ReadingContext,
                Message = "正在读取上下文...",
                Progress = 10
            });

            var snapshot = await contextProvider.GetActiveWindowContextAsync(ct);
            windowTitle = snapshot.ActiveWindowTitle;
            screenshotPath = await contextProvider.CaptureScreenshotAsync(ct: ct);
        }

        _eventBus.Publish(new TaskEvent
        {
            TaskId = task.Id,
            State = MascotState.Working,
            Message = "AI 正在思考...",
            Progress = 40
        });

        var userContent = task.Input;
        if (!string.IsNullOrEmpty(windowTitle))
        {
            userContent = $"当前窗口: {windowTitle}\n\n用户请求: {task.Input}";
        }
        if (memoryContext != null && memoryContext.HasRelevantMemories)
        {
            userContent += memoryContext.ToPromptContext();
        }

        var messages = new List<LlmMessage>
        {
            new() { Role = "system", Content = GetChatPrompt() },
            new() { Role = "user", Content = userContent }
        };

        if (!string.IsNullOrEmpty(screenshotPath) && !screenshotPath.StartsWith("["))
        {
            try
            {
                var imageBytes = await File.ReadAllBytesAsync(screenshotPath, ct);
                var base64 = Convert.ToBase64String(imageBytes);
                messages[1].Images = new List<VisionContent>
                {
                    new() { Base64Data = base64, MediaType = "image/png" }
                };
            }
            catch { }
        }

        var fullResponse = new System.Text.StringBuilder();
        var iteration = 0;

        await foreach (var chunk in _llmProvider.ChatStreamAsync(messages, ct))
        {
            ct.ThrowIfCancellationRequested();
            fullResponse.Append(chunk);
            iteration++;

            if (iteration % 10 == 0)
            {
                _eventBus.Publish(TaskEvent.LlmStreamChunk(task.Id, chunk));
            }

            yield return chunk;
        }

        _eventBus.Publish(new TaskEvent
        {
            TaskId = task.Id,
            State = MascotState.Completed,
            Message = "任务完成",
            Progress = 100
        });

        if (_memoryService != null)
        {
            var result = new TaskResult
            {
                TaskId = task.Id,
                Success = true,
                Content = fullResponse.ToString()
            };
            var proposals = await _memoryService.ProposeMemoriesAsync(task, result, ct);
            if (proposals.Count > 0)
            {
                await _memoryService.SaveProposedMemoriesAsync(proposals, ct);
            }
        }
    }

    /// <summary>
    /// 执行 Computer Use 任务
    /// </summary>
    private async Task<TaskResult> ExecuteComputerUseAsync(AgentTask task, CancellationToken ct)
    {
        if (_computerUseOrchestrator == null)
        {
            return TaskResult.Failed(task.Id, "Computer Use 编排器未初始化");
        }

        _eventBus.Publish(new TaskEvent
        {
            TaskId = task.Id,
            State = MascotState.Listening,
            Message = "Computer Use 任务开始",
            Progress = 0
        });

        var result = await _computerUseOrchestrator.ExecuteAsync(task.Input, ct);

        if (result.Success)
        {
            _eventBus.Publish(new TaskEvent
            {
                TaskId = task.Id,
                State = MascotState.Completed,
                Message = "Computer Use 任务完成",
                Progress = 100
            });
        }
        else
        {
            _eventBus.Publish(new TaskEvent
            {
                TaskId = task.Id,
                State = MascotState.Error,
                Message = result.Error ?? "Computer Use 任务失败",
                Progress = 0
            });
        }

        return result;
    }

    /// <summary>获取对话管理器</summary>
    public ConversationManager GetConversationManager() => _conversationManager;

    /// <summary>获取学习引擎</summary>
    public LearningEngine GetLearningEngine() => _learningEngine;

    /// <summary>记录用户反馈</summary>
    public void RecordFeedback(string taskId, FeedbackType type, string content)
    {
        _learningEngine.RecordFeedback(taskId, type, content);
    }

    /// <summary>获取进化报告</summary>
    public EvolutionReport GetEvolutionReport()
    {
        return _learningEngine.GenerateReport();
    }
}
