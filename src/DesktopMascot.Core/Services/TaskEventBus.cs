using DesktopMascot.Core.Interfaces;
using DesktopMascot.Core.Models;

namespace DesktopMascot.Core.Services;

/// <summary>
/// 任务事件总线实现
/// </summary>
public class TaskEventBus : ITaskEventBus
{
    public event EventHandler<TaskEvent>? TaskEventPublished;

    public void Publish(TaskEvent evt)
    {
        TaskEventPublished?.Invoke(this, evt);
    }
}
