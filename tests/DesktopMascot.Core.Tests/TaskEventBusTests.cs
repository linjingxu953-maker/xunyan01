using DesktopMascot.Core.Enums;
using DesktopMascot.Core.Interfaces;
using DesktopMascot.Core.Models;
using DesktopMascot.Core.Services;

namespace DesktopMascot.Core.Tests;

public class TaskEventBusTests
{
    [Fact]
    public void Publish_ShouldRaiseEvent()
    {
        // Arrange
        var bus = new TaskEventBus();
        TaskEvent? receivedEvent = null;
        bus.TaskEventPublished += (_, e) => receivedEvent = e;

        var testEvent = new TaskEvent
        {
            TaskId = "test-1",
            State = MascotState.Working,
            Message = "测试事件"
        };

        // Act
        bus.Publish(testEvent);

        // Assert
        Assert.NotNull(receivedEvent);
        Assert.Equal("test-1", receivedEvent!.TaskId);
        Assert.Equal(MascotState.Working, receivedEvent.State);
        Assert.Equal("测试事件", receivedEvent.Message);
    }

    [Fact]
    public void Publish_MultipleSubscribers_ShouldNotifyAll()
    {
        // Arrange
        var bus = new TaskEventBus();
        int callCount = 0;
        bus.TaskEventPublished += (_, _) => callCount++;
        bus.TaskEventPublished += (_, _) => callCount++;

        // Act
        bus.Publish(new TaskEvent { TaskId = "test-2" });

        // Assert
        Assert.Equal(2, callCount);
    }
}
