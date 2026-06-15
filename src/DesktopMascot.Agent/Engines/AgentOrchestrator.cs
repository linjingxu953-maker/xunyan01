using System.Text.Json;
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

    private readonly AgentPersonality _personality;

    public AgentOrchestrator(
        ILlmProvider llmProvider,
        ToolRegistry toolRegistry,
        ITaskEventBus eventBus,
        ILogger<AgentOrchestrator> logger,
        int maxIterations = 10,
        MemoryIntegrationService? memoryService = null,
        ComputerUseOrchestrator? computerUseOrchestrator = null,
        ITaskHistoryStore? historyStore = null,
        ConversationManager? conversationManager = null,
        LearningEngine? learningEngine = null,
        IAuditLogStore? auditLogStore = null,
        ErrorHandler? errorHandler = null,
        AgentPersonality? personality = null)
    {
        _llmProvider = llmProvider;
        _toolRegistry = toolRegistry;
        _eventBus = eventBus;
        _logger = logger;
        _maxIterations = maxIterations;
        _memoryService = memoryService;
        _computerUseOrchestrator = computerUseOrchestrator;
        _historyStore = historyStore;
        _conversationManager = conversationManager ?? new ConversationManager();
        _learningEngine = learningEngine ?? new LearningEngine();
        _auditLogStore = auditLogStore;
        _errorHandler = errorHandler;
        _personality = personality ?? new AgentPersonality();
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

    private static string GetChatPrompt()
    {
        return """
            你是一个友善、专业的 AI 助手。你的任务是帮助用户解决各种问题。

            回答原则：
            1. **准确**：确保信息准确，不确定时如实说明
            2. **简洁**：回答要简洁明了，避免冗余
            3. **有用**：给出具体可执行的建议
            4. **友善**：保持友善耐心的态度

            回答格式：
            - 直接回答用户问题，不需要固定格式
            - 如果问题需要步骤，用有序列表
            - 如果是代码问题，给出代码示例
            - 如果是概念解释，用通俗易懂的语言

            注意：
            - 如果有截图，结合截图内容回答
            - 不确定的信息要标注
            - 复杂问题可以分步骤回答
            """;
    }

    private static string GetWriteFilePrompt()
    {
        return """
            你是一个专业的文件生成助手。你的任务是帮助用户生成各种文件内容。

            生成原则：
            1. **完整**：生成完整的文件内容，不要省略
            2. **规范**：遵循语言/格式的最佳实践
            3. **可运行**：代码要能直接运行
            4. **有注释**：关键代码添加注释

            回答格式：
            **文件类型**: [类型]
            **文件名**: [建议文件名]
            **内容**:
            ```
            [完整的文件内容]
            ```

            注意：
            - 如果有截图，结合截图中的需求生成
            - 代码要完整，不要用 ... 省略
            - 如果需要多个文件，分别列出
            - 说明文件的用途和使用方法
            """;
    }

    private static string GetRunCommandPrompt()
    {
        return """
            你是一个专业的命令执行助手。你的任务是帮助用户生成和解释各种命令。

            回答原则：
            1. **安全**：提醒用户注意命令风险
            2. **准确**：确保命令语法正确
            3. **解释**：说明命令的作用和参数含义
            4. **替代**：提供更安全的替代方案

            回答格式：
            **命令**: [完整命令]
            **作用**: [命令做什么]
            **参数说明**:
            - [参数1]: [含义]
            - [参数2]: [含义]
            **风险提示**: [如果有风险]
            **注意事项**: [使用前需要知道的]

            注意：
            - 危险命令（rm -rf, DROP TABLE 等）要特别提醒
            - 给出命令的可复制版本
            - 如果有截图，结合截图中的上下文
            """;
    }

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

/// <summary>
/// IAsyncEnumerable 的 EnumeratorCancellation 特性
/// </summary>
[AttributeUsage(AttributeTargets.Parameter)]
internal sealed class EnumeratorCancellationAttribute : Attribute { }
