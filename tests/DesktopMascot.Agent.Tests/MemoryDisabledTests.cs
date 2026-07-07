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

public sealed class MemoryDisabledTests
{
    [Fact]
    public async Task ExecuteAsync_WhenMemoryDisabled_ShouldNotSearchOrSaveMemories()
    {
        var memoryStore = new Mock<IMemoryStore>();
        var memoryManager = new MemoryManager(memoryStore.Object);
        var coreLogger = new Mock<DesktopMascot.Core.Logging.ILogger>();
        var memoryService = new MemoryIntegrationService(memoryManager, coreLogger.Object);

        var llm = new Mock<ILlmProvider>();
        llm.Setup(x => x.ChatAsync(
                It.IsAny<IEnumerable<LlmMessage>>(),
                It.IsAny<IEnumerable<ToolDefinition>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmResponse
            {
                Success = true,
                Content = "memory disabled response"
            });

        var orchestrator = new AgentOrchestrator(new AgentOrchestratorOptions
        {
            LlmProvider = llm.Object,
            ToolRegistry = new ToolRegistry(),
            EventBus = new Mock<ITaskEventBus>().Object,
            Logger = new Mock<ILogger<AgentOrchestrator>>().Object,
            MemoryService = memoryService,
            MemoryEnabled = false
        });

        var result = await orchestrator.ExecuteAsync(new AgentTask
        {
            Title = "memory disabled",
            Input = "do not use memory",
            Type = TaskType.Chat
        });

        Assert.True(result.Success);
        memoryStore.Verify(x => x.SearchAsync(
            It.IsAny<string>(),
            It.IsAny<MemoryType?>(),
            It.IsAny<int>(),
            It.IsAny<CancellationToken>()), Times.Never);
        memoryStore.Verify(x => x.SaveAsync(
            It.IsAny<MemoryEntry>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }
}
