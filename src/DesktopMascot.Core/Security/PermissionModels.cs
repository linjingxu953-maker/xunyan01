using DesktopMascot.Core.Enums;

namespace DesktopMascot.Core.Security;

/// <summary>
/// 权限请求
/// </summary>
public class PermissionRequest
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string TaskId { get; set; } = string.Empty;
    public PermissionLevel Level { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Target { get; set; } = string.Empty;
    public string Risk { get; set; } = string.Empty;
    public Dictionary<string, string> Metadata { get; set; } = new();
    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// 权限决策结果
/// </summary>
public class PermissionResponse
{
    public string RequestId { get; set; } = string.Empty;
    public PermissionDecision Decision { get; set; }
    public string? Reason { get; set; }
    public DateTime RespondedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// 审计日志条目
/// </summary>
public class AuditLogEntry
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string TaskId { get; set; } = string.Empty;
    public string Operation { get; set; } = string.Empty;
    public string Target { get; set; } = string.Empty;
    public PermissionLevel Level { get; set; }
    public PermissionDecision Decision { get; set; }
    public string? Details { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// 权限策略
/// </summary>
public class PermissionPolicy
{
    /// <summary>是否需要确认</summary>
    public bool RequiresConfirmation { get; set; }
    
    /// <summary>是否永久授权</summary>
    public bool AllowPermanent { get; set; }
    
    /// <summary>默认决策</summary>
    public PermissionDecision DefaultDecision { get; set; } = PermissionDecision.Deny;
    
    /// <summary>风险描述</summary>
    public string RiskDescription { get; set; } = string.Empty;
}

/// <summary>
/// 命令风险评估结果
/// </summary>
public class CommandRiskAssessment
{
    public string Command { get; set; } = string.Empty;
    public PermissionLevel RiskLevel { get; set; }
    public bool IsBlocked { get; set; }
    public string? BlockReason { get; set; }
    public List<string> Warnings { get; set; } = new();
}
