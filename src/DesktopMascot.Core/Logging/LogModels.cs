namespace DesktopMascot.Core.Logging;

/// <summary>
/// 日志级别
/// </summary>
public enum LogLevel
{
    Trace = 0,
    Debug = 1,
    Information = 2,
    Warning = 3,
    Error = 4,
    Critical = 5,
    None = 6
}

/// <summary>
/// 日志条目
/// </summary>
public class LogEntry
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public LogLevel Level { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? Source { get; set; }
    public string? TaskId { get; set; }
    public Exception? Exception { get; set; }
    public Dictionary<string, object> Properties { get; set; } = new();
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// 日志过滤器
/// </summary>
public class LogFilter
{
    public LogLevel? MinLevel { get; set; }
    public LogLevel? MaxLevel { get; set; }
    public string? SourceContains { get; set; }
    public string? MessageContains { get; set; }
    public DateTime? After { get; set; }
    public DateTime? Before { get; set; }
}

/// <summary>
/// 日志统计
/// </summary>
public class LogStatistics
{
    public int TotalCount { get; set; }
    public int TraceCount { get; set; }
    public int DebugCount { get; set; }
    public int InformationCount { get; set; }
    public int WarningCount { get; set; }
    public int ErrorCount { get; set; }
    public int CriticalCount { get; set; }
    public DateTime? OldestEntry { get; set; }
    public DateTime? NewestEntry { get; set; }
}
