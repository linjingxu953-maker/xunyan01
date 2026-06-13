using System.Collections.Concurrent;
using DesktopMascot.Core.Enums;

namespace DesktopMascot.Core.Security;

/// <summary>
/// 权限管理器实现
/// </summary>
public class PermissionManager : IPermissionManager
{
    private readonly ConcurrentDictionary<string, PermissionLevel> _permanentPermissions = new();
    private readonly ConcurrentDictionary<string, PermissionLevel> _sessionPermissions = new();
    private readonly List<AuditLogEntry> _auditLogs = new();
    private readonly IConfirmationHandler? _confirmationHandler;
    private readonly IAuditLogStore? _auditLogStore;
    private readonly object _auditLock = new();

    public PermissionManager(
        IConfirmationHandler? confirmationHandler = null,
        IAuditLogStore? auditLogStore = null)
    {
        _confirmationHandler = confirmationHandler;
        _auditLogStore = auditLogStore;
    }

    public async Task<PermissionResponse> RequestPermissionAsync(PermissionRequest request, CancellationToken ct = default)
    {
        // 检查永久权限
        if (_permanentPermissions.TryGetValue(request.Target, out var existingLevel) &&
            existingLevel >= request.Level)
        {
            var autoResponse = new PermissionResponse
            {
                RequestId = request.Id,
                Decision = PermissionDecision.AllowAlways,
                Reason = "永久授权已存在"
            };
            
            await LogAuditAsync(new AuditLogEntry
            {
                TaskId = request.TaskId,
                Operation = request.Title,
                Target = request.Target,
                Level = request.Level,
                Decision = PermissionDecision.AllowAlways,
                Details = "自动批准：永久授权"
            }, ct);
            
            return autoResponse;
        }

        // 检查会话权限
        if (_sessionPermissions.TryGetValue(request.Target, out var sessionLevel) &&
            sessionLevel >= request.Level)
        {
            return new PermissionResponse
            {
                RequestId = request.Id,
                Decision = PermissionDecision.AllowOnce,
                Reason = "会话授权"
            };
        }

        // L6 操作直接拒绝
        if (request.Level == PermissionLevel.L6_Forbidden)
        {
            var deniedResponse = new PermissionResponse
            {
                RequestId = request.Id,
                Decision = PermissionDecision.Deny,
                Reason = "此操作在初版中被禁止"
            };
            
            await LogAuditAsync(new AuditLogEntry
            {
                TaskId = request.TaskId,
                Operation = request.Title,
                Target = request.Target,
                Level = request.Level,
                Decision = PermissionDecision.Deny,
                Details = "L6 操作被禁止"
            }, ct);
            
            return deniedResponse;
        }

        // L0 操作直接允许
        if (request.Level == PermissionLevel.L0_Chat)
        {
            return new PermissionResponse
            {
                RequestId = request.Id,
                Decision = PermissionDecision.Allow,
                Reason = "普通聊天无需确认"
            };
        }

        // 需要用户确认
        if (_confirmationHandler != null)
        {
            var response = await _confirmationHandler.RequestConfirmationAsync(request, ct);
            
            // 记录决策
            if (response.Decision == PermissionDecision.AllowAlways)
            {
                _permanentPermissions[request.Target] = request.Level;
            }
            else if (response.Decision == PermissionDecision.AllowOnce)
            {
                _sessionPermissions[request.Target] = request.Level;
            }
            
            await LogAuditAsync(new AuditLogEntry
            {
                TaskId = request.TaskId,
                Operation = request.Title,
                Target = request.Target,
                Level = request.Level,
                Decision = response.Decision,
                Details = response.Reason
            }, ct);
            
            return response;
        }

        // 无确认处理器，默认拒绝
        return new PermissionResponse
        {
            RequestId = request.Id,
            Decision = PermissionDecision.Deny,
            Reason = "无确认处理器"
        };
    }

    public Task<bool> HasPermissionAsync(string operation, PermissionLevel level, CancellationToken ct = default)
    {
        // 检查永久权限
        if (_permanentPermissions.TryGetValue(operation, out var permanentLevel) &&
            permanentLevel >= level)
        {
            return Task.FromResult(true);
        }

        // 检查会话权限
        if (_sessionPermissions.TryGetValue(operation, out var sessionLevel) &&
            sessionLevel >= level)
        {
            return Task.FromResult(true);
        }

        // L0 总是允许
        if (level == PermissionLevel.L0_Chat)
        {
            return Task.FromResult(true);
        }

        return Task.FromResult(false);
    }

    public Task GrantPermanentPermissionAsync(string operation, PermissionLevel level, CancellationToken ct = default)
    {
        _permanentPermissions[operation] = level;
        return Task.CompletedTask;
    }

    public Task RevokePermissionAsync(string operation, CancellationToken ct = default)
    {
        _permanentPermissions.TryRemove(operation, out _);
        _sessionPermissions.TryRemove(operation, out _);
        return Task.CompletedTask;
    }

    public Task<List<AuditLogEntry>> GetAuditLogsAsync(int limit = 100, CancellationToken ct = default)
    {
        lock (_auditLock)
        {
            var logs = _auditLogs
                .OrderByDescending(l => l.Timestamp)
                .Take(limit)
                .ToList();
            return Task.FromResult(logs);
        }
    }

    public Task LogAuditAsync(AuditLogEntry entry, CancellationToken ct = default)
    {
        lock (_auditLock)
        {
            _auditLogs.Add(entry);
            
            // 保持日志在合理范围
            if (_auditLogs.Count > 1000)
            {
                _auditLogs.RemoveRange(0, _auditLogs.Count - 1000);
            }
        }
        return Task.CompletedTask;
    }

    /// <summary>
    /// 清除会话权限
    /// </summary>
    public void ClearSessionPermissions()
    {
        _sessionPermissions.Clear();
    }
}
