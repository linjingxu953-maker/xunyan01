using DesktopMascot.Core.Enums;
using DesktopMascot.Core.Interfaces;
using DesktopMascot.Core.Models;
using DesktopMascot.Core.Tools;

namespace DesktopMascot.Core.Services;

/// <summary>
/// 任务路由器实现 - 集成 TaskEventStream 和 ToolExecutionPipeline
/// </summary>
public class EnhancedTaskRouter : ITaskRouter
{
    private readonly IAgentEngine _agent;
    private readonly ITaskEventStream _eventStream;
    private readonly ToolExecutionPipeline _toolPipeline;
    private readonly MascotStateMachine _stateMachine;
    private readonly Dictionary<string, CancellationTokenSource> _activeTasks = new();
    private readonly Dictionary<string, AgentTask> _taskStore = new();

    public EnhancedTaskRouter(
        IAgentEngine agent,
        ITaskEventStream eventStream,
        ToolExecutionPipeline toolPipeline)
    {
        _agent = agent;
        _eventStream = eventStream;
        _toolPipeline = toolPipeline;
        _stateMachine = new MascotStateMachine(new TaskEventBusAdapter(eventStream));
    }

    public MascotStateMachine StateMachine => _stateMachine;

    public async Task<TaskResult> DispatchAsync(AgentTask task, CancellationToken ct = default)
    {
        var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _activeTasks[task.Id] = linkedCts;
        _taskStore[task.Id] = task;

        try
        {
            // 发布任务开始事件
            _eventStream.Publish(TaskEvent.TaskStarted(task.Id, $"开始处理任务: {task.Title}"));

            // 状态转换: Idle -> Listening
            _stateMachine.TryTransition(MascotState.Listening, "正在接收任务...");

            // 状态转换: Listening -> Understanding
            _stateMachine.TryTransition(MascotState.Understanding, "理解用户意图...");
            _eventStream.Publish(TaskEvent.ProgressUpdated(task.Id, 10, "理解用户意图..."));

            // 状态转换: Understanding -> Working
            _stateMachine.TryTransition(MascotState.Working, "正在执行任务...");
            _eventStream.Publish(TaskEvent.ProgressUpdated(task.Id, 30, "开始执行任务..."));

            // 执行任务
            var result = await _agent.ExecuteAsync(task, linkedCts.Token);

            if (result.Success)
            {
                // 状态转换: Working -> Completed
                _stateMachine.TryTransition(MascotState.Completed, "任务完成");
                _eventStream.Publish(TaskEvent.TaskCompleted(task.Id, "任务执行成功"));
            }
            else
            {
                // 状态转换: Working -> Error
                _stateMachine.TryTransition(MascotState.Error, result.Error ?? "任务失败");
                _eventStream.Publish(TaskEvent.TaskFailed(task.Id, result.Error ?? "任务失败"));
            }

            // 延迟后回到 Idle
            await Task.Delay(500, linkedCts.Token);
            _stateMachine.TryTransition(MascotState.Idle, "空闲");

            return result;
        }
        catch (OperationCanceledException)
        {
            // 任务被取消
            _stateMachine.TryTransition(MascotState.Error, "任务已取消");
            _eventStream.Publish(TaskEvent.TaskFailed(task.Id, "任务已取消"));
            await Task.Delay(500);
            _stateMachine.TryTransition(MascotState.Idle, "空闲");

            return TaskResult.Failed(task.Id, "任务已取消");
        }
        catch (Exception ex)
        {
            // 异常处理
            _stateMachine.TryTransition(MascotState.Error, $"执行异常: {ex.Message}");
            _eventStream.Publish(TaskEvent.TaskFailed(task.Id, ex.Message));
            await Task.Delay(500);
            _stateMachine.TryTransition(MascotState.Idle, "空闲");

            return TaskResult.Failed(task.Id, ex.Message);
        }
        finally
        {
            _activeTasks.Remove(task.Id);
            _taskStore.Remove(task.Id);
            linkedCts.Dispose();
        }
    }

    /// <summary>
    /// 取消指定任务
    /// </summary>
    public bool CancelTask(string taskId)
    {
        if (_activeTasks.TryGetValue(taskId, out var cts))
        {
            cts.Cancel();
            _eventStream.Publish(TaskEvent.ProgressUpdated(taskId, 0, "任务已取消"));
            return true;
        }
        return false;
    }

    /// <summary>
    /// 取消所有活跃任务
    /// </summary>
    public void CancelAllTasks()
    {
        foreach (var cts in _activeTasks.Values)
        {
            cts.Cancel();
        }
        _activeTasks.Clear();
    }

    /// <summary>
    /// 获取任务状态
    /// </summary>
    public AgentTask? GetTask(string taskId)
    {
        return _taskStore.TryGetValue(taskId, out var task) ? task : null;
    }

    /// <summary>
    /// 获取所有活跃任务
    /// </summary>
    public IReadOnlyList<AgentTask> GetActiveTasks()
    {
        return _taskStore.Values.ToList().AsReadOnly();
    }
}

/// <summary>
/// TaskEventBus 适配器 - 将 ITaskEventStream 适配为 ITaskEventBus
/// </summary>
internal class TaskEventBusAdapter : ITaskEventBus
{
    private readonly ITaskEventStream _eventStream;

    public TaskEventBusAdapter(ITaskEventStream eventStream)
    {
        _eventStream = eventStream;
    }

    public event EventHandler<TaskEvent>? TaskEventPublished
    {
        add { }
        remove { }
    }

    public void Publish(TaskEvent evt)
    {
        _eventStream.Publish(evt);
    }
}
