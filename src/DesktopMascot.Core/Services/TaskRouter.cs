using DesktopMascot.Core.Enums;
using DesktopMascot.Core.Interfaces;
using DesktopMascot.Core.Models;

namespace DesktopMascot.Core.Services;

/// <summary>
/// 任务路由器实现
/// </summary>
public class TaskRouter : ITaskRouter
{
    private readonly IAgentEngine _agent;
    private readonly ITaskEventBus _eventBus;
    private readonly MascotStateMachine _stateMachine;
    private readonly Dictionary<string, CancellationTokenSource> _activeTasks = new();

    public TaskRouter(IAgentEngine agent, ITaskEventBus eventBus)
    {
        _agent = agent;
        _eventBus = eventBus;
        _stateMachine = new MascotStateMachine(eventBus);
    }

    public MascotStateMachine StateMachine => _stateMachine;

    public async Task<TaskResult> DispatchAsync(AgentTask task, CancellationToken ct = default)
    {
        var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _activeTasks[task.Id] = linkedCts;

        try
        {
            // 状态转换: Idle -> Listening
            _stateMachine.TryTransition(MascotState.Listening, "正在接收任务...");

            // 模拟理解阶段
            _stateMachine.TryTransition(MascotState.Understanding, "理解用户意图...");
            await Task.Delay(100, linkedCts.Token);

            // 状态转换: Understanding -> Working
            _stateMachine.TryTransition(MascotState.Working, "正在执行任务...", 50);

            // 执行任务
            var result = await _agent.ExecuteAsync(task, linkedCts.Token);

            if (result.Success)
            {
                // 状态转换: Working -> Completed
                _stateMachine.TryTransition(MascotState.Completed, "任务完成", 100);
            }
            else
            {
                // 状态转换: Working -> Error
                _stateMachine.TryTransition(MascotState.Error, result.Error ?? "任务失败");
            }

            // 延迟后回到 Idle
            await Task.Delay(500, linkedCts.Token);
            _stateMachine.TryTransition(MascotState.Idle, "空闲");

            return result;
        }
        catch (OperationCanceledException)
        {
            // 任务被取消 - 使用 None 确保状态归位不因令牌取消而跳过
            _stateMachine.TryTransition(MascotState.Error, "任务已取消");
            await Task.Delay(500, CancellationToken.None);
            _stateMachine.TryTransition(MascotState.Idle, "空闲");

            return TaskResult.Failed(task.Id, "任务已取消");
        }
        catch (Exception ex)
        {
            // 异常处理
            _stateMachine.TryTransition(MascotState.Error, $"执行异常: {ex.Message}");
            await Task.Delay(500);
            _stateMachine.TryTransition(MascotState.Idle, "空闲");

            return TaskResult.Failed(task.Id, ex.Message);
        }
        finally
        {
            _activeTasks.Remove(task.Id);
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
}
