using DesktopMascot.Core.Models;

namespace DesktopMascot.Core.Interfaces;

/// <summary>
/// 任务事件总线接口
/// </summary>
public interface ITaskEventBus
{
    event EventHandler<TaskEvent>? TaskEventPublished;
    void Publish(TaskEvent evt);
}
