using DesktopMascot.Core.Enums;
using DesktopMascot.Core.Storage;

namespace DesktopMascot.Core.Tests;

public class SqliteTaskHistoryStoreTests
{
    private async Task<SqliteTaskHistoryStore> CreateStoreAsync()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"sqlite_test_{Guid.NewGuid():N}.db");
        var store = new SqliteTaskHistoryStore(dbPath);

        // 初始化数据库（用 Task.Run 避免同步上下文死锁）
        await Task.Run(async () =>
        {
            using var context = new DatabaseContext($"Data Source={dbPath}");
            var migrator = new DatabaseMigrator(context);
            await migrator.MigrateAsync();
        });

        return store;
    }

    [Fact]
    public async Task SaveAndGetTask_ShouldWork()
    {
        var store = await CreateStoreAsync();
        var record = new TaskHistoryRecord
        {
            Id = "task-1",
            Title = "测试任务",
            Input = "测试输入",
            Type = TaskType.Chat,
            Status = AppTaskStatus.Created
        };

        await store.SaveTaskAsync(record);
        var result = await store.GetTaskAsync("task-1");

        Assert.NotNull(result);
        Assert.Equal("测试任务", result!.Title);
    }

    [Fact]
    public async Task DeleteTask_ShouldWork()
    {
        var store = await CreateStoreAsync();
        var record = new TaskHistoryRecord
        {
            Id = "task-2",
            Title = "删除测试"
        };
        await store.SaveTaskAsync(record);

        var deleted = await store.DeleteTaskAsync("task-2");

        Assert.True(deleted);
        var result = await store.GetTaskAsync("task-2");
        Assert.Null(result);
    }

    [Fact]
    public async Task SearchTasks_ShouldWork()
    {
        var store = await CreateStoreAsync();
        await store.SaveTaskAsync(new TaskHistoryRecord
        {
            Id = "task-3",
            Title = "网页总结",
            Type = TaskType.SummarizePage
        });

        var results = await store.SearchTasksAsync("总结");

        Assert.Single(results.Records);
    }

    [Fact]
    public async Task GetStatistics_ShouldWork()
    {
        var store = await CreateStoreAsync();
        await store.SaveTaskAsync(new TaskHistoryRecord
        {
            Id = "s1",
            Status = AppTaskStatus.Completed
        });

        var stats = await store.GetStatisticsAsync();

        Assert.Equal(1, stats.TotalTasks);
        Assert.Equal(1, stats.CompletedTasks);
    }

    [Fact]
    public async Task Cleanup_ShouldWork()
    {
        var store = await CreateStoreAsync();
        await store.SaveTaskAsync(new TaskHistoryRecord
        {
            Id = "old",
            CreatedAt = DateTime.UtcNow.AddDays(-60)
        });
        await store.SaveTaskAsync(new TaskHistoryRecord
        {
            Id = "new",
            CreatedAt = DateTime.UtcNow
        });

        var removed = await store.CleanupAsync(30);

        Assert.Equal(1, removed);
    }
}
