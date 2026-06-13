using System.Collections.Concurrent;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using DesktopMascot.Core.Interfaces;
using DesktopMascot.Core.Models;

namespace DesktopMascot.Core.Services;

/// <summary>
/// 任务事件流实现
/// </summary>
public class TaskEventStream : ITaskEventStream
{
    private readonly Subject<TaskEvent> _subject = new();
    private readonly ConcurrentDictionary<string, ConcurrentBag<TaskEvent>> _taskEvents = new();
    private readonly ConcurrentBag<TaskEvent> _allEvents = new();
    private const int MaxEventsPerTask = 500;

    /// <summary>
    /// 发布任务事件
    /// </summary>
    public void Publish(TaskEvent evt)
    {
        _subject.OnNext(evt);

        // 存储到任务事件列表
        var taskEvents = _taskEvents.GetOrAdd(evt.TaskId, _ => new ConcurrentBag<TaskEvent>());
        taskEvents.Add(evt);

        // 清理旧事件（保留最近的）
        if (taskEvents.Count > MaxEventsPerTask)
        {
            var events = taskEvents.ToArray();
            var toKeep = events.OrderByDescending(e => e.Timestamp).Take(MaxEventsPerTask / 2).ToList();
            _taskEvents[evt.TaskId] = new ConcurrentBag<TaskEvent>(toKeep);
        }

        // 存储到全局事件列表
        _allEvents.Add(evt);
    }

    /// <summary>
    /// 订阅指定任务的事件
    /// </summary>
    public IObservable<TaskEvent> Subscribe(string taskId)
    {
        return _subject.Where(e => e.TaskId == taskId);
    }

    /// <summary>
    /// 订阅所有事件
    /// </summary>
    public IObservable<TaskEvent> SubscribeAll()
    {
        return _subject.AsObservable();
    }

    /// <summary>
    /// 获取指定任务的最近事件
    /// </summary>
    public IReadOnlyList<TaskEvent> GetRecentEvents(string taskId, int count = 50)
    {
        if (_taskEvents.TryGetValue(taskId, out var events))
        {
            return events
                .OrderByDescending(e => e.Timestamp)
                .Take(count)
                .ToList()
                .AsReadOnly();
        }

        return Array.Empty<TaskEvent>();
    }

    /// <summary>
    /// 获取所有任务的最近事件
    /// </summary>
    public IReadOnlyList<TaskEvent> GetAllRecentEvents(int count = 100)
    {
        return _allEvents
            .OrderByDescending(e => e.Timestamp)
            .Take(count)
            .ToList()
            .AsReadOnly();
    }
}
