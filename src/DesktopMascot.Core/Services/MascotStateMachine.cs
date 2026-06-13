using DesktopMascot.Core.Enums;
using DesktopMascot.Core.Interfaces;
using DesktopMascot.Core.Models;

namespace DesktopMascot.Core.Services;

/// <summary>
/// 状态机 - 管理小人状态转换
/// </summary>
public class MascotStateMachine : IStateMachine
{
    private readonly ITaskEventBus _eventBus;
    private MascotState _currentState = MascotState.Idle;
    private readonly Dictionary<MascotState, HashSet<MascotState>> _allowedTransitions;

    public MascotState CurrentState => _currentState;

    public event Action<MascotState>? StateChanged;

    public MascotStateMachine(ITaskEventBus eventBus)
    {
        _eventBus = eventBus;
        _allowedTransitions = BuildTransitionRules();
    }

    /// <summary>
    /// 构建状态转换规则
    /// </summary>
    private static Dictionary<MascotState, HashSet<MascotState>> BuildTransitionRules()
    {
        return new Dictionary<MascotState, HashSet<MascotState>>
        {
            [MascotState.Idle] = new()
            {
                MascotState.Listening,
                MascotState.Error
            },
            [MascotState.Listening] = new()
            {
                MascotState.Understanding,
                MascotState.Working,
                MascotState.Error
            },
            [MascotState.Understanding] = new()
            {
                MascotState.ReadingContext,
                MascotState.Planning,
                MascotState.Working, // 简单任务可直接执行
                MascotState.Error
            },
            [MascotState.ReadingContext] = new()
            {
                MascotState.Planning,
                MascotState.Error
            },
            [MascotState.Planning] = new()
            {
                MascotState.Working,
                MascotState.WaitingApproval,
                MascotState.Error
            },
            [MascotState.WaitingApproval] = new()
            {
                MascotState.Working,
                MascotState.Idle, // 用户拒绝
                MascotState.Error
            },
            [MascotState.Working] = new()
            {
                MascotState.Reporting,
                MascotState.MemoryConfirm,
                MascotState.Completed,
                MascotState.Error
            },
            [MascotState.Reporting] = new()
            {
                MascotState.Completed,
                MascotState.Error
            },
            [MascotState.MemoryConfirm] = new()
            {
                MascotState.Completed,
                MascotState.Error
            },
            [MascotState.Completed] = new()
            {
                MascotState.Idle
            },
            [MascotState.Error] = new()
            {
                MascotState.Idle
            }
        };
    }

    /// <summary>
    /// 尝试转换到新状态
    /// </summary>
    public bool TryTransition(MascotState newState, string message = "", int progress = -1)
    {
        if (_currentState == newState)
            return true; // 已经在目标状态

        if (!_allowedTransitions.TryGetValue(_currentState, out var allowed))
            return false;

        if (!allowed.Contains(newState))
            return false;

        _currentState = newState;
        StateChanged?.Invoke(newState);

        // 发布状态事件
        _eventBus.Publish(new TaskEvent
        {
            State = newState,
            Message = message,
            Progress = progress
        });

        return true;
    }

    /// <summary>
    /// 强制转换（用于错误恢复）
    /// </summary>
    public void ForceTransition(MascotState newState, string message = "")
    {
        _currentState = newState;
        StateChanged?.Invoke(newState);

        _eventBus.Publish(new TaskEvent
        {
            State = newState,
            Message = message
        });
    }

    /// <summary>
    /// 重置到空闲状态
    /// </summary>
    public void Reset()
    {
        ForceTransition(MascotState.Idle, "状态重置");
    }
}

/// <summary>
/// 状态机接口
/// </summary>
public interface IStateMachine
{
    MascotState CurrentState { get; }
    event Action<MascotState>? StateChanged;
    bool TryTransition(MascotState newState, string message = "", int progress = -1);
    void ForceTransition(MascotState newState, string message = "");
    void Reset();
}
