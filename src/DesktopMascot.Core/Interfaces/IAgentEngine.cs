using DesktopMascot.Core.Models;

namespace DesktopMascot.Core.Interfaces;

/// <summary>
/// Agent 引擎接口
/// </summary>
public interface IAgentEngine
{
    Task<TaskResult> ExecuteAsync(AgentTask task, CancellationToken ct = default);
}
