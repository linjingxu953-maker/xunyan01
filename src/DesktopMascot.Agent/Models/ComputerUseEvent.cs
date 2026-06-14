using DesktopMascot.Core.Enums;

namespace DesktopMascot.Agent.Models;

/// <summary>
/// Computer Use 事件类型 - UI 层根据这些事件更新控制面板
/// </summary>
public enum ComputerUseEventType
{
    /// <summary>Computer Use 任务开始</summary>
    ComputerUseStarted,
    /// <summary>屏幕已观察（截图完成）</summary>
    ScreenObserved,
    /// <summary>动作已规划（LLM 分析完成）</summary>
    ActionPlanned,
    /// <summary>正在执行动作</summary>
    ActionExecuting,
    /// <summary>动作执行完成</summary>
    ActionCompleted,
    /// <summary>等待用户审批（敏感操作）</summary>
    WaitingUserApproval,
    /// <summary>用户接管控制</summary>
    UserTakeoverRequested,
    /// <summary>Computer Use 任务完成</summary>
    ComputerUseCompleted,
    /// <summary>Computer Use 任务失败</summary>
    ComputerUseFailed
}

/// <summary>
/// Computer Use 事件 - 传递给 UI 层的标准化事件
/// </summary>
public class ComputerUseEvent
{
    public string TaskId { get; set; } = string.Empty;
    public ComputerUseEventType EventType { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>屏幕截图路径（ScreenObserved 时）</summary>
    public string? ScreenshotPath { get; set; }

    /// <summary>规划的动作列表（ActionPlanned 时）</summary>
    public List<PlannedAction>? PlannedActions { get; set; }

    /// <summary>当前执行的动作（ActionExecuting 时）</summary>
    public PlannedAction? CurrentAction { get; set; }

    /// <summary>执行结果（ActionCompleted 时）</summary>
    public string? ActionResult { get; set; }

    /// <summary>需要确认的操作信息（WaitingUserApproval 时）</summary>
    public ApprovalRequest? ApprovalRequest { get; set; }

    /// <summary>错误信息（ComputerUseFailed 时）</summary>
    public string? ErrorMessage { get; set; }

    /// <summary>进度（0-100）</summary>
    public int Progress { get; set; }

    /// <summary>状态消息</summary>
    public string Message { get; set; } = string.Empty;
}

/// <summary>
/// 规划的动作
/// </summary>
public class PlannedAction
{
    public int Step { get; set; }
    public string ActionName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ToolName { get; set; } = string.Empty;
    public string Arguments { get; set; } = "{}";
    public bool RequiresApproval { get; set; }
    public ActionStatus Status { get; set; } = ActionStatus.Pending;
}

/// <summary>
/// 动作状态
/// </summary>
public enum ActionStatus
{
    Pending,
    Executing,
    Completed,
    Failed,
    WaitingApproval,
    Skipped
}

/// <summary>
/// 审批请求
/// </summary>
public class ApprovalRequest
{
    public string RequestId { get; set; } = Guid.NewGuid().ToString("N");
    public string ActionName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string RiskLevel { get; set; } = "medium";
    public Dictionary<string, string> Details { get; set; } = new();
    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Computer Use 会话状态 - UI 可查询
/// </summary>
public class ComputerUseSession
{
    public string TaskId { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public bool IsPaused { get; set; }
    public bool UserHasTakeover { get; set; }
    public string CurrentState { get; set; } = "idle";
    public List<PlannedAction> ActionPlan { get; set; } = new();
    public List<ComputerUseEvent> EventHistory { get; set; } = new();
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}
