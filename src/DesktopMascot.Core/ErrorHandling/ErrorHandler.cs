namespace DesktopMascot.Core.ErrorHandling;

/// <summary>
/// 重试策略
/// </summary>
public class RetryPolicy
{
    public int MaxRetries { get; set; } = 3;
    public TimeSpan InitialDelay { get; set; } = TimeSpan.FromSeconds(1);
    public TimeSpan MaxDelay { get; set; } = TimeSpan.FromSeconds(30);
    public double BackoffMultiplier { get; set; } = 2.0;
    public Func<Exception, bool>? ShouldRetry { get; set; }

    public static RetryPolicy Default => new();
    
    public static RetryPolicy NoRetry => new() { MaxRetries = 0 };
    
    public static RetryPolicy Aggressive => new()
    {
        MaxRetries = 5,
        InitialDelay = TimeSpan.FromMilliseconds(500),
        MaxDelay = TimeSpan.FromSeconds(10)
    };
}

/// <summary>
/// 错误处理器
/// </summary>
public class ErrorHandler
{
    private readonly List<Action<ErrorContext>> _errorHandlers = new();
    private readonly object _lock = new();

    /// <summary>记录错误</summary>
    public event Action<ErrorContext>? ErrorOccurred;

    /// <summary>添加全局错误处理器</summary>
    public void AddHandler(Action<ErrorContext> handler)
    {
        lock (_lock)
        {
            _errorHandlers.Add(handler);
        }
    }

    /// <summary>处理异常</summary>
    public ErrorContext HandleException(Exception exception, string? source = null, string? taskId = null)
    {
        var context = new ErrorContext
        {
            Exception = exception,
            Source = source,
            TaskId = taskId,
            Timestamp = DateTime.UtcNow,
            ErrorType = ClassifyError(exception)
        };

        // 调用全局处理器
        lock (_lock)
        {
            foreach (var handler in _errorHandlers)
            {
                try
                {
                    handler(context);
                }
                catch
                {
                    // 避免处理器异常影响主流程
                }
            }
        }

        // 触发事件
        ErrorOccurred?.Invoke(context);

        return context;
    }

    /// <summary>分类异常类型</summary>
    public static ErrorType ClassifyError(Exception exception)
    {
        return exception switch
        {
            OperationCanceledException => ErrorType.OperationCancelled,
            TimeoutException => ErrorType.Timeout,
            UnauthorizedAccessException => ErrorType.Permission,
            System.IO.FileNotFoundException => ErrorType.FileOperation,
            System.IO.IOException => ErrorType.FileOperation,
            System.Net.Http.HttpRequestException => ErrorType.Network,
            System.Net.Sockets.SocketException => ErrorType.Network,
            System.Text.Json.JsonException => ErrorType.Validation,
            AppException appEx => appEx.ErrorType,
            _ => ErrorType.Unknown
        };
    }
}

/// <summary>
/// 错误上下文
/// </summary>
public class ErrorContext
{
    public Exception Exception { get; set; } = null!;
    public string? Source { get; set; }
    public string? TaskId { get; set; }
    public ErrorType ErrorType { get; set; }
    public DateTime Timestamp { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
}
