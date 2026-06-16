namespace DesktopMascot.Core.Logging;

/// <summary>
/// 日志管理器接口
/// </summary>
public interface ILogger
{
    void Trace(string message, string? source = null, Exception? exception = null);
    void Debug(string message, string? source = null, Exception? exception = null);
    void Information(string message, string? source = null, Exception? exception = null);
    void Warning(string message, string? source = null, Exception? exception = null);
    void Error(string message, string? source = null, Exception? exception = null);
    void Critical(string message, string? source = null, Exception? exception = null);
    
    /// <summary>带结构化数据的日志</summary>
    void Log(LogLevel level, string message, Dictionary<string, object>? properties = null, string? source = null, Exception? exception = null);
    
    Task FlushAsync(CancellationToken ct = default);
}

/// <summary>
/// 日志管理器（增强版：结构化日志 + 分类过滤 + 性能统计）
/// </summary>
public class LogManager : ILogger
{
    private readonly ILogStore _store;
    private readonly LogLevel _minimumLevel;
    private readonly List<LogEntry> _buffer = new();
    private readonly object _bufferLock = new();
    private readonly Timer? _flushTimer;
    private readonly HashSet<string> _enabledSources = new();
    private readonly Dictionary<LogLevel, int> _levelCounts = new();
    private int _totalLogs;
    private const int BufferSize = 100;

    public LogManager(ILogStore store, LogLevel minimumLevel = LogLevel.Information)
    {
        _store = store;
        _minimumLevel = minimumLevel;
        
        foreach (var level in Enum.GetValues<LogLevel>())
        {
            _levelCounts[level] = 0;
        }
        
        _flushTimer = new Timer(async _ => await FlushAsync(), null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
    }

    public void Trace(string message, string? source = null, Exception? exception = null)
        => Log(LogLevel.Trace, message, null, source, exception);

    public void Debug(string message, string? source = null, Exception? exception = null)
        => Log(LogLevel.Debug, message, null, source, exception);

    public void Information(string message, string? source = null, Exception? exception = null)
        => Log(LogLevel.Information, message, null, source, exception);

    public void Warning(string message, string? source = null, Exception? exception = null)
        => Log(LogLevel.Warning, message, null, source, exception);

    public void Error(string message, string? source = null, Exception? exception = null)
        => Log(LogLevel.Error, message, null, source, exception);

    public void Critical(string message, string? source = null, Exception? exception = null)
        => Log(LogLevel.Critical, message, null, source, exception);

    public void Log(LogLevel level, string message, Dictionary<string, object>? properties = null, string? source = null, Exception? exception = null)
    {
        if (level < _minimumLevel)
            return;

        if (_enabledSources.Count > 0 && !string.IsNullOrEmpty(source) && !_enabledSources.Contains(source))
            return;
        
        var entry = new LogEntry
        {
            Level = level,
            Message = message,
            Source = source,
            Exception = exception,
            Properties = properties ?? new Dictionary<string, object>()
        };

        lock (_bufferLock)
        {
            _buffer.Add(entry);
            _levelCounts[level]++;
            _totalLogs++;
            
            if (_buffer.Count >= BufferSize)
            {
                _ = FlushAsync();
            }
        }
    }

    public async Task FlushAsync(CancellationToken ct = default)
    {
        List<LogEntry> entriesToFlush;
        
        lock (_bufferLock)
        {
            if (_buffer.Count == 0)
                return;
            
            entriesToFlush = new List<LogEntry>(_buffer);
            _buffer.Clear();
        }
        
        await _store.WriteBatchAsync(entriesToFlush, ct);
    }

    /// <summary>启用特定来源的日志</summary>
    public void EnableSource(string source)
    {
        lock (_bufferLock)
        {
            _enabledSources.Add(source);
        }
    }

    /// <summary>禁用特定来源的日志</summary>
    public void DisableSource(string source)
    {
        lock (_bufferLock)
        {
            _enabledSources.Remove(source);
        }
    }

    /// <summary>获取日志统计</summary>
    public LogStatistics GetStatistics()
    {
        lock (_bufferLock)
        {
            return new LogStatistics
            {
                TotalCount = _totalLogs,
                TraceCount = _levelCounts.GetValueOrDefault(LogLevel.Trace, 0),
                DebugCount = _levelCounts.GetValueOrDefault(LogLevel.Debug, 0),
                InformationCount = _levelCounts.GetValueOrDefault(LogLevel.Information, 0),
                WarningCount = _levelCounts.GetValueOrDefault(LogLevel.Warning, 0),
                ErrorCount = _levelCounts.GetValueOrDefault(LogLevel.Error, 0),
                CriticalCount = _levelCounts.GetValueOrDefault(LogLevel.Critical, 0)
            };
        }
    }

    public void Dispose()
    {
        _flushTimer?.Dispose();
        FlushAsync().Wait();
    }
}
