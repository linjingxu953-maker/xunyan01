using DesktopMascot.Core.Enums;
using DesktopMascot.Core.Storage;

namespace DesktopMascot.Core.Tests;

public class TaskHistoryStoreTests : IDisposable
{
    private readonly FileTaskHistoryStore _store;
    private readonly string _testDir;

    public TaskHistoryStoreTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"history_test_{Guid.NewGuid():N}");
        _store = new FileTaskHistoryStore(_testDir);
    }

    void IDisposable.Dispose()
    {
        if (Directory.Exists(_testDir))
            Directory.Delete(_testDir, true);
    }

    [Fact]
    public async Task SaveTaskAsync_ShouldPersistRecord()
    {
        var record = new TaskHistoryRecord
        {
            Id = "task-1",
            Title = "测试任务",
            Input = "测试输入",
            Type = TaskType.Chat,
            Status = AppTaskStatus.Created
        };

        var saved = await _store.SaveTaskAsync(record);

        Assert.NotNull(saved);
        Assert.Equal("task-1", saved.Id);
    }

    [Fact]
    public async Task GetTaskAsync_ShouldReturnRecord()
    {
        var record = new TaskHistoryRecord
        {
            Id = "task-2",
            Title = "获取测试",
            Input = "input",
            Type = TaskType.Chat
        };
        await _store.SaveTaskAsync(record);

        var result = await _store.GetTaskAsync("task-2");

        Assert.NotNull(result);
        Assert.Equal("获取测试", result!.Title);
    }

    [Fact]
    public async Task UpdateTaskAsync_ShouldUpdateRecord()
    {
        var record = new TaskHistoryRecord
        {
            Id = "task-3",
            Title = "原始标题",
            Status = AppTaskStatus.Created
        };
        await _store.SaveTaskAsync(record);

        record.Title = "更新后标题";
        record.Status = AppTaskStatus.Completed;
        await _store.UpdateTaskAsync(record);

        var result = await _store.GetTaskAsync("task-3");
        Assert.Equal("更新后标题", result!.Title);
        Assert.Equal(AppTaskStatus.Completed, result.Status);
    }

    [Fact]
    public async Task DeleteTaskAsync_ShouldRemoveRecord()
    {
        var record = new TaskHistoryRecord
        {
            Id = "task-4",
            Title = "删除测试"
        };
        await _store.SaveTaskAsync(record);

        var deleted = await _store.DeleteTaskAsync("task-4");

        Assert.True(deleted);
        var result = await _store.GetTaskAsync("task-4");
        Assert.Null(result);
    }

    [Fact]
    public async Task SearchTasksAsync_ShouldFindTasks()
    {
        await _store.SaveTaskAsync(new TaskHistoryRecord
        {
            Id = "task-5",
            Title = "网页总结任务",
            Input = "总结这个页面"
        });
        await _store.SaveTaskAsync(new TaskHistoryRecord
        {
            Id = "task-6",
            Title = "代码分析任务",
            Input = "分析这段代码"
        });

        var results = await _store.SearchTasksAsync("总结");

        Assert.Single(results.Records);
        Assert.Contains("网页总结", results.Records[0].Title);
    }

    [Fact]
    public async Task GetRecentTasksAsync_ShouldReturnLatest()
    {
        for (int i = 0; i < 5; i++)
        {
            await _store.SaveTaskAsync(new TaskHistoryRecord
            {
                Id = $"task-{i}",
                Title = $"任务 {i}",
                CreatedAt = DateTime.UtcNow.AddMinutes(-i)
            });
        }

        var recent = await _store.GetRecentTasksAsync(3);

        Assert.Equal(3, recent.Count);
    }

    [Fact]
    public async Task SaveToolCallAsync_ShouldPersistRecord()
    {
        var record = new ToolCallRecord
        {
            TaskId = "task-7",
            ToolName = "get_weather",
            Arguments = """{"city": "北京"}""",
            Success = true,
            Result = "晴天"
        };

        var saved = await _store.SaveToolCallAsync(record);

        Assert.NotNull(saved);
        Assert.Equal("get_weather", saved.ToolName);
    }

    [Fact]
    public async Task GetToolCallsAsync_ShouldReturnToolCalls()
    {
        await _store.SaveToolCallAsync(new ToolCallRecord
        {
            TaskId = "task-8",
            ToolName = "tool1"
        });
        await _store.SaveToolCallAsync(new ToolCallRecord
        {
            TaskId = "task-8",
            ToolName = "tool2"
        });
        await _store.SaveToolCallAsync(new ToolCallRecord
        {
            TaskId = "task-9",
            ToolName = "tool3"
        });

        var calls = await _store.GetToolCallsAsync("task-8");

        Assert.Equal(2, calls.Count);
    }

    [Fact]
    public async Task GetStatisticsAsync_ShouldReturnCorrectStats()
    {
        await _store.SaveTaskAsync(new TaskHistoryRecord
        {
            Id = "s1",
            Status = AppTaskStatus.Completed,
            Type = TaskType.Chat
        });
        await _store.SaveTaskAsync(new TaskHistoryRecord
        {
            Id = "s2",
            Status = AppTaskStatus.Failed
        });

        var stats = await _store.GetStatisticsAsync();

        Assert.Equal(2, stats.TotalTasks);
        Assert.Equal(1, stats.CompletedTasks);
        Assert.Equal(1, stats.FailedTasks);
    }

    [Fact]
    public async Task CleanupAsync_ShouldRemoveOldRecords()
    {
        await _store.SaveTaskAsync(new TaskHistoryRecord
        {
            Id = "old",
            CreatedAt = DateTime.UtcNow.AddDays(-60)
        });
        await _store.SaveTaskAsync(new TaskHistoryRecord
        {
            Id = "new",
            CreatedAt = DateTime.UtcNow
        });

        var removed = await _store.CleanupAsync(30);

        Assert.Equal(1, removed);
        var remaining = await _store.GetRecentTasksAsync(100);
        Assert.Single(remaining);
    }
}

