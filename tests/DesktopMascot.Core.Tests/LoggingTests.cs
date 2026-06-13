using DesktopMascot.Core.Logging;

namespace DesktopMascot.Core.Tests;

public class LogManagerTests : IDisposable
{
    private readonly FileLogStore _store;
    private readonly LogManager _manager;
    private readonly string _testDir;

    public LogManagerTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"log_test_{Guid.NewGuid():N}");
        _store = new FileLogStore(_testDir);
        _manager = new LogManager(_store, LogLevel.Trace);
    }

    public void Dispose()
    {
        _manager.Dispose();
        if (Directory.Exists(_testDir))
            Directory.Delete(_testDir, true);
    }

    [Fact]
    public async Task Information_ShouldWriteLog()
    {
        _manager.Information("测试消息", "TestSource");
        await _manager.FlushAsync();

        var entries = await _store.QueryAsync();

        Assert.Single(entries);
        Assert.Equal(LogLevel.Information, entries[0].Level);
        Assert.Equal("测试消息", entries[0].Message);
        Assert.Equal("TestSource", entries[0].Source);
    }

    [Fact]
    public async Task Error_ShouldWriteLog()
    {
        var exception = new InvalidOperationException("测试异常");
        _manager.Error("错误消息", "TestSource", exception);
        await _manager.FlushAsync();

        var entries = await _store.QueryAsync();

        Assert.Single(entries);
        Assert.Equal(LogLevel.Error, entries[0].Level);
        Assert.NotNull(entries[0].Exception);
    }

    [Fact]
    public async Task MinimumLevel_ShouldFilterLogs()
    {
        var manager = new LogManager(_store, LogLevel.Warning);
        
        manager.Debug("调试消息");
        manager.Information("信息消息");
        manager.Warning("警告消息");
        await manager.FlushAsync();

        var entries = await _store.QueryAsync();

        Assert.Single(entries);
        Assert.Equal(LogLevel.Warning, entries[0].Level);
        
        manager.Dispose();
    }

    [Fact]
    public async Task FlushAsync_ShouldWriteBufferedLogs()
    {
        _manager.Information("消息1");
        _manager.Information("消息2");
        _manager.Information("消息3");
        
        await _manager.FlushAsync();

        var entries = await _store.QueryAsync();

        Assert.Equal(3, entries.Count);
    }
}

public class FileLogStoreTests : IDisposable
{
    private readonly FileLogStore _store;
    private readonly string _testDir;

    public FileLogStoreTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"log_store_test_{Guid.NewGuid():N}");
        _store = new FileLogStore(_testDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
            Directory.Delete(_testDir, true);
    }

    [Fact]
    public async Task WriteAsync_ShouldPersistEntry()
    {
        var entry = new LogEntry
        {
            Level = LogLevel.Information,
            Message = "测试消息"
        };

        await _store.WriteAsync(entry);

        var entries = await _store.QueryAsync();

        Assert.Single(entries);
        Assert.Equal("测试消息", entries[0].Message);
    }

    [Fact]
    public async Task QueryAsync_WithFilter_ShouldFilterEntries()
    {
        await _store.WriteAsync(new LogEntry { Level = LogLevel.Information, Message = "信息" });
        await _store.WriteAsync(new LogEntry { Level = LogLevel.Warning, Message = "警告" });
        await _store.WriteAsync(new LogEntry { Level = LogLevel.Error, Message = "错误" });

        var entries = await _store.QueryAsync(new LogFilter { MinLevel = LogLevel.Warning });

        Assert.Equal(2, entries.Count);
    }

    [Fact]
    public async Task GetStatisticsAsync_ShouldReturnCorrectStats()
    {
        await _store.WriteAsync(new LogEntry { Level = LogLevel.Information });
        await _store.WriteAsync(new LogEntry { Level = LogLevel.Warning });
        await _store.WriteAsync(new LogEntry { Level = LogLevel.Error });

        var stats = await _store.GetStatisticsAsync();

        Assert.Equal(3, stats.TotalCount);
        Assert.Equal(1, stats.InformationCount);
        Assert.Equal(1, stats.WarningCount);
        Assert.Equal(1, stats.ErrorCount);
    }

    [Fact]
    public async Task CleanupAsync_ShouldRemoveOldEntries()
    {
        await _store.WriteAsync(new LogEntry
        {
            Level = LogLevel.Information,
            Timestamp = DateTime.UtcNow.AddDays(-30)
        });
        await _store.WriteAsync(new LogEntry
        {
            Level = LogLevel.Information,
            Timestamp = DateTime.UtcNow
        });

        var removed = await _store.CleanupAsync(7);

        Assert.Equal(1, removed);
    }

    [Fact]
    public async Task ExportAsync_ShouldReturnJson()
    {
        await _store.WriteAsync(new LogEntry { Level = LogLevel.Information, Message = "test message" });

        var json = await _store.ExportAsync();

        Assert.NotEmpty(json);
        Assert.Contains("test message", json);
    }
}
