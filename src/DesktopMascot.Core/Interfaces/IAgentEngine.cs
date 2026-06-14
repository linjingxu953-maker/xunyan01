using DesktopMascot.Core.Models;

namespace DesktopMascot.Core.Interfaces;

/// <summary>
/// Agent 引擎接口
/// </summary>
public interface IAgentEngine
{
    /// <summary>执行任务</summary>
    Task<TaskResult> ExecuteAsync(AgentTask task, CancellationToken ct = default);

    /// <summary>流式执行任务</summary>
    IAsyncEnumerable<string> ExecuteStreamingAsync(AgentTask task, CancellationToken ct = default);
}
