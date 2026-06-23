using System.Collections.Concurrent;
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

    [Fact]
    public async Task ComputerUseOrchestrator_PublishedTaskEvents_ShouldCarryStructuredMetadataForUiPanel()
    {
        var mockLlm = new Mock<ILlmProvider>();
        var mockEventBus = new Mock<ITaskEventBus>();
        var mockLogger = new Mock<ILogger<ComputerUseOrchestrator>>();
        var publishedEvents = new ConcurrentQueue<Core.Models.TaskEvent>();
        var permissionRequested = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        mockLlm.Setup(x => x.ChatAsync(
                It.IsAny<IEnumerable<LlmMessage>>(),
                It.IsAny<IEnumerable<ToolDefinition>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmResponse
            {
                Success = true,
                Content = """[{"step":1,"actionName":"screenshot","description":"截取当前屏幕作为执行证据","toolName":"computer_use","arguments":"{\"action\":\"screenshot\"}","requiresApproval":true}]"""
            });

        mockEventBus.Setup(x => x.Publish(It.IsAny<Core.Models.TaskEvent>()))
            .Callback<Core.Models.TaskEvent>(e =>
            {
                publishedEvents.Enqueue(e);
                if (e.EventType == Core.Enums.TaskEventType.PermissionRequested)
                {
                    permissionRequested.TrySetResult();
                }
            });

        var orchestrator = new ComputerUseOrchestrator(
            mockLlm.Object, mockEventBus.Object, mockLogger.Object);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        _ = Task.Run(async () =>
        {
            await permissionRequested.Task.WaitAsync(cts.Token);
            orchestrator.ApproveCurrentAction();
        }, cts.Token);

        var result = await orchestrator.ExecuteAsync("截取当前屏幕作为执行证据", cts.Token);

        Assert.True(result.Success);
        var planningEvent = Assert.Single(publishedEvents.Where(e =>
            e.Metadata?.TryGetValue("computerUseEventType", out var type) == true &&
            string.Equals(type?.ToString(), nameof(ComputerUseEventType.ActionPlanned), StringComparison.Ordinal) &&
            string.Equals(e.Metadata?["action"]?.ToString(), "plan_actions", StringComparison.Ordinal)));
        Assert.Equal("plan_actions", planningEvent.Metadata?["action"]);
        Assert.True(planningEvent.Metadata?.ContainsKey("screenshotPath"));

        var approvalEvent = Assert.Single(publishedEvents.Where(e =>
            e.EventType == Core.Enums.TaskEventType.PermissionRequested));
        Assert.Equal("screenshot", approvalEvent.Metadata?["action"]);
        Assert.Equal("computer_use", approvalEvent.Metadata?["toolName"]);
        Assert.Equal("waiting_approval", approvalEvent.Metadata?["status"]);
        Assert.Equal("ComputerUse", approvalEvent.Metadata?["permissionType"]);
        Assert.Contains("截取当前屏幕", approvalEvent.Metadata?["reason"]?.ToString());
    }
}
