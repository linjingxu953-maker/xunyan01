using DesktopMascot.Core.Enums;
using DesktopMascot.Core.Models;
using DesktopMascot.Core.Services;

namespace DesktopMascot.Core.Tests;

public class AgentEngineStubTests
{
    [Fact]
    public async Task ExecuteAsync_ShouldReturnSuccess()
    {
        // Arrange
        var engine = new AgentEngineStub();
        var task = new AgentTask
        {
            Title = "测试任务",
            Input = "测试输入",
            Type = TaskType.Chat
        };

        // Act
        var result = await engine.ExecuteAsync(task);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(task.Id, result.TaskId);
        Assert.NotEmpty(result.Content);
    }

    [Fact]
    public async Task ExecuteAsync_ChatTask_ShouldIncludeInput()
    {
        // Arrange
        var engine = new AgentEngineStub();
        var task = new AgentTask
        {
            Title = "聊天",
            Input = "你好",
            Type = TaskType.Chat
        };

        // Act
        var result = await engine.ExecuteAsync(task);

        // Assert
        Assert.Contains("你好", result.Content);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldCompleteWithinTimeout()
    {
        // Arrange
        var engine = new AgentEngineStub();
        var task = new AgentTask { Type = TaskType.Chat };
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        // Act
        var result = await engine.ExecuteAsync(task, cts.Token);

        // Assert
        Assert.True(result.Success);
    }
}
