using System.Text.Json;
using DesktopMascot.Agent.Context;
using DesktopMascot.Agent.Models;
using DesktopMascot.Agent.Providers;
using DesktopMascot.Agent.Tools;
using DesktopMascot.Core.Enums;
using DesktopMascot.Core.Interfaces;
using DesktopMascot.Core.Models;
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
    private readonly int _maxIterations;

    public AgentOrchestrator(
        ILlmProvider llmProvider,
        ToolRegistry toolRegistry,
        ITaskEventBus eventBus,
        ILogger<AgentOrchestrator> logger,
        int maxIterations = 10)
    {
        _llmProvider = llmProvider;
        _toolRegistry = toolRegistry;
        _eventBus = eventBus;
        _logger = logger;
        _maxIterations = maxIterations;
    }

    public async Task<TaskResult> ExecuteAsync(AgentTask task, CancellationToken ct = default)
    {
        _logger.LogInformation("Agent 开始执行任务: {Title}", task.Title);

        var taskType = ResolveTaskType(task);
        var contextProvider = ResolveContextProvider();

        if (taskType == TaskType.SummarizePage && contextProvider != null)
        {
            return await ExecuteSummarizePageAsync(task, contextProvider, ct);
        }

        if (taskType == TaskType.ScreenUnderstand)
        {
            return await ExecuteScreenUnderstandAsync(task, ct);
        }

        if (taskType == TaskType.AnalyzeError)
        {
            return await ExecuteWithSpecializedPromptAsync(task, GetAnalyzeErrorPrompt(), "报错分析", ct);
        }

        if (taskType == TaskType.InspectProject)
        {
            return await ExecuteWithSpecializedPromptAsync(task, GetInspectProjectPrompt(), "项目诊断", ct);
        }

        if (taskType == TaskType.SolveProblem)
        {
            return await ExecuteWithSpecializedPromptAsync(task, GetSolveProblemPrompt(), "题目解答", ct);
        }

        return await ExecuteGenericTaskAsync(task, ct);
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

        var arguments = task.Parameters.TryGetValue("Region", out var regionObj)
            ? JsonSerializer.Serialize(regionObj)
            : "{}";

        if (task.Parameters.TryGetValue("UserHint", out var hintObj) && hintObj is string hint)
        {
            var doc = JsonDocument.Parse(arguments);
            var dict = doc.RootElement.EnumerateObject().ToDictionary(p => p.Name, p => p.Value.GetRawText());
            dict["user_hint"] = $"\"{hint}\"";
            arguments = JsonSerializer.Serialize(dict);
        }

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

    private string GetSystemPrompt()
    {
        var toolNames = string.Join(", ", _toolRegistry.GetToolDefinitions().Select(t => t.Name));
        return $"""
            你是一个智能助手，可以帮助用户完成各种任务。

            可用工具: {toolNames}

            当你需要使用工具时，请在响应中包含工具调用。
            当任务完成时，直接返回结果给用户。

            请用中文回复。
            """;
    }

    private List<ToolCall> ParseToolCalls(string content)
    {
        var toolCalls = new List<ToolCall>();

        try
        {
            if (content.Contains("tool_call"))
            {
                var start = content.IndexOf("tool_call");
                var jsonStart = content.IndexOf('{', start);
                var jsonEnd = content.LastIndexOf('}');

                if (jsonStart >= 0 && jsonEnd > jsonStart)
                {
                    var json = content.Substring(jsonStart, jsonEnd - jsonStart + 1);
                    var doc = JsonDocument.Parse(json);
                    var root = doc.RootElement;

                    if (root.TryGetProperty("tool_call", out var toolCallWrapper))
                    {
                        root = toolCallWrapper;
                    }

                    if (root.TryGetProperty("name", out var name))
                    {
                        toolCalls.Add(new ToolCall
                        {
                            Name = name.GetString() ?? "",
                            Arguments = root.TryGetProperty("arguments", out var args)
                                ? args.GetRawText()
                                : "{}"
                        });
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "解析工具调用失败");
        }

        return toolCalls;
    }

    private async Task<TaskResult> ExecuteWithSpecializedPromptAsync(
        AgentTask task, string systemPrompt, string taskLabel, CancellationToken ct)
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

    private static string GetAnalyzeErrorPrompt()
    {
        return """
            你是一个专业的错误分析助手。你的任务是帮助用户理解和解决软件报错。

            分析步骤：
            1. **错误识别**：提取错误类型、错误代码、错误消息
            2. **原因分析**：分析可能的根本原因（从最可能到最不可能）
            3. **解决方案**：给出具体的修复步骤
            4. **预防建议**：如何避免类似错误再次发生

            回答格式：
            **错误类型**: [类型]
            **错误信息**: [关键信息]
            **可能原因**:
            1. [原因1]
            2. [原因2]
            **解决方案**:
            1. [步骤1]
            2. [步骤2]
            **预防建议**: [建议]

            注意：
            - 如果有截图，结合截图中的代码/报错信息分析
            - 给出可执行的具体命令或代码片段
            - 如果需要更多上下文，请明确指出需要什么信息
            """;
    }

    private static string GetInspectProjectPrompt()
    {
        return """
            你是一个专业的项目结构分析助手。你的任务是分析项目目录结构，给出诊断和改进建议。

            分析维度：
            1. **项目概览**：技术栈、项目类型、规模
            2. **目录结构**：是否符合最佳实践
            3. **依赖管理**：依赖是否合理、是否有冗余
            4. **代码质量**：文件组织、命名规范
            5. **潜在问题**：缺失文件、配置问题、安全隐患
            6. **改进建议**：具体可执行的优化步骤

            回答格式：
            **项目类型**: [类型]
            **技术栈**: [技术栈]
            **项目规模**: [规模评估]
            **目录结构评价**: [评价]
            **发现的问题**:
            - [问题1]
            - [问题2]
            **改进建议**:
            1. [建议1]
            2. [建议2]

            注意：
            - 如果有截图，结合截图中的目录结构分析
            - 建议要具体可执行
            - 按优先级排序建议
            """;
    }

    private static string GetSolveProblemPrompt()
    {
        return """
            你是一个专业的解题助手。你的任务是帮助用户理解和解答各种题目。

            解题步骤：
            1. **理解题意**：准确理解题目要求
            2. **分析思路**：确定解题方向和方法
            3. **详细解答**：给出完整的解题过程
            4. **验证答案**：检查答案是否正确
            5. **扩展思考**：相关知识点和类似题型

            回答格式：
            **题目理解**: [用自己的话复述题目]
            **解题思路**: [用什么方法解决]
            **详细解答**:
            [完整的解题步骤，包括公式、代码、推理过程]
            **答案**: [最终答案]
            **知识点**: [涉及的核心概念]
            **类似题型**: [举一反三]

            注意：
            - 如果有截图，仔细阅读题目内容
            - 解题过程要详细，让用户能理解每一步
            - 如果有多种解法，都列出来
            - 代码题要给出可运行的代码
            """;
    }
}
