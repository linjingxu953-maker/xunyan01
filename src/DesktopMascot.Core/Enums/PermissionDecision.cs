namespace DesktopMascot.Core.Enums;

/// <summary>
/// 权限决策
/// </summary>
public enum PermissionDecision
{
    /// <summary>允许</summary>
    Allow,
    /// <summary>拒绝</summary>
    Deny,
    /// <summary>本次允许</summary>
    AllowOnce,
    /// <summary>始终允许</summary>
    AllowAlways
}
