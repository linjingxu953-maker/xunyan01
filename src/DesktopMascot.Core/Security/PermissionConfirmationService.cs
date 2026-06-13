using DesktopMascot.Core.Enums;
using DesktopMascot.Core.Interfaces;
using DesktopMascot.Core.Models;

namespace DesktopMascot.Core.Security;

/// <summary>
/// 权限确认服务 - 桥接 IPermissionManager 和 IPermissionPrompt
/// </summary>
public class PermissionConfirmationService
{
    private readonly IPermissionManager _permissionManager;
    private readonly IPermissionPrompt _permissionPrompt;
    private readonly ITaskEventStream? _eventStream;

    public PermissionConfirmationService(
        IPermissionManager permissionManager,
        IPermissionPrompt permissionPrompt,
        ITaskEventStream? eventStream = null)
    {
        _permissionManager = permissionManager;
        _permissionPrompt = permissionPrompt;
        _eventStream = eventStream;
    }

    /// <summary>
    /// 请求权限确认
    /// </summary>
    public async Task<PermissionPromptResponse> RequestPermissionAsync(
        string taskId,
        PromptPermissionType permissionType,
        string scope,
        string reason,
        string commandOrPath = "",
        PromptRiskLevel riskLevel = PromptRiskLevel.Medium,
        CancellationToken ct = default)
    {
        var request = new PermissionPromptRequest
        {
            TaskId = taskId,
            PermissionType = permissionType,
            Scope = scope,
            Reason = reason,
            CommandOrPath = commandOrPath,
            RiskLevel = riskLevel
        };

        // 发布权限请求事件
        _eventStream?.Publish(TaskEvent.PermissionRequested(
            taskId,
            permissionType.ToString(),
            scope,
            reason));

        // 调用 UI 确认
        var response = await _permissionPrompt.PromptAsync(request, ct);

        // 记录审计日志
        await _permissionManager.LogAuditAsync(new AuditLogEntry
        {
            TaskId = taskId,
            Operation = permissionType.ToString(),
            Target = scope,
            Level = MapToPermissionLevel(permissionType),
            Decision = response.Decision,
            Details = response.DenyReason
        }, ct);

        // 发布权限结果事件
        if (response.Decision == PermissionDecision.AllowOnce ||
            response.Decision == PermissionDecision.AllowAlways)
        {
            _eventStream?.Publish(TaskEvent.ProgressUpdated(
                taskId,
                0,
                $"权限已授予: {permissionType}"));
        }

        return response;
    }

    /// <summary>
    /// 检查是否有权限
    /// </summary>
    public bool HasPermission(PromptPermissionType type, string scope)
    {
        return _permissionPrompt.HasPermission(type, scope);
    }

    /// <summary>
    /// 撤销权限
    /// </summary>
    public void RevokePermission(PromptPermissionType type, string scope)
    {
        _permissionPrompt.RevokePermission(type, scope);
    }

    /// <summary>
    /// 映射到 PermissionLevel
    /// </summary>
    private static PermissionLevel MapToPermissionLevel(PromptPermissionType type)
    {
        return type switch
        {
            PromptPermissionType.FileRead => PermissionLevel.L3_FileRead,
            PromptPermissionType.FileWrite => PermissionLevel.L4_FileWrite,
            PromptPermissionType.FileDelete => PermissionLevel.L6_Forbidden,
            PromptPermissionType.CommandExecute => PermissionLevel.L5_CommandExec,
            PromptPermissionType.ScreenCapture => PermissionLevel.L2_ScreenBrowser,
            PromptPermissionType.BrowserRead => PermissionLevel.L2_ScreenBrowser,
            PromptPermissionType.WindowRead => PermissionLevel.L1_WindowTitle,
            PromptPermissionType.SelectedTextRead => PermissionLevel.L1_WindowTitle,
            PromptPermissionType.MemorySave => PermissionLevel.L3_FileRead,
            PromptPermissionType.MemoryDelete => PermissionLevel.L4_FileWrite,
            PromptPermissionType.ApiCall => PermissionLevel.L2_ScreenBrowser,
            PromptPermissionType.WebhookSend => PermissionLevel.L5_CommandExec,
            _ => PermissionLevel.L0_Chat
        };
    }
}
