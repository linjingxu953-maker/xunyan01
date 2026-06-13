using DesktopMascot.Core.Enums;
using DesktopMascot.Core.Interfaces;
using DesktopMascot.Core.Models;
using DesktopMascot.Core.Services;
using Moq;

namespace DesktopMascot.Core.Tests;

public class MascotStateMachineTests
{
    private readonly Mock<ITaskEventBus> _mockEventBus;
    private readonly MascotStateMachine _stateMachine;

    public MascotStateMachineTests()
    {
        _mockEventBus = new Mock<ITaskEventBus>();
        _stateMachine = new MascotStateMachine(_mockEventBus.Object);
    }

    [Fact]
    public void InitialState_ShouldBeIdle()
    {
        Assert.Equal(MascotState.Idle, _stateMachine.CurrentState);
    }

    [Fact]
    public void TryTransition_ValidTransition_ShouldSucceed()
    {
        var result = _stateMachine.TryTransition(MascotState.Listening, "开始监听");

        Assert.True(result);
        Assert.Equal(MascotState.Listening, _stateMachine.CurrentState);
    }

    [Fact]
    public void TryTransition_InvalidTransition_ShouldFail()
    {
        // Idle 不能直接转到 Working
        var result = _stateMachine.TryTransition(MascotState.Working);

        Assert.False(result);
        Assert.Equal(MascotState.Idle, _stateMachine.CurrentState);
    }

    [Fact]
    public void TryTransition_ShouldPublishEvent()
    {
        _stateMachine.TryTransition(MascotState.Listening, "测试事件");

        _mockEventBus.Verify(x => x.Publish(It.Is<TaskEvent>(
            e => e.State == MascotState.Listening && e.Message == "测试事件"
        )), Times.Once);
    }

    [Fact]
    public void StateChanged_ShouldBeRaised()
    {
        MascotState? raisedState = null;
        _stateMachine.StateChanged += s => raisedState = s;

        _stateMachine.TryTransition(MascotState.Listening);

        Assert.Equal(MascotState.Listening, raisedState);
    }

    [Fact]
    public void FullWorkflow_ShouldAllowAllTransitions()
    {
        // 完整工作流：Idle -> Listening -> Understanding -> Working -> Reporting -> Completed -> Idle
        Assert.True(_stateMachine.TryTransition(MascotState.Listening));
        Assert.True(_stateMachine.TryTransition(MascotState.Understanding));
        Assert.True(_stateMachine.TryTransition(MascotState.Working));
        Assert.True(_stateMachine.TryTransition(MascotState.Reporting));
        Assert.True(_stateMachine.TryTransition(MascotState.Completed));
        Assert.True(_stateMachine.TryTransition(MascotState.Idle));
    }

    [Fact]
    public void ErrorTransition_FromAnyState_ShouldWork()
    {
        _stateMachine.TryTransition(MascotState.Listening);
        _stateMachine.TryTransition(MascotState.Working);

        var result = _stateMachine.TryTransition(MascotState.Error, "出错了");

        Assert.True(result);
        Assert.Equal(MascotState.Error, _stateMachine.CurrentState);
    }

    [Fact]
    public void ForceTransition_ShouldBypassRules()
    {
        // 正常情况下 Idle 不能直接到 Completed
        _stateMachine.ForceTransition(MascotState.Completed, "强制完成");

        Assert.Equal(MascotState.Completed, _stateMachine.CurrentState);
    }

    [Fact]
    public void Reset_ShouldReturnToIdle()
    {
        _stateMachine.TryTransition(MascotState.Listening);
        _stateMachine.TryTransition(MascotState.Working);

        _stateMachine.Reset();

        Assert.Equal(MascotState.Idle, _stateMachine.CurrentState);
    }
}
