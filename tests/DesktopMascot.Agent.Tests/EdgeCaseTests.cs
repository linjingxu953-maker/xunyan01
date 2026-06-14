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

public class EdgeCaseTests
{
    private readonly Mock<ITaskEventBus> _mockEventBus = new();
    private readonly Mock<ILogger<AgentOrchestrator>> _mockLogger = new();

    [Fact]
    public async Task ExecuteAsync_EmptyInput_ShouldHandleGracefully()
    {
        var mockLlm = new Mock<ILlmProvider>();
        mockLlm.Setup(x => x.ChatAsync(
                It.IsAny<IEnumerable<LlmMessage>>(),
                It.IsAny<IEnumerable<ToolDefinition>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmResponse { Success = true, Content = "空输入处理" });

        var orchestrator = new AgentOrchestrator(
            mockLlm.Object, new ToolRegistry(), _mockEventBus.Object, _mockLogger.Object);

        var task = new AgentTask { Title = "空输入", Input = "", Type = TaskType.Chat };
        var result = await orchestrator.ExecuteAsync(task);

        Assert.True(result.Success);
    }

    [Fact]
    public async Task ExecuteAsync_VeryLongInput_ShouldHandleGracefully()
    {
        var mockLlm = new Mock<ILlmProvider>();
        mockLlm.Setup(x => x.ChatAsync(
                It.IsAny<IEnumerable<LlmMessage>>(),
                It.IsAny<IEnumerable<ToolDefinition>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmResponse { Success = true, Content = "长输入处理" });

        var orchestrator = new AgentOrchestrator(
            mockLlm.Object, new ToolRegistry(), _mockEventBus.Object, _mockLogger.Object);

        var longInput = new string('A', 100000);
        var task = new AgentTask { Title = "长输入", Input = longInput, Type = TaskType.Chat };
        var result = await orchestrator.ExecuteAsync(task);

        Assert.True(result.Success);
    }

    [Fact]
    public async Task ExecuteAsync_LlmFailure_ShouldReturnFailed()
    {
        var mockLlm = new Mock<ILlmProvider>();
        mockLlm.Setup(x => x.ChatAsync(
                It.IsAny<IEnumerable<LlmMessage>>(),
                It.IsAny<IEnumerable<ToolDefinition>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmResponse { Success = false, Error = "API 超时" });

        var orchestrator = new AgentOrchestrator(
            mockLlm.Object, new ToolRegistry(), _mockEventBus.Object, _mockLogger.Object);

        var task = new AgentTask { Title = "LLM失败", Input = "测试", Type = TaskType.Chat };
        var result = await orchestrator.ExecuteAsync(task);

        Assert.False(result.Success);
        Assert.Contains("API 超时", result.Error);
    }

    [Fact]
    public async Task ExecuteAsync_ConcurrentTasks_ShouldHandleIndependently()
    {
        var mockLlm = new Mock<ILlmProvider>();
        mockLlm.Setup(x => x.ChatAsync(
                It.IsAny<IEnumerable<LlmMessage>>(),
                It.IsAny<IEnumerable<ToolDefinition>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmResponse { Success = true, Content = "完成" });

        var orchestrator = new AgentOrchestrator(
            mockLlm.Object, new ToolRegistry(), _mockEventBus.Object, _mockLogger.Object);

        var tasks = Enumerable.Range(0, 5).Select(i => new AgentTask
        {
            Title = $"并发任务{i}",
            Input = $"输入{i}",
            Type = TaskType.Chat
        }).ToList();

        var results = await Task.WhenAll(tasks.Select(t => orchestrator.ExecuteAsync(t)));

        Assert.All(results, r => Assert.True(r.Success));
    }

    [Fact]
    public async Task ExecuteAsync_Cancellation_ShouldReturnCancelled()
    {
        var mockLlm = new Mock<ILlmProvider>();
        mockLlm.Setup(x => x.ChatAsync(
                It.IsAny<IEnumerable<LlmMessage>>(),
                It.IsAny<IEnumerable<ToolDefinition>?>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        var orchestrator = new AgentOrchestrator(
            mockLlm.Object, new ToolRegistry(), _mockEventBus.Object, _mockLogger.Object);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var task = new AgentTask { Title = "取消测试", Input = "测试", Type = TaskType.Chat };
        var result = await orchestrator.ExecuteAsync(task, cts.Token);

        Assert.False(result.Success);
    }

    [Fact]
    public async Task ExecuteAsync_SpecialCharacters_ShouldHandleGracefully()
    {
        var mockLlm = new Mock<ILlmProvider>();
        mockLlm.Setup(x => x.ChatAsync(
                It.IsAny<IEnumerable<LlmMessage>>(),
                It.IsAny<IEnumerable<ToolDefinition>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmResponse { Success = true, Content = "特殊字符处理" });

        var orchestrator = new AgentOrchestrator(
            mockLlm.Object, new ToolRegistry(), _mockEventBus.Object, _mockLogger.Object);

        var specialInput = "测试<script>alert('xss')</script>\"引号\"'单引号\\反斜杠";
        var task = new AgentTask { Title = "特殊字符", Input = specialInput, Type = TaskType.Chat };
        var result = await orchestrator.ExecuteAsync(task);

        Assert.True(result.Success);
    }

    [Fact]
    public async Task ExecuteAsync_UnicodeInput_ShouldHandleGracefully()
    {
        var mockLlm = new Mock<ILlmProvider>();
        mockLlm.Setup(x => x.ChatAsync(
                It.IsAny<IEnumerable<LlmMessage>>(),
                It.IsAny<IEnumerable<ToolDefinition>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmResponse { Success = true, Content = "Unicode处理" });

        var orchestrator = new AgentOrchestrator(
            mockLlm.Object, new ToolRegistry(), _mockEventBus.Object, _mockLogger.Object);

        var unicodeInput = "测试中文😀🎉🇨🇳日本語";
        var task = new AgentTask { Title = "Unicode", Input = unicodeInput, Type = TaskType.Chat };
        var result = await orchestrator.ExecuteAsync(task);

        Assert.True(result.Success);
    }
}
