using DesktopMascot.Core.Enums;

namespace DesktopMascot.Core.Security;

/// <summary>
/// 权限管理器接口
/// </summary>
public interface IPermissionManager
{
    /// <summary>请求权限确认</summary>
    Task<PermissionResponse> RequestPermissionAsync(PermissionRequest request, CancellationToken ct = default);
    
    /// <summary>检查是否有权限</summary>
    Task<bool> HasPermissionAsync(string operation, PermissionLevel level, CancellationToken ct = default);
    
    /// <summary>授予永久权限</summary>
    Task GrantPermanentPermissionAsync(string operation, PermissionLevel level, CancellationToken ct = default);
    
    /// <summary>撤销权限</summary>
    Task RevokePermissionAsync(string operation, CancellationToken ct = default);
    
    /// <summary>获取审计日志</summary>
    Task<List<AuditLogEntry>> GetAuditLogsAsync(int limit = 100, CancellationToken ct = default);
    
    /// <summary>记录审计日志</summary>
    Task LogAuditAsync(AuditLogEntry entry, CancellationToken ct = default);
}

/// <summary>
/// 用户确认处理器接口
/// </summary>
public interface IConfirmationHandler
{
    /// <summary>向用户请求确认</summary>
    Task<PermissionResponse> RequestConfirmationAsync(PermissionRequest request, CancellationToken ct = default);
}
