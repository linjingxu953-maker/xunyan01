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
    
    Task FlushAsync(CancellationToken ct = default);
}

/// <summary>
/// 日志管理器
/// </summary>
public class LogManager : ILogger
{
    private readonly ILogStore _store;
    private readonly LogLevel _minimumLevel;
    private readonly List<LogEntry> _buffer = new();
    private readonly object _bufferLock = new();
    private readonly Timer? _flushTimer;
    private const int BufferSize = 100;

    public LogManager(ILogStore store, LogLevel minimumLevel = LogLevel.Information)
    {
        _store = store;
        _minimumLevel = minimumLevel;
        
        // 每 30 秒自动刷新
        _flushTimer = new Timer(async _ => await FlushAsync(), null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
    }

    public void Trace(string message, string? source = null, Exception? exception = null)
    {
        Log(LogLevel.Trace, message, source, exception);
    }

    public void Debug(string message, string? source = null, Exception? exception = null)
    {
        Log(LogLevel.Debug, message, source, exception);
    }

    public void Information(string message, string? source = null, Exception? exception = null)
    {
        Log(LogLevel.Information, message, source, exception);
    }

    public void Warning(string message, string? source = null, Exception? exception = null)
    {
        Log(LogLevel.Warning, message, source, exception);
    }

    public void Error(string message, string? source = null, Exception? exception = null)
    {
        Log(LogLevel.Error, message, source, exception);
    }

    public void Critical(string message, string? source = null, Exception? exception = null)
    {
        Log(LogLevel.Critical, message, source, exception);
    }

    private void Log(LogLevel level, string message, string? source, Exception? exception)
    {
        if (level < _minimumLevel)
            return;
        
        var entry = new LogEntry
        {
            Level = level,
            Message = message,
            Source = source,
            Exception = exception
        };
        
        lock (_bufferLock)
        {
            _buffer.Add(entry);
            
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

    public void Dispose()
    {
        _flushTimer?.Dispose();
        FlushAsync().Wait();
    }
}
