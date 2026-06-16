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

/// <summary>
/// 记忆集成端到端测试 - 验证完整流程
/// </summary>
public class MemoryE2ETests
{
    private readonly Mock<ITaskEventBus> _mockEventBus = new();
    private readonly Mock<ILogger<AgentOrchestrator>> _mockLogger = new();
    private readonly Mock<Core.Logging.ILogger> _mockCoreLogger = new();

    [Fact]
    public async Task FullFlow_MemoryInjection_ThenProposal_ShouldWork()
    {
        var mockStore = new Mock<IMemoryStore>();
        var memoryManager = new MemoryManager(mockStore.Object);
        var memoryService = new MemoryIntegrationService(memoryManager, _mockCoreLogger.Object);

        mockStore.Setup(x => x.SearchAsync(It.IsAny<string>(), It.IsAny<MemoryType?>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemorySearchResult
            {
                Entries = new List<MemoryEntry>
                {
                    new() { Type = MemoryType.User, Content = "用户喜欢简洁回答" },
                    new() { Type = MemoryType.History, Content = "上次总结网页成功", Tags = new Dictionary<string, string> { ["taskType"] = "SummarizePage" } }
                }
            });

        mockStore.Setup(x => x.SaveAsync(It.IsAny<MemoryEntry>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((MemoryEntry e, CancellationToken ct) => e);

        var mockLlm = new Mock<ILlmProvider>();
        var registry = new ToolRegistry();

        var orchestrator = new AgentOrchestrator(new AgentOrchestratorOptions
        {
            LlmProvider = mockLlm.Object, ToolRegistry = registry,
            EventBus = _mockEventBus.Object, Logger = _mockLogger.Object,
            MaxIterations = 3, MemoryService = memoryService
        });

        var capturedMessages = new List<LlmMessage>();
        mockLlm.Setup(x => x.ChatAsync(
                It.IsAny<IEnumerable<LlmMessage>>(),
                It.IsAny<IEnumerable<ToolDefinition>?>(),
                It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<LlmMessage>, IEnumerable<ToolDefinition>?, CancellationToken>(
                (msgs, _, _) => capturedMessages.AddRange(msgs))
            .ReturnsAsync(new LlmResponse
            {
                Success = true,
                Content = "这是简洁的回答"
            });

        var task = new AgentTask
        {
            Title = "测试对话",
            Input = "什么是量子计算？",
            Type = TaskType.Chat,
            Parameters = new Dictionary<string, object> { ["TaskType"] = TaskType.Chat }
        };

        var result = await orchestrator.ExecuteAsync(task);

        Assert.True(result.Success);

        var systemMessage = capturedMessages.First(m => m.Role == "system");
        var userMessage = capturedMessages.First(m => m.Role == "user");
        Assert.Contains("AI 助手", systemMessage.Content);
        Assert.Contains("用户喜欢简洁回答", userMessage.Content);
        Assert.Contains("上次总结网页成功", userMessage.Content);

        mockStore.Verify(x => x.SearchAsync(It.IsAny<string>(), It.IsAny<MemoryType?>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.AtLeastOnce);
        mockStore.Verify(x => x.SaveAsync(It.IsAny<MemoryEntry>(), It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task FullFlow_SkillAutoGeneration_ShouldTrigger()
    {
        var mockStore = new Mock<IMemoryStore>();
        var memoryManager = new MemoryManager(mockStore.Object);
        var memoryService = new MemoryIntegrationService(memoryManager, _mockCoreLogger.Object);

        var historyEntries = Enumerable.Range(1, 5).Select(i => new MemoryEntry
        {
            Type = MemoryType.History,
            Content = $"历史任务{i}",
            Tags = new Dictionary<string, string> { ["taskType"] = "SummarizePage" }
        }).ToList();

        mockStore.Setup(x => x.SearchAsync(It.IsAny<string>(), It.IsAny<MemoryType?>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemorySearchResult { Entries = historyEntries });

        mockStore.Setup(x => x.GetByKeyAsync(It.IsAny<string>(), It.IsAny<MemoryType>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((MemoryEntry?)null);

        mockStore.Setup(x => x.SaveAsync(It.IsAny<MemoryEntry>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((MemoryEntry e, CancellationToken ct) => e);

        var mockLlm = new Mock<ILlmProvider>();
        var registry = new ToolRegistry();

        var orchestrator = new AgentOrchestrator(new AgentOrchestratorOptions
        {
            LlmProvider = mockLlm.Object, ToolRegistry = registry,
            EventBus = _mockEventBus.Object, Logger = _mockLogger.Object,
            MaxIterations = 3, MemoryService = memoryService
        });

        mockLlm.Setup(x => x.ChatAsync(
                It.IsAny<IEnumerable<LlmMessage>>(),
                It.IsAny<IEnumerable<ToolDefinition>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmResponse
            {
                Success = true,
                Content = "网页总结完成"
            });

        var task = new AgentTask
        {
            Title = "总结网页",
            Input = "总结这个页面",
            Type = TaskType.SummarizePage,
            Parameters = new Dictionary<string, object> { ["TaskType"] = TaskType.SummarizePage }
        };

        var result = await orchestrator.ExecuteAsync(task);

        Assert.True(result.Success);

        mockStore.Verify(x => x.SaveAsync(
            It.Is<MemoryEntry>(e => e.Type == MemoryType.Skill),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task FullFlow_MemoryContextInjectedIntoPrompt_ShouldAppearInUserMessage()
    {
        var mockStore = new Mock<IMemoryStore>();
        var memoryManager = new MemoryManager(mockStore.Object);
        var memoryService = new MemoryIntegrationService(memoryManager, _mockCoreLogger.Object);

        mockStore.Setup(x => x.SearchAsync(It.IsAny<string>(), It.IsAny<MemoryType?>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemorySearchResult
            {
                Entries = new List<MemoryEntry>
                {
                    new() { Type = MemoryType.User, Content = "用户是开发者" },
                    new() { Type = MemoryType.Skill, Content = "报错分析技能：先看错误类型" }
                }
            });

        var mockLlm = new Mock<ILlmProvider>();
        var registry = new ToolRegistry();

        var orchestrator = new AgentOrchestrator(new AgentOrchestratorOptions
        {
            LlmProvider = mockLlm.Object, ToolRegistry = registry,
            EventBus = _mockEventBus.Object, Logger = _mockLogger.Object,
            MaxIterations = 3, MemoryService = memoryService
        });

        var capturedMessages = new List<LlmMessage>();
        mockLlm.Setup(x => x.ChatAsync(
                It.IsAny<IEnumerable<LlmMessage>>(),
                It.IsAny<IEnumerable<ToolDefinition>?>(),
                It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<LlmMessage>, IEnumerable<ToolDefinition>?, CancellationToken>(
                (msgs, _, _) => capturedMessages.AddRange(msgs))
            .ReturnsAsync(new LlmResponse
            {
                Success = true,
                Content = "分析完成"
            });

        var task = new AgentTask
        {
            Title = "分析报错",
            Input = "TypeError: undefined",
            Type = TaskType.AnalyzeError,
            Parameters = new Dictionary<string, object> { ["TaskType"] = TaskType.AnalyzeError }
        };

        await orchestrator.ExecuteAsync(task);

        var userMessage = capturedMessages.First(m => m.Role == "user");
        Assert.Contains("用户是开发者", userMessage.Content);
        Assert.Contains("报错分析技能", userMessage.Content);
    }

    [Fact]
    public async Task FullFlow_WithoutMemoryService_ShouldStillWork()
    {
        var mockLlm = new Mock<ILlmProvider>();
        var registry = new ToolRegistry();

        var orchestrator = new AgentOrchestrator(new AgentOrchestratorOptions
        {
            LlmProvider = mockLlm.Object, ToolRegistry = registry,
            EventBus = _mockEventBus.Object, Logger = _mockLogger.Object,
            MaxIterations = 3, MemoryService = null
        });

        mockLlm.Setup(x => x.ChatAsync(
                It.IsAny<IEnumerable<LlmMessage>>(),
                It.IsAny<IEnumerable<ToolDefinition>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmResponse
            {
                Success = true,
                Content = "正常回答"
            });

        var task = new AgentTask
        {
            Title = "测试",
            Input = "你好",
            Type = TaskType.Chat,
            Parameters = new Dictionary<string, object> { ["TaskType"] = TaskType.Chat }
        };

        var result = await orchestrator.ExecuteAsync(task);

        Assert.True(result.Success);
        Assert.Equal("正常回答", result.Content);
    }
}
