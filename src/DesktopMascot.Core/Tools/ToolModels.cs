using DesktopMascot.Core.Enums;

namespace DesktopMascot.Core.Tools;

/// <summary>
/// 工具执行结果（统一的轻量结果类型）
/// </summary>
public class ToolResult
{
    public string ToolCallId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string? Error { get; set; }
}

/// <summary>
/// 工具定义
/// </summary>
public class ToolDefinition
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Version { get; set; } = "1.0.0";
    public string Category { get; set; } = "general";
    public string ParametersSchema { get; set; } = "{}";
    public PermissionLevel RequiredPermission { get; set; } = PermissionLevel.L0_Chat;
    public List<string> Tags { get; set; } = new();
    public bool IsEnabled { get; set; } = true;
    public DateTime RegisteredAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// 工具调用请求
/// </summary>
public class ToolCallRequest
{
    public string ToolName { get; set; } = string.Empty;
    public string Arguments { get; set; } = "{}";
    public string? TaskId { get; set; }
    public string? RequestId { get; set; } = Guid.NewGuid().ToString("N");
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// 工具调用结果
/// </summary>
public class ToolCallResponse
{
    public string RequestId { get; set; } = string.Empty;
    public string ToolName { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string? Result { get; set; }
    public string? Error { get; set; }
    public TimeSpan Duration { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// 工具调用日志
/// </summary>
public class ToolCallLog
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string ToolName { get; set; } = string.Empty;
    public string Arguments { get; set; } = "{}";
    public bool Success { get; set; }
    public string? Result { get; set; }
    public string? Error { get; set; }
    public TimeSpan Duration { get; set; }
    public string? TaskId { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// 工具类别
/// </summary>
public static class ToolCategories
{
    public const string General = "general";
    public const string File = "file";
    public const string Network = "network";
    public const string System = "system";
    public const string Data = "data";
    public const string AI = "ai";
}
