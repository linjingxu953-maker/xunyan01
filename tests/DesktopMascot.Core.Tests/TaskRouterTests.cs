using DesktopMascot.Core.Enums;
using DesktopMascot.Core.Interfaces;
using DesktopMascot.Core.Models;
using DesktopMascot.Core.Services;
using Moq;

namespace DesktopMascot.Core.Tests;

public class TaskRouterTests
{
    private readonly Mock<IAgentEngine> _mockAgent;
    private readonly Mock<ITaskEventBus> _mockEventBus;
    private readonly TaskRouter _router;

    public TaskRouterTests()
    {
        _mockAgent = new Mock<IAgentEngine>();
        _mockEventBus = new Mock<ITaskEventBus>();
        _router = new TaskRouter(_mockAgent.Object, _mockEventBus.Object);
    }

    [Fact]
    public async Task DispatchAsync_SuccessfulTask_ShouldReturnSuccess()
    {
        // Arrange
        var task = new AgentTask
        {
            Title = "测试",
            Type = TaskType.Chat
        };

        _mockAgent.Setup(x => x.ExecuteAsync(task, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TaskResult
            {
                TaskId = task.Id,
                Success = true,
                Content = "成功"
            });

        // Act
        var result = await _router.DispatchAsync(task);

        // Assert
        Assert.True(result.Success);
        Assert.Equal("成功", result.Content);
    }

    [Fact]
    public async Task DispatchAsync_FailedTask_ShouldReturnFailed()
    {
        // Arrange
        var task = new AgentTask
        {
            Title = "测试",
            Type = TaskType.Chat
        };

        _mockAgent.Setup(x => x.ExecuteAsync(task, It.IsAny<CancellationToken>()))
            .ReturnsAsync(TaskResult.Failed(task.Id, "测试错误"));

        // Act
        var result = await _router.DispatchAsync(task);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("测试错误", result.Error);
    }

    [Fact]
    public async Task DispatchAsync_ShouldPublishStateEvents()
    {
        // Arrange
        var task = new AgentTask
        {
            Title = "测试",
            Type = TaskType.Chat
        };

        _mockAgent.Setup(x => x.ExecuteAsync(task, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TaskResult { TaskId = task.Id, Success = true });

        var publishedEvents = new List<TaskEvent>();
        _mockEventBus.Setup(x => x.Publish(It.IsAny<TaskEvent>()))
            .Callback<TaskEvent>(e => publishedEvents.Add(e));

        // Act
        await _router.DispatchAsync(task);

        // Assert - 验证关键状态转换
        Assert.Contains(publishedEvents, e => e.State == MascotState.Listening);
        Assert.Contains(publishedEvents, e => e.State == MascotState.Understanding);
        Assert.Contains(publishedEvents, e => e.State == MascotState.Working);
        Assert.Contains(publishedEvents, e => e.State == MascotState.Completed);
        Assert.Contains(publishedEvents, e => e.State == MascotState.Idle);
    }

    [Fact]
    public async Task DispatchAsync_Exception_ShouldPublishErrorEvent()
    {
        // Arrange
        var task = new AgentTask
        {
            Title = "测试",
            Type = TaskType.Chat
        };

        _mockAgent.Setup(x => x.ExecuteAsync(task, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("测试异常"));

        var publishedEvents = new List<TaskEvent>();
        _mockEventBus.Setup(x => x.Publish(It.IsAny<TaskEvent>()))
            .Callback<TaskEvent>(e => publishedEvents.Add(e));

        // Act
        var result = await _router.DispatchAsync(task);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("测试异常", result.Error);
        Assert.Contains(publishedEvents, e => e.State == MascotState.Error);
    }

    [Fact]
    public async Task DispatchAsync_Cancellation_ShouldReturnCancelled()
    {
        // Arrange
        var task = new AgentTask
        {
            Title = "测试",
            Type = TaskType.Chat
        };

        _mockAgent.Setup(x => x.ExecuteAsync(task, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        // Act
        var result = await _router.DispatchAsync(task);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("取消", result.Error);
    }

    [Fact]
    public async Task CancelTask_ExistingTask_ShouldReturnTrue()
    {
        // Arrange
        var task = new AgentTask
        {
            Title = "测试",
            Type = TaskType.Chat
        };

        var tcs = new TaskCompletionSource<TaskResult>();
        _mockAgent.Setup(x => x.ExecuteAsync(task, It.IsAny<CancellationToken>()))
            .Returns(tcs.Task);

        // 启动任务但不等待完成
        var dispatchTask = _router.DispatchAsync(task);
        await Task.Delay(50); // 等待任务开始

        // Act
        var cancelled = _router.CancelTask(task.Id);

        // Assert
        Assert.True(cancelled);
        tcs.SetCanceled();
    }

    [Fact]
    public void CancelTask_NonExistingTask_ShouldReturnFalse()
    {
        var result = _router.CancelTask("non-existing-id");
        Assert.False(result);
    }

    [Fact]
    public void StateMachine_ShouldBeAccessible()
    {
        Assert.NotNull(_router.StateMachine);
        Assert.Equal(MascotState.Idle, _router.StateMachine.CurrentState);
    }
}
