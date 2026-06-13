using DesktopMascot.Core.Enums;

namespace DesktopMascot.Core.Storage;

/// <summary>
/// 任务历史记录
/// </summary>
public class TaskHistoryRecord
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Input { get; set; } = string.Empty;
    public TaskType Type { get; set; }
    public AppTaskStatus Status { get; set; }
    public string? Result { get; set; }
    public string? Error { get; set; }
    public List<TaskEventRecord> Events { get; set; } = new();
    public List<ToolCallRecord> ToolCalls { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    public TimeSpan? Duration => CompletedAt.HasValue ? CompletedAt.Value - CreatedAt : null;
}

/// <summary>
/// 任务事件记录
/// </summary>
public class TaskEventRecord
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string TaskId { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public int Progress { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// 工具调用记录
/// </summary>
public class ToolCallRecord
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string TaskId { get; set; } = string.Empty;
    public string ToolName { get; set; } = string.Empty;
    public string Arguments { get; set; } = "{}";
    public string? Result { get; set; }
    public bool Success { get; set; }
    public string? Error { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public TimeSpan? Duration { get; set; }
}

/// <summary>
/// 任务搜索结果
/// </summary>
public class TaskHistorySearchResult
{
    public List<TaskHistoryRecord> Records { get; set; } = new();
    public int TotalCount { get; set; }
    public string Query { get; set; } = string.Empty;
}

/// <summary>
/// 任务统计
/// </summary>
public class TaskHistoryStatistics
{
    public int TotalTasks { get; set; }
    public int CompletedTasks { get; set; }
    public int FailedTasks { get; set; }
    public int RunningTasks { get; set; }
    public double AverageDurationSeconds { get; set; }
    public int TotalToolCalls { get; set; }
    public Dictionary<string, int> ToolUsageCounts { get; set; } = new();
    public Dictionary<TaskType, int> TaskTypeCounts { get; set; } = new();
}
