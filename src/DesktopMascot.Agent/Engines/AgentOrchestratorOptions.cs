using DesktopMascot.Agent.Memory;
using DesktopMascot.Agent.Models;
using DesktopMascot.Agent.Providers;
using DesktopMascot.Agent.Tools;
using DesktopMascot.Core.Conversation;
using DesktopMascot.Core.ErrorHandling;
using DesktopMascot.Core.Interfaces;
using DesktopMascot.Core.Learning;
using DesktopMascot.Core.Security;
using DesktopMascot.Core.Storage;
using DesktopMascot.Core.Tools;
using Microsoft.Extensions.Logging;

namespace DesktopMascot.Agent.Engines;

/// <summary>
/// AgentOrchestrator 配置选项 — 封装所有可选依赖，避免构造函数参数膨胀
/// </summary>
public class AgentOrchestratorOptions
{
    /// <summary>LLM 提供器（必选）</summary>
    public required ILlmProvider LlmProvider { get; init; }

    /// <summary>工具注册表（必选）</summary>
    public required DesktopMascot.Agent.Tools.ToolRegistry ToolRegistry { get; init; }

    /// <summary>任务事件总线（必选）</summary>
    public required ITaskEventBus EventBus { get; init; }

    /// <summary>日志（必选）</summary>
    public required ILogger<AgentOrchestrator> Logger { get; init; }

    /// <summary>最大迭代次数（默认 10）</summary>
    public int MaxIterations { get; init; } = 10;

    // ---- 可选依赖 ----

    public MemoryIntegrationService? MemoryService { get; init; }
    public bool MemoryEnabled { get; init; } = true;
    public ComputerUseOrchestrator? ComputerUseOrchestrator { get; init; }
    public ToolExecutionPipeline? ToolPipeline { get; init; }
    public ITaskHistoryStore? HistoryStore { get; init; }
    public ConversationManager? ConversationManager { get; init; }
    public LearningEngine? LearningEngine { get; init; }
    public IAuditLogStore? AuditLogStore { get; init; }
    public ErrorHandler? ErrorHandler { get; init; }
    public AgentPersonality? Personality { get; init; }
}
