namespace DesktopMascot.Core.ErrorHandling;

/// <summary>
/// 错误类型
/// </summary>
public enum ErrorType
{
    /// <summary>未知错误</summary>
    Unknown,
    /// <summary>网络错误</summary>
    Network,
    /// <summary>API 错误</summary>
    Api,
    /// <summary>文件操作错误</summary>
    FileOperation,
    /// <summary>权限错误</summary>
    Permission,
    /// <summary>配置错误</summary>
    Configuration,
    /// <summary>插件错误</summary>
    Plugin,
    /// <summary>验证错误</summary>
    Validation,
    /// <summary>超时错误</summary>
    Timeout,
    /// <summary>取消操作</summary>
    OperationCancelled
}

/// <summary>
/// 应用异常
/// </summary>
public class AppException : Exception
{
    public ErrorType ErrorType { get; }
    public string? ErrorCode { get; }
    public Dictionary<string, object> Metadata { get; } = new();
    public bool IsRetryable { get; }

    public AppException(
        string message,
        ErrorType errorType = ErrorType.Unknown,
        string? errorCode = null,
        bool isRetryable = false,
        Exception? innerException = null)
        : base(message, innerException)
    {
        ErrorType = errorType;
        ErrorCode = errorCode;
        IsRetryable = isRetryable;
    }

    public AppException WithMetadata(string key, object value)
    {
        Metadata[key] = value;
        return this;
    }
}

/// <summary>
/// 操作结果
/// </summary>
public class OperationResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public string? ErrorCode { get; set; }
    public ErrorType ErrorType { get; set; }
    public Exception? Exception { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();

    public static OperationResult Ok() => new() { Success = true };
    
    public static OperationResult Fail(string message, ErrorType errorType = ErrorType.Unknown)
        => new() { Success = false, ErrorMessage = message, ErrorType = errorType };
    
    public static OperationResult Fail(Exception ex)
        => new() { Success = false, ErrorMessage = ex.Message, Exception = ex };
}

/// <summary>
/// 操作结果（带返回值）
/// </summary>
public class OperationResult<T>
{
    public bool Success { get; set; }
    public T? Value { get; set; }
    public string? ErrorMessage { get; set; }
    public string? ErrorCode { get; set; }
    public ErrorType ErrorType { get; set; }
    public Exception? Exception { get; set; }

    public static OperationResult<T> Ok(T value) => new() { Success = true, Value = value };
    
    public static OperationResult<T> Fail(string message, ErrorType errorType = ErrorType.Unknown)
        => new() { Success = false, ErrorMessage = message, ErrorType = errorType };
    
    public static OperationResult<T> Fail(Exception ex)
        => new() { Success = false, ErrorMessage = ex.Message, Exception = ex };
}
