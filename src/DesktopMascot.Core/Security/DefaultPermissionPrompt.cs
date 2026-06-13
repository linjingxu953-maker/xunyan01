using DesktopMascot.Core.Enums;

namespace DesktopMascot.Core.Security;

/// <summary>
/// 默认权限确认处理器 - 用于 UI 未实现时的降级处理
/// </summary>
public class DefaultPermissionPrompt : IPermissionPrompt
{
    private readonly Dictionary<(PromptPermissionType, string), bool> _permissions = new();

    /// <summary>
    /// 默认决策 - 可配置
    /// </summary>
    public PermissionDecision DefaultDecision { get; set; } = PermissionDecision.Deny;

    /// <summary>
    /// 自动批准的权限类型
    /// </summary>
    public HashSet<PromptPermissionType> AutoApproveTypes { get; set; } = new()
    {
        PromptPermissionType.FileRead,
        PromptPermissionType.WindowRead,
        PromptPermissionType.SelectedTextRead
    };

    public Task<PermissionPromptResponse> PromptAsync(
        PermissionPromptRequest request,
        CancellationToken ct = default)
    {
        // 检查是否已授权
        if (_permissions.TryGetValue((request.PermissionType, request.Scope), out var hasPermission) && hasPermission)
        {
            return Task.FromResult(new PermissionPromptResponse
            {
                RequestId = request.RequestId,
                Decision = PermissionDecision.AllowOnce,
                DenyReason = null
            });
        }

        // 检查是否自动批准
        if (AutoApproveTypes.Contains(request.PermissionType))
        {
            return Task.FromResult(new PermissionPromptResponse
            {
                RequestId = request.RequestId,
                Decision = PermissionDecision.AllowOnce,
                DenyReason = null
            });
        }

        // 使用默认决策
        return Task.FromResult(new PermissionPromptResponse
        {
            RequestId = request.RequestId,
            Decision = DefaultDecision,
            DenyReason = DefaultDecision == PermissionDecision.Deny ? "默认拒绝" : null
        });
    }

    public bool HasPermission(PromptPermissionType type, string scope)
    {
        return _permissions.TryGetValue((type, scope), out var hasPermission) && hasPermission;
    }

    public void RevokePermission(PromptPermissionType type, string scope)
    {
        _permissions.Remove((type, scope));
    }

    /// <summary>
    /// 授予权限
    /// </summary>
    public void GrantPermission(PromptPermissionType type, string scope)
    {
        _permissions[(type, scope)] = true;
    }
}
