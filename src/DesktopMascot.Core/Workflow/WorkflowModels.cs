namespace DesktopMascot.Core.Workflow;

/// <summary>
/// 工作流状态
/// </summary>
public enum WorkflowStatus
{
    Pending,
    Running,
    Completed,
    Failed,
    Cancelled,
    Paused
}

/// <summary>
/// 步骤状态
/// </summary>
public enum StepStatus
{
    Pending,
    Running,
    Completed,
    Failed,
    Skipped,
    WaitingForApproval
}

/// <summary>
/// 工作流定义
/// </summary>
public class WorkflowDefinition
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<WorkflowStep> Steps { get; set; } = new();
    public Dictionary<string, object> Variables { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// 工作流步骤
/// </summary>
public class WorkflowStep
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = string.Empty;
    public string ToolName { get; set; } = string.Empty;
    public string ArgumentsTemplate { get; set; } = "{}";
    public List<string> DependsOn { get; set; } = new();
    public bool RequiresApproval { get; set; }
    public int Order { get; set; }
    public Dictionary<string, string> OutputMapping { get; set; } = new();
}

/// <summary>
/// 工作流实例
/// </summary>
public class WorkflowInstance
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string DefinitionId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public WorkflowStatus Status { get; set; } = WorkflowStatus.Pending;
    public List<StepInstance> Steps { get; set; } = new();
    public Dictionary<string, object> Variables { get; set; } = new();
    public string? ErrorMessage { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}

/// <summary>
/// 步骤实例
/// </summary>
public class StepInstance
{
    public string StepId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    /// <summary>步骤对应的工具名称</summary>
    public string ToolName { get; set; } = string.Empty;
    /// <summary>工具参数模板（可含 {variable} 占位符）</summary>
    public string ArgumentsTemplate { get; set; } = "{}";
    public bool RequiresApproval { get; set; }
    public List<string> OutputMappingKeys { get; set; } = new();
    public List<string> OutputMappingValues { get; set; } = new();
    public StepStatus Status { get; set; } = StepStatus.Pending;
    public string? Input { get; set; }
    public string? Output { get; set; }
    public string? Error { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public TimeSpan? Duration => CompletedAt.HasValue && StartedAt.HasValue
        ? CompletedAt.Value - StartedAt.Value
        : null;
}

/// <summary>
/// 工作流事件
/// </summary>
public class WorkflowEvent
{
    public string WorkflowId { get; set; } = string.Empty;
    public string? StepId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public Dictionary<string, object> Data { get; set; } = new();
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// 工作流执行选项
/// </summary>
public class WorkflowOptions
{
    public int MaxParallelSteps { get; set; } = 1;
    public TimeSpan? StepTimeout { get; set; }
    public TimeSpan? WorkflowTimeout { get; set; }
    public bool ContinueOnError { get; set; } = false;
}
