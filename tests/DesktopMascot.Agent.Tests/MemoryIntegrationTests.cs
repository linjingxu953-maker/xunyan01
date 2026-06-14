using DesktopMascot.Agent.Memory;
using DesktopMascot.Core.Enums;
using DesktopMascot.Core.Memory;
using DesktopMascot.Core.Models;
using Moq;

namespace DesktopMascot.Agent.Tests;

public class MemoryIntegrationTests
{
    private readonly Mock<IMemoryStore> _mockStore = new();
    private readonly Mock<Core.Logging.ILogger> _mockLogger = new();
    private readonly MemoryManager _memoryManager;
    private readonly MemoryIntegrationService _memoryService;

    public MemoryIntegrationTests()
    {
        _memoryManager = new MemoryManager(_mockStore.Object);
        _memoryService = new MemoryIntegrationService(_memoryManager, _mockLogger.Object);
    }

    [Fact]
    public async Task SearchRelevantMemories_ShouldReturnContext()
    {
        _mockStore.Setup(x => x.SearchAsync(It.IsAny<string>(), It.IsAny<MemoryType?>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemorySearchResult { Entries = new List<MemoryEntry> { new() { Content = "test memory" } } });

        var task = new AgentTask { Input = "test input", Type = TaskType.Chat };
        var context = await _memoryService.SearchRelevantMemoriesAsync(task);

        Assert.NotNull(context);
        Assert.True(context.HasRelevantMemories);
    }

    [Fact]
    public async Task SearchRelevantMemories_EmptyResult_ShouldReturnEmptyContext()
    {
        _mockStore.Setup(x => x.SearchAsync(It.IsAny<string>(), It.IsAny<MemoryType?>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemorySearchResult { Entries = new List<MemoryEntry>() });

        var task = new AgentTask { Input = "test", Type = TaskType.Chat };
        var context = await _memoryService.SearchRelevantMemoriesAsync(task);

        Assert.False(context.HasRelevantMemories);
    }

    [Fact]
    public async Task ProposeMemories_ShouldCreateProposals()
    {
        _mockStore.Setup(x => x.SearchAsync(It.IsAny<string>(), It.IsAny<MemoryType?>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemorySearchResult { Entries = new List<MemoryEntry>() });

        var task = new AgentTask { Input = "简洁回答", Type = TaskType.Chat };
        var result = new TaskResult { TaskId = "1", Success = true, Content = "test response" };

        var proposals = await _memoryService.ProposeMemoriesAsync(task, result);

        Assert.NotEmpty(proposals);
        Assert.Contains(proposals, p => p.Entry.Type == MemoryType.User);
    }

    [Fact]
    public async Task ProposeMemories_ShouldCreateHistoryEntry()
    {
        _mockStore.Setup(x => x.SearchAsync(It.IsAny<string>(), It.IsAny<MemoryType?>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemorySearchResult { Entries = new List<MemoryEntry>() });

        var task = new AgentTask { Title = "test", Input = "input", Type = TaskType.Chat };
        var result = new TaskResult { TaskId = "1", Success = true, Content = "response" };

        var proposals = await _memoryService.ProposeMemoriesAsync(task, result);

        Assert.Contains(proposals, p => p.Entry.Type == MemoryType.History);
    }

    [Fact]
    public async Task ProposeMemories_WithProjectPath_ShouldCreateProjectEntry()
    {
        _mockStore.Setup(x => x.SearchAsync(It.IsAny<string>(), It.IsAny<MemoryType?>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemorySearchResult { Entries = new List<MemoryEntry>() });

        var task = new AgentTask
        {
            Input = "分析项目",
            Type = TaskType.InspectProject,
            Parameters = new Dictionary<string, object> { ["ProjectPath"] = "C:\\MyProject" }
        };
        var result = new TaskResult { TaskId = "1", Success = true, Content = "analysis" };

        var proposals = await _memoryService.ProposeMemoriesAsync(task, result);

        Assert.Contains(proposals, p => p.Entry.Type == MemoryType.Project);
    }

    [Fact]
    public async Task SaveProposedMemories_ShouldSaveEntries()
    {
        _mockStore.Setup(x => x.SaveAsync(It.IsAny<MemoryEntry>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemoryEntry { Id = "1", Type = MemoryType.History });

        var proposals = new List<MemoryProposal>
        {
            new() { Entry = new MemoryEntry { Type = MemoryType.History, Key = "test" }, Reason = "test" }
        };

        var saved = await _memoryService.SaveProposedMemoriesAsync(proposals);

        Assert.Single(saved);
        _mockStore.Verify(x => x.SaveAsync(It.IsAny<MemoryEntry>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void MemoryContext_ToPromptContext_ShouldFormatCorrectly()
    {
        var context = new MemoryContext
        {
            UserPreferences = new List<MemoryEntry> { new() { Content = "偏好1" } },
            RelevantSkills = new List<MemoryEntry> { new() { Content = "技能1" } }
        };

        var prompt = context.ToPromptContext();

        Assert.Contains("用户偏好", prompt);
        Assert.Contains("偏好1", prompt);
        Assert.Contains("相关技能", prompt);
        Assert.Contains("技能1", prompt);
    }

    [Fact]
    public void MemoryContext_Empty_ShouldReturnEmptyString()
    {
        var context = new MemoryContext();
        var prompt = context.ToPromptContext();
        Assert.Equal("", prompt);
    }
}