public class TaskHistoryManagerTests : IDisposable
{
    private readonly FileTaskHistoryStore _store;
    private readonly TaskHistoryManager _manager;
    private readonly string _testDir;

    public TaskHistoryManagerTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"history_manager_test_{Guid.NewGuid():N}");
        _store = new FileTaskHistoryStore(_testDir);
        _manager = new TaskHistoryManager(_store);
    }

    void IDisposable.Dispose()
    {
        if (Directory.Exists(_testDir))
            Directory.Delete(_testDir, true);
    }

    [Fact]
    public async Task CreateTaskAsync_ShouldCreateRecord()
    {
        var record = await _manager.CreateTaskAsync("测试", "输入", TaskType.Chat);

        Assert.NotNull(record);
        Assert.Equal("测试", record.Title);
        Assert.Equal(AppTaskStatus.Created, record.Status);
    }

    [Fact]
    public async Task StartTaskAsync_ShouldUpdateStatus()
    {
        var record = await _manager.CreateTaskAsync("测试", "输入", TaskType.Chat);

        var started = await _manager.StartTaskAsync(record.Id);

        Assert.NotNull(started);
        Assert.Equal(AppTaskStatus.Running, started!.Status);
    }

    [Fact]
    public async Task CompleteTaskAsync_ShouldMarkCompleted()
    {
        var record = await _manager.CreateTaskAsync("测试", "输入", TaskType.Chat);
        await _manager.StartTaskAsync(record.Id);

        var completed = await _manager.CompleteTaskAsync(record.Id, "完成结果");

        Assert.NotNull(completed);
        Assert.Equal(AppTaskStatus.Completed, completed!.Status);
        Assert.Equal("完成结果", completed.Result);
        Assert.NotNull(completed.CompletedAt);
    }

    [Fact]
    public async Task FailTaskAsync_ShouldMarkFailed()
    {
        var record = await _manager.CreateTaskAsync("测试", "输入", TaskType.Chat);

        var failed = await _manager.FailTaskAsync(record.Id, "错误信息");

        Assert.NotNull(failed);
        Assert.Equal(AppTaskStatus.Failed, failed!.Status);
        Assert.Equal("错误信息", failed.Error);
    }

    [Fact]
    public async Task AddEventAsync_ShouldAddEvent()
    {
        var record = await _manager.CreateTaskAsync("测试", "输入", TaskType.Chat);

        var updated = await _manager.AddEventAsync(record.Id, "Working", "执行中", 50);

        Assert.NotNull(updated);
        Assert.Single(updated!.Events);
        Assert.Equal("Working", updated.Events[0].State);
    }

    [Fact]
    public async Task LogToolCallAsync_ShouldRecordToolCall()
    {
        var record = await _manager.CreateTaskAsync("测试", "输入", TaskType.Chat);

        var toolCall = await _manager.LogToolCallAsync(
            record.Id,
            "get_weather",
            """{"city": "北京"}""",
            "晴天",
            true);

        Assert.NotNull(toolCall);
        Assert.Equal("get_weather", toolCall.ToolName);
        Assert.True(toolCall.Success);
    }
}
