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

public class ComputerUseIntegrationTests
{
    private readonly Mock<ITaskEventBus> _mockEventBus = new();
    private readonly Mock<ILogger<AgentOrchestrator>> _mockLogger = new();
    private readonly Mock<ILogger<ComputerUseOrchestrator>> _mockCuLogger = new();

    [Fact]
    public async Task ExecuteAsync_ComputerUseTask_ShouldUseComputerUseOrchestrator()
    {
        var mockLlm = new Mock<ILlmProvider>();
        mockLlm.Setup(x => x.ChatAsync(
                It.IsAny<IEnumerable<LlmMessage>>(),
                It.IsAny<IEnumerable<ToolDefinition>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmResponse
            {
                Success = true,
                Content = """[{"step":1,"actionName":"screenshot","description":"截图","toolName":"computer_use","arguments":"{\"action\":\"screenshot\"}","requiresApproval":false}]"""
            });

        var registry = new ToolRegistry();
        var cuOrchestrator = new ComputerUseOrchestrator(mockLlm.Object, _mockEventBus.Object, _mockCuLogger.Object);

        var orchestrator = new AgentOrchestrator(
            mockLlm.Object,
            registry,
            _mockEventBus.Object,
            _mockLogger.Object,
            computerUseOrchestrator: cuOrchestrator);

        var task = new AgentTask
        {
            Title = "计算机操作",
            Input = "截图当前屏幕",
            Type = TaskType.ComputerUse,
            Parameters = new Dictionary<string, object> { ["TaskType"] = TaskType.ComputerUse }
        };

        var result = await orchestrator.ExecuteAsync(task);

        Assert.True(result.Success);
        Assert.Contains("Computer Use", result.Content);
    }

    [Fact]
    public async Task ExecuteAsync_ComputerUseWithoutOrchestrator_ShouldFail()
    {
        var mockLlm = new Mock<ILlmProvider>();
        var registry = new ToolRegistry();

        var orchestrator = new AgentOrchestrator(
            mockLlm.Object,
            registry,
            _mockEventBus.Object,
            _mockLogger.Object,
            computerUseOrchestrator: null);

        var task = new AgentTask
        {
            Title = "计算机操作",
            Input = "截图当前屏幕",
            Type = TaskType.ComputerUse,
            Parameters = new Dictionary<string, object> { ["TaskType"] = TaskType.ComputerUse }
        };

        var result = await orchestrator.ExecuteAsync(task);

        Assert.False(result.Success);
        Assert.Contains("未初始化", result.Error);
    }
}
