namespace DesktopMascot.Core.Scheduling;

/// <summary>
/// 任务调度状态
/// </summary>
public enum ScheduleStatus
{
    Pending,
    Running,
    Completed,
    Failed,
    Cancelled,
    Paused
}

/// <summary>
/// 调度类型
/// </summary>
public enum ScheduleType
{
    Once,
    Interval,
    Cron
}

/// <summary>
/// 调度任务定义
/// </summary>
public class ScheduledTask
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public ScheduleType Type { get; set; } = ScheduleType.Once;
    public string ToolName { get; set; } = string.Empty;
    public string Arguments { get; set; } = "{}";
    public DateTime? ScheduledTime { get; set; }
    public TimeSpan? Interval { get; set; }
    public string? CronExpression { get; set; }
    public bool IsEnabled { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastRunTime { get; set; }
    public DateTime? NextRunTime { get; set; }
    public int MaxRetries { get; set; } = 3;
    public int CurrentRetry { get; set; } = 0;
}

/// <summary>
/// 调度任务执行记录
/// </summary>
public class ScheduleExecution
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string TaskId { get; set; } = string.Empty;
    public ScheduleStatus Status { get; set; }
    public string? Result { get; set; }
    public string? Error { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public TimeSpan? Duration => CompletedAt.HasValue ? CompletedAt.Value - StartedAt : null;
}

/// <summary>
/// 调度统计
/// </summary>
public class ScheduleStatistics
{
    public int TotalTasks { get; set; }
    public int ActiveTasks { get; set; }
    public int CompletedExecutions { get; set; }
    public int FailedExecutions { get; set; }
    public DateTime? LastExecutionTime { get; set; }
}

/// <summary>
/// 调度事件
/// </summary>
public class ScheduleEvent
{
    public string TaskId { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
