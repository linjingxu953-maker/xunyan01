using DesktopMascot.Agent.Engines;
using DesktopMascot.Agent.Models;
using DesktopMascot.Agent.Providers;
using DesktopMascot.Agent.Tools;
using DesktopMascot.Core.Enums;
using DesktopMascot.Core.Interfaces;
using DesktopMascot.Core.Models;
using Microsoft.Extensions.Logging;
using Moq;

namespace DesktopMascot.Agent.Tests;

public class AgentOrchestratorTests
{
    private readonly Mock<ILlmProvider> _mockLlmProvider;
    private readonly Mock<ITaskEventBus> _mockEventBus;
    private readonly Mock<ILogger<AgentOrchestrator>> _mockLogger;
    private readonly ToolRegistry _toolRegistry;
    private readonly AgentOrchestrator _orchestrator;

    public AgentOrchestratorTests()
    {
        _mockLlmProvider = new Mock<ILlmProvider>();
        _mockEventBus = new Mock<ITaskEventBus>();
        _mockLogger = new Mock<ILogger<AgentOrchestrator>>();
        _toolRegistry = new ToolRegistry();
        _orchestrator = new AgentOrchestrator(
            _mockLlmProvider.Object,
            _toolRegistry,
            _mockEventBus.Object,
            _mockLogger.Object,
            maxIterations: 5);
    }

    [Fact]
    public async Task ExecuteAsync_SimpleResponse_ShouldReturnSuccess()
    {
        // Arrange
        var task = new AgentTask
        {
            Title = "测试",
            Input = "你好"
        };

        _mockLlmProvider.Setup(x => x.ChatAsync(
            It.IsAny<IEnumerable<LlmMessage>>(),
            It.IsAny<IEnumerable<ToolDefinition>?>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmResponse
            {
                Success = true,
                Content = "你好！有什么可以帮助你的吗？"
            });

        // Act
        var result = await _orchestrator.ExecuteAsync(task);

        // Assert
        Assert.True(result.Success);
        Assert.Contains("你好", result.Content);
    }

    [Fact]
    public async Task ExecuteAsync_LlmFailure_ShouldReturnFailed()
    {
        // Arrange
        var task = new AgentTask
        {
            Title = "测试",
            Input = "你好"
        };

        _mockLlmProvider.Setup(x => x.ChatAsync(
            It.IsAny<IEnumerable<LlmMessage>>(),
            It.IsAny<IEnumerable<ToolDefinition>?>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmResponse
            {
                Success = false,
                Error = "API 错误"
            });

        // Act
        var result = await _orchestrator.ExecuteAsync(task);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("API 错误", result.Error);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldPublishProgressEvents()
    {
        // Arrange
        var task = new AgentTask
        {
            Title = "测试",
            Input = "你好"
        };

        _mockLlmProvider.Setup(x => x.ChatAsync(
            It.IsAny<IEnumerable<LlmMessage>>(),
            It.IsAny<IEnumerable<ToolDefinition>?>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmResponse
            {
                Success = true,
                Content = "响应"
            });

        var publishedEvents = new List<TaskEvent>();
        _mockEventBus.Setup(x => x.Publish(It.IsAny<TaskEvent>()))
            .Callback<TaskEvent>(e => publishedEvents.Add(e));

        // Act
        await _orchestrator.ExecuteAsync(task);

        // Assert
        Assert.Contains(publishedEvents, e => e.State == MascotState.Working);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldCallLlmAtLeastOnce()
    {
        // Arrange
        var task = new AgentTask
        {
            Title = "测试",
            Input = "测试输入"
        };

        _mockLlmProvider.Setup(x => x.ChatAsync(
            It.IsAny<IEnumerable<LlmMessage>>(),
            It.IsAny<IEnumerable<ToolDefinition>?>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmResponse
            {
                Success = true,
                Content = "测试响应"
            });

        // Act
        await _orchestrator.ExecuteAsync(task);

        // Assert
        _mockLlmProvider.Verify(x => x.ChatAsync(
            It.IsAny<IEnumerable<LlmMessage>>(),
            It.IsAny<IEnumerable<ToolDefinition>?>(),
            It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    [Fact]
    public void ToolRegistry_ShouldBeAccessible()
    {
        Assert.NotNull(_orchestrator);
    }
}
