using DesktopMascot.Core.Models;

namespace DesktopMascot.Core.Interfaces;

/// <summary>
/// 任务路由器接口
/// </summary>
public interface ITaskRouter
{
    Task<TaskResult> DispatchAsync(AgentTask task, CancellationToken ct = default);
    bool CancelTask(string taskId);
    void CancelAllTasks();
}
