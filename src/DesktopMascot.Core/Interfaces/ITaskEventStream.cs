using DesktopMascot.Core.Models;

namespace DesktopMascot.Core.Interfaces;

/// <summary>
/// 任务事件流接口
/// </summary>
public interface ITaskEventStream
{
    /// <summary>
    /// 发布任务事件
    /// </summary>
    void Publish(TaskEvent evt);

    /// <summary>
    /// 订阅指定任务的事件
    /// </summary>
    IObservable<TaskEvent> Subscribe(string taskId);

    /// <summary>
    /// 订阅所有事件
    /// </summary>
    IObservable<TaskEvent> SubscribeAll();

    /// <summary>
    /// 获取指定任务的最近事件
    /// </summary>
    IReadOnlyList<TaskEvent> GetRecentEvents(string taskId, int count = 50);
}
