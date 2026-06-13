namespace DesktopMascot.Core.Logging;

/// <summary>
/// 日志存储接口
/// </summary>
public interface ILogStore
{
    /// <summary>写入日志</summary>
    Task WriteAsync(LogEntry entry, CancellationToken ct = default);
    
    /// <summary>批量写入</summary>
    Task WriteBatchAsync(IEnumerable<LogEntry> entries, CancellationToken ct = default);
    
    /// <summary>查询日志</summary>
    Task<List<LogEntry>> QueryAsync(LogFilter? filter = null, int limit = 100, CancellationToken ct = default);
    
    /// <summary>获取统计</summary>
    Task<LogStatistics> GetStatisticsAsync(CancellationToken ct = default);
    
    /// <summary>清理旧日志</summary>
    Task<int> CleanupAsync(int keepDays = 7, CancellationToken ct = default);
    
    /// <summary>导出日志</summary>
    Task<string> ExportAsync(LogFilter? filter = null, CancellationToken ct = default);
}

/// <summary>
/// 文件日志存储
/// </summary>
public class FileLogStore : ILogStore
{
    private readonly string _logDirectory;
    private readonly object _lock = new();
    private const string LogFileName = "app_log.jsonl";

    public FileLogStore(string? logDirectory = null)
    {
        _logDirectory = logDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "DesktopMascot",
            "logs");
        Directory.CreateDirectory(_logDirectory);
    }

    private string GetLogFilePath() => Path.Combine(_logDirectory, LogFileName);

    public async Task WriteAsync(LogEntry entry, CancellationToken ct = default)
    {
        var filePath = GetLogFilePath();
        var json = System.Text.Json.JsonSerializer.Serialize(entry);
        
        lock (_lock)
        {
            File.AppendAllText(filePath, json + Environment.NewLine);
        }
        
        await Task.CompletedTask;
    }

    public async Task WriteBatchAsync(IEnumerable<LogEntry> entries, CancellationToken ct = default)
    {
        var filePath = GetLogFilePath();
        var lines = entries.Select(e => System.Text.Json.JsonSerializer.Serialize(e));
        
        lock (_lock)
        {
            File.AppendAllLines(filePath, lines);
        }
        
        await Task.CompletedTask;
    }

    public Task<List<LogEntry>> QueryAsync(LogFilter? filter = null, int limit = 100, CancellationToken ct = default)
    {
        var filePath = GetLogFilePath();

        if (!File.Exists(filePath))
            return Task.FromResult(new List<LogEntry>());

        List<LogEntry> entries;

        lock (_lock)
        {
            entries = File.ReadAllLines(filePath)
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .Select(line =>
                {
                    try
                    {
                        return System.Text.Json.JsonSerializer.Deserialize<LogEntry>(line);
                    }
                    catch
                    {
                        return null;
                    }
                })
                .Where(e => e != null)
                .Cast<LogEntry>()
                .ToList();
        }

        // 应用过滤器
        if (filter != null)
        {
            if (filter.MinLevel.HasValue)
                entries = entries.Where(e => e.Level >= filter.MinLevel.Value).ToList();

            if (filter.MaxLevel.HasValue)
                entries = entries.Where(e => e.Level <= filter.MaxLevel.Value).ToList();

            if (!string.IsNullOrEmpty(filter.SourceContains))
                entries = entries.Where(e => e.Source?.Contains(filter.SourceContains) == true).ToList();

            if (!string.IsNullOrEmpty(filter.MessageContains))
                entries = entries.Where(e => e.Message.Contains(filter.MessageContains)).ToList();

            if (filter.After.HasValue)
                entries = entries.Where(e => e.Timestamp >= filter.After.Value).ToList();

            if (filter.Before.HasValue)
                entries = entries.Where(e => e.Timestamp <= filter.Before.Value).ToList();
        }

        return Task.FromResult(entries
            .OrderByDescending(e => e.Timestamp)
            .Take(limit)
            .ToList());
    }

    public async Task<LogStatistics> GetStatisticsAsync(CancellationToken ct = default)
    {
        var entries = await QueryAsync(limit: int.MaxValue, ct: ct);
        
        return new LogStatistics
        {
            TotalCount = entries.Count,
            TraceCount = entries.Count(e => e.Level == LogLevel.Trace),
            DebugCount = entries.Count(e => e.Level == LogLevel.Debug),
            InformationCount = entries.Count(e => e.Level == LogLevel.Information),
            WarningCount = entries.Count(e => e.Level == LogLevel.Warning),
            ErrorCount = entries.Count(e => e.Level == LogLevel.Error),
            CriticalCount = entries.Count(e => e.Level == LogLevel.Critical),
            OldestEntry = entries.Any() ? entries.Min(e => e.Timestamp) : null,
            NewestEntry = entries.Any() ? entries.Max(e => e.Timestamp) : null
        };
    }

    public async Task<int> CleanupAsync(int keepDays = 7, CancellationToken ct = default)
    {
        var cutoff = DateTime.UtcNow.AddDays(-keepDays);
        var entries = await QueryAsync(limit: int.MaxValue, ct: ct);
        var toKeep = entries.Where(e => e.Timestamp >= cutoff).ToList();
        var removed = entries.Count - toKeep.Count;
        
        if (removed > 0)
        {
            var filePath = GetLogFilePath();
            var lines = toKeep.Select(e => System.Text.Json.JsonSerializer.Serialize(e));
            
            lock (_lock)
            {
                File.WriteAllLines(filePath, lines);
            }
        }
        
        return removed;
    }

    public async Task<string> ExportAsync(LogFilter? filter = null, CancellationToken ct = default)
    {
        var entries = await QueryAsync(filter, limit: int.MaxValue, ct: ct);
        return System.Text.Json.JsonSerializer.Serialize(entries, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
    }
}
