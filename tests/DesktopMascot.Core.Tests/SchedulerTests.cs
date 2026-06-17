using DesktopMascot.Core.Scheduling;
using DesktopMascot.Core.Tools;

namespace DesktopMascot.Core.Tests;

public class AppTaskSchedulerTests : IAsyncLifetime
{
    private readonly ToolRegistry _toolRegistry;
    private readonly AppTaskScheduler _scheduler;

    public AppTaskSchedulerTests()
    {
        _toolRegistry = new ToolRegistry();
        _toolRegistry.Register(new GetCurrentTimeTool());
        _toolRegistry.Register(new CalculatorTool());
        _scheduler = new AppTaskScheduler(_toolRegistry);
    }

    public async Task InitializeAsync()
    {
        await _scheduler.StartAsync();
    }

    public async Task DisposeAsync()
    {
        await _scheduler.StopAsync();
        _scheduler.Dispose();
    }

    [Fact]
    public async Task AddTaskAsync_ShouldAddTask()
    {
        var task = new ScheduledTask
        {
            Name = "测试任务",
            ToolName = "get_current_time",
            Type = ScheduleType.Once,
            ScheduledTime = DateTime.UtcNow.AddMinutes(5)
        };

        var result = await _scheduler.AddTaskAsync(task);

        Assert.NotNull(result);
        Assert.Equal("测试任务", result.Name);
    }

    [Fact]
    public async Task RemoveTaskAsync_ShouldRemoveTask()
    {
        var task = new ScheduledTask
        {
            Name = "待删除任务",
            ToolName = "get_current_time"
        };
        await _scheduler.AddTaskAsync(task);

        var removed = await _scheduler.RemoveTaskAsync(task.Id);

        Assert.True(removed);
        var retrieved = await _scheduler.GetTaskAsync(task.Id);
        Assert.Null(retrieved);
    }

    [Fact]
    public async Task PauseTaskAsync_ShouldPauseTask()
    {
        var task = new ScheduledTask
        {
            Name = "待暂停任务",
            ToolName = "get_current_time",
            IsEnabled = true
        };
        await _scheduler.AddTaskAsync(task);

        await _scheduler.PauseTaskAsync(task.Id);

        var retrieved = await _scheduler.GetTaskAsync(task.Id);
        Assert.False(retrieved!.IsEnabled);
    }

    [Fact]
    public async Task ResumeTaskAsync_ShouldResumeTask()
    {
        var task = new ScheduledTask
        {
            Name = "待恢复任务",
            ToolName = "get_current_time",
            IsEnabled = false
        };
        await _scheduler.AddTaskAsync(task);

        await _scheduler.ResumeTaskAsync(task.Id);

        var retrieved = await _scheduler.GetTaskAsync(task.Id);
        Assert.True(retrieved!.IsEnabled);
    }

    [Fact]
    public async Task RunNowAsync_ShouldExecuteTask()
    {
        var task = new ScheduledTask
        {
            Name = "立即执行任务",
            ToolName = "get_current_time"
        };
        await _scheduler.AddTaskAsync(task);

        var execution = await _scheduler.RunNowAsync(task.Id);

        Assert.NotNull(execution);
        Assert.Equal(ScheduleStatus.Completed, execution.Status);
        Assert.NotNull(execution.Result);
    }

    [Fact]
    public async Task RunNowAsync_NonExisting_ShouldFail()
    {
        var execution = await _scheduler.RunNowAsync("non_existing");

        Assert.Equal(ScheduleStatus.Failed, execution.Status);
        Assert.Contains("不存在", execution.Error);
    }

    [Fact]
    public async Task GetAllTasksAsync_ShouldReturnAllTasks()
    {
        await _scheduler.AddTaskAsync(new ScheduledTask { Name = "任务1", ToolName = "get_current_time" });
        await _scheduler.AddTaskAsync(new ScheduledTask { Name = "任务2", ToolName = "calculator" });

        var tasks = await _scheduler.GetAllTasksAsync();

        Assert.Equal(2, tasks.Count);
    }

    [Fact]
    public async Task GetExecutionsAsync_ShouldReturnExecutions()
    {
        var task = new ScheduledTask
        {
            Name = "执行记录任务",
            ToolName = "get_current_time"
        };
        await _scheduler.AddTaskAsync(task);
        await _scheduler.RunNowAsync(task.Id);

        var executions = await _scheduler.GetExecutionsAsync(task.Id);

        Assert.Single(executions);
    }

    [Fact]
    public async Task GetStatisticsAsync_ShouldReturnStats()
    {
        await _scheduler.AddTaskAsync(new ScheduledTask { Name = "统计任务1", ToolName = "get_current_time" });
        await _scheduler.AddTaskAsync(new ScheduledTask { Name = "统计任务2", ToolName = "calculator" });

        var stats = await _scheduler.GetStatisticsAsync();

        Assert.Equal(2, stats.TotalTasks);
        Assert.Equal(2, stats.ActiveTasks);
    }

    [Fact]
    public async Task ScheduleEventOccurred_ShouldFire()
    {
        var eventFired = false;
        _scheduler.ScheduleEventOccurred += e => eventFired = true;

        await _scheduler.AddTaskAsync(new ScheduledTask
        {
            Name = "事件测试任务",
            ToolName = "get_current_time"
        });

        Assert.True(eventFired);
    }
}

public class ScheduleModelsTests
{
    [Fact]
    public void ScheduledTask_ShouldHaveDefaults()
    {
        var task = new ScheduledTask();

        Assert.False(string.IsNullOrEmpty(task.Id));
        Assert.True(task.IsEnabled);
        Assert.Equal(ScheduleType.Once, task.Type);
    }

    [Fact]
    public void ScheduleExecution_ShouldCalculateDuration()
    {
        var execution = new ScheduleExecution
        {
            StartedAt = DateTime.UtcNow.AddSeconds(-10),
            CompletedAt = DateTime.UtcNow
        };

        Assert.NotNull(execution.Duration);
        Assert.True(execution.Duration!.Value.TotalSeconds > 9);
    }

    [Fact]
    public void ScheduleEvent_ShouldHaveDefaults()
    {
        var evt = new ScheduleEvent
        {
            TaskId = "test",
            EventType = "test_event",
            Message = "test message"
        };

        Assert.Equal("test", evt.TaskId);
        Assert.True(evt.Timestamp <= DateTime.UtcNow);
    }
}
