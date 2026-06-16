using DesktopMascot.Agent.Engines;
using DesktopMascot.Agent.Memory;
using DesktopMascot.Agent.Models;
using DesktopMascot.Agent.Providers;
using DesktopMascot.Agent.Tools;
using DesktopMascot.Core.Enums;
using DesktopMascot.Core.Interfaces;
using DesktopMascot.Core.Memory;
using DesktopMascot.Core.Models;
using Microsoft.Extensions.Logging;
using Moq;

namespace DesktopMascot.Agent.Tests;

public class StreamingTests
{
    private readonly Mock<ITaskEventBus> _mockEventBus = new();
    private readonly Mock<ILogger<AgentOrchestrator>> _mockLogger = new();

    [Fact]
    public async Task ExecuteStreamingAsync_ShouldYieldChunks()
    {
        var mockLlm = new Mock<ILlmProvider>();
        var chunks = new List<string> { "你好", "世界", "！" };

        mockLlm.Setup(x => x.ChatStreamAsync(
                It.IsAny<IEnumerable<LlmMessage>>(),
                It.IsAny<CancellationToken>()))
            .Returns(ToAsyncEnumerable(chunks));

        var orchestrator = new AgentOrchestrator(
            mockLlm.Object, new ToolRegistry(), _mockEventBus.Object, _mockLogger.Object);

        var task = new AgentTask { Title = "流式测试", Input = "测试", Type = TaskType.Chat };
        var results = new List<string>();

        await foreach (var chunk in orchestrator.ExecuteStreamingAsync(task))
        {
            results.Add(chunk);
        }

        Assert.Equal(3, results.Count);
        Assert.Equal("你好", results[0]);
        Assert.Equal("世界", results[1]);
        Assert.Equal("！", results[2]);
    }

    [Fact]
    public async Task ExecuteStreamingAsync_WithMemory_ShouldInjectContext()
    {
        var mockLlm = new Mock<ILlmProvider>();
        var mockMemoryStore = new Mock<IMemoryStore>();
        var mockLogger = new Mock<Core.Logging.ILogger>();
        var memoryManager = new MemoryManager(mockMemoryStore.Object);
        var memoryService = new MemoryIntegrationService(memoryManager, mockLogger.Object);

        mockLlm.Setup(x => x.ChatStreamAsync(
                It.IsAny<IEnumerable<LlmMessage>>(),
                It.IsAny<CancellationToken>()))
            .Returns(ToAsyncEnumerable(new[] { "响应" }));

        var orchestrator = new AgentOrchestrator(new AgentOrchestratorOptions
        {
            LlmProvider = mockLlm.Object, ToolRegistry = new ToolRegistry(),
            EventBus = _mockEventBus.Object, Logger = _mockLogger.Object,
            MemoryService = memoryService
        });

        var task = new AgentTask { Title = "记忆测试", Input = "测试", Type = TaskType.Chat };
        var results = new List<string>();

        await foreach (var chunk in orchestrator.ExecuteStreamingAsync(task))
        {
            results.Add(chunk);
        }

        Assert.Single(results);
        Assert.Equal("响应", results[0]);
    }

    [Fact]
    public async Task ExecuteStreamingAsync_EmptyInput_ShouldWork()
    {
        var mockLlm = new Mock<ILlmProvider>();
        mockLlm.Setup(x => x.ChatStreamAsync(
                It.IsAny<IEnumerable<LlmMessage>>(),
                It.IsAny<CancellationToken>()))
            .Returns(ToAsyncEnumerable(new[] { "空输入处理" }));

        var orchestrator = new AgentOrchestrator(
            mockLlm.Object, new ToolRegistry(), _mockEventBus.Object, _mockLogger.Object);

        var task = new AgentTask { Title = "空输入", Input = "", Type = TaskType.Chat };
        var results = new List<string>();

        await foreach (var chunk in orchestrator.ExecuteStreamingAsync(task))
        {
            results.Add(chunk);
        }

        Assert.Single(results);
    }

    private static async IAsyncEnumerable<T> ToAsyncEnumerable<T>(IEnumerable<T> source)
    {
        foreach (var item in source)
        {
            yield return item;
        }
        await Task.CompletedTask;
    }
}
