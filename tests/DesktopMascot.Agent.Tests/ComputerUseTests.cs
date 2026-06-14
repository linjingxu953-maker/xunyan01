using DesktopMascot.Agent.Engines;
using DesktopMascot.Agent.Models;
using DesktopMascot.Agent.Providers;
using DesktopMascot.Core.Interfaces;
using Microsoft.Extensions.Logging;
using Moq;

namespace DesktopMascot.Agent.Tests;

public class ComputerUseTests
{
    [Fact]
    public void ComputerUseEvent_ShouldHaveAllTypes()
    {
        var types = Enum.GetValues<ComputerUseEventType>();
        Assert.Equal(9, types.Length);
        Assert.Contains(ComputerUseEventType.ComputerUseStarted, types);
        Assert.Contains(ComputerUseEventType.ComputerUseCompleted, types);
        Assert.Contains(ComputerUseEventType.WaitingUserApproval, types);
    }

    [Fact]
    public void PlannedAction_ShouldDefaultToPending()
    {
        var action = new PlannedAction();
        Assert.Equal(ActionStatus.Pending, action.Status);
    }

    [Fact]
    public void ComputerUseSession_ShouldTrackState()
    {
        var session = new ComputerUseSession();
        Assert.False(session.IsActive);
        Assert.False(session.IsPaused);
        Assert.False(session.UserHasTakeover);
        Assert.Equal("idle", session.CurrentState);
    }

    [Fact]
    public void ApprovalRequest_ShouldHaveDefaults()
    {
        var request = new ApprovalRequest();
        Assert.NotEmpty(request.RequestId);
        Assert.Equal("medium", request.RiskLevel);
    }

    [Fact]
    public async Task ComputerUseOrchestrator_PauseResume_ShouldWork()
    {
        var mockLlm = new Mock<ILlmProvider>();
        var mockEventBus = new Mock<ITaskEventBus>();
        var mockLogger = new Mock<ILogger<ComputerUseOrchestrator>>();

        var orchestrator = new ComputerUseOrchestrator(
            mockLlm.Object, mockEventBus.Object, mockLogger.Object);

        Assert.False(orchestrator.Session.IsPaused);

        orchestrator.Pause();
        Assert.True(orchestrator.Session.IsPaused);

        orchestrator.Resume();
        Assert.False(orchestrator.Session.IsPaused);
    }

    [Fact]
    public async Task ComputerUseOrchestrator_Takeover_ShouldWork()
    {
        var mockLlm = new Mock<ILlmProvider>();
        var mockEventBus = new Mock<ITaskEventBus>();
        var mockLogger = new Mock<ILogger<ComputerUseOrchestrator>>();

        var orchestrator = new ComputerUseOrchestrator(
            mockLlm.Object, mockEventBus.Object, mockLogger.Object);

        Assert.False(orchestrator.Session.UserHasTakeover);

        orchestrator.Takeover();
        Assert.True(orchestrator.Session.UserHasTakeover);
    }

    [Fact]
    public async Task ComputerUseOrchestrator_Execute_ShouldEmitEvents()
    {
        var mockLlm = new Mock<ILlmProvider>();
        var mockEventBus = new Mock<ITaskEventBus>();
        var mockLogger = new Mock<ILogger<ComputerUseOrchestrator>>();

        mockLlm.Setup(x => x.ChatAsync(
                It.IsAny<IEnumerable<LlmMessage>>(),
                It.IsAny<IEnumerable<ToolDefinition>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmResponse
            {
                Success = true,
                Content = """[{"step":1,"actionName":"screenshot","description":"截图","toolName":"computer_use","arguments":"{\"action\":\"screenshot\"}","requiresApproval":false}]"""
            });

        var orchestrator = new ComputerUseOrchestrator(
            mockLlm.Object, mockEventBus.Object, mockLogger.Object);

        var events = new List<ComputerUseEvent>();
        orchestrator.ComputerUseEventOccurred += (_, e) => events.Add(e);

        var result = await orchestrator.ExecuteAsync("测试");

        Assert.True(result.Success);
        Assert.Contains(events, e => e.EventType == ComputerUseEventType.ComputerUseStarted);
        Assert.Contains(events, e => e.EventType == ComputerUseEventType.ComputerUseCompleted);
    }
}
