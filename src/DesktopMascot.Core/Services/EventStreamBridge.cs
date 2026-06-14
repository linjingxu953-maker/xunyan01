using DesktopMascot.Core.Interfaces;
using DesktopMascot.Core.Models;

namespace DesktopMascot.Core.Services;

/// <summary>
/// 事件流桥接服务 - 供 UI 层订阅实时事件
/// </summary>
public interface IEventStreamBridge
{
    /// <summary>订阅指定任务的事件</summary>
    IObservable<TaskEvent> SubscribeToTask(string taskId);

    /// <summary>订阅所有事件</summary>
    IObservable<TaskEvent> SubscribeToAll();

    /// <summary>获取指定任务的最近事件</summary>
    IReadOnlyList<TaskEvent> GetRecentEvents(string taskId, int count = 50);

    /// <summary>获取所有最近事件</summary>
    IReadOnlyList<TaskEvent> GetAllRecentEvents(int count = 100);
}

/// <summary>
/// 事件流桥接服务实现
/// </summary>
public class EventStreamBridge : IEventStreamBridge
{
    private readonly ITaskEventStream _eventStream;

    public EventStreamBridge(ITaskEventStream eventStream)
    {
        _eventStream = eventStream;
    }

    public IObservable<TaskEvent> SubscribeToTask(string taskId)
    {
        return _eventStream.Subscribe(taskId);
    }

    public IObservable<TaskEvent> SubscribeToAll()
    {
        return _eventStream.SubscribeAll();
    }

    public IReadOnlyList<TaskEvent> GetRecentEvents(string taskId, int count = 50)
    {
        return _eventStream.GetRecentEvents(taskId, count);
    }

    public IReadOnlyList<TaskEvent> GetAllRecentEvents(int count = 100)
    {
        return _eventStream.GetRecentEvents("", count);
    }
}
