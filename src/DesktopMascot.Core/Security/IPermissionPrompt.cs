using DesktopMascot.Core.Enums;

namespace DesktopMascot.Core.Security;

/// <summary>
/// 权限类型枚举
/// </summary>
public enum PromptPermissionType
{
    // 文件操作
    /// <summary>读取文件</summary>
    FileRead,
    /// <summary>写入文件</summary>
    FileWrite,
    /// <summary>删除文件</summary>
    FileDelete,

    // 命令执行
    /// <summary>执行命令</summary>
    CommandExecute,

    // 上下文读取
    /// <summary>屏幕截图</summary>
    ScreenCapture,
    /// <summary>浏览器读取</summary>
    BrowserRead,
    /// <summary>窗口读取</summary>
    WindowRead,
    /// <summary>选中文本读取</summary>
    SelectedTextRead,

    // 记忆操作
    /// <summary>保存记忆</summary>
    MemorySave,
    /// <summary>删除记忆</summary>
    MemoryDelete,

    // 外部调用
    /// <summary>API 调用</summary>
    ApiCall,
    /// <summary>Webhook 发送</summary>
    WebhookSend
}

/// <summary>
/// 风险等级
/// </summary>
public enum PromptRiskLevel
{
    /// <summary>低风险：读取文件、读取窗口标题</summary>
    Low,
    /// <summary>中风险：写入文件、执行命令</summary>
    Medium,
    /// <summary>高风险：删除文件、执行危险命令</summary>
    High
}

/// <summary>
/// 权限确认请求
/// </summary>
public record PermissionPromptRequest
{
    /// <summary>请求 ID</summary>
    public string RequestId { get; init; } = Guid.NewGuid().ToString();

    /// <summary>任务 ID</summary>
    public string TaskId { get; init; } = string.Empty;

    /// <summary>权限类型</summary>
    public PromptPermissionType PermissionType { get; init; }

    /// <summary>权限范围（如文件路径、命令）</summary>
    public string Scope { get; init; } = string.Empty;

    /// <summary>请求原因</summary>
    public string Reason { get; init; } = string.Empty;

    /// <summary>命令或文件路径</summary>
    public string CommandOrPath { get; init; } = string.Empty;

    /// <summary>风险等级</summary>
    public PromptRiskLevel RiskLevel { get; init; }

    /// <summary>附加信息</summary>
    public Dictionary<string, string> Metadata { get; init; } = new();

    /// <summary>请求时间</summary>
    public DateTime RequestedAt { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// 权限确认响应
/// </summary>
public record PermissionPromptResponse
{
    /// <summary>请求 ID</summary>
    public string RequestId { get; init; } = string.Empty;

    /// <summary>决策</summary>
    public PermissionDecision Decision { get; init; }

    /// <summary>拒绝原因</summary>
    public string? DenyReason { get; init; }

    /// <summary>响应时间</summary>
    public DateTime RespondedAt { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// 权限确认提示接口 - 由 UI 层实现
/// </summary>
public interface IPermissionPrompt
{
    /// <summary>
    /// 请求权限确认
    /// </summary>
    /// <param name="request">权限请求</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>权限响应</returns>
    Task<PermissionPromptResponse> PromptAsync(
        PermissionPromptRequest request,
        CancellationToken ct = default);

    /// <summary>
    /// 检查是否已有权限
    /// </summary>
    /// <param name="type">权限类型</param>
    /// <param name="scope">权限范围</param>
    /// <returns>是否有权限</returns>
    bool HasPermission(PromptPermissionType type, string scope);

    /// <summary>
    /// 撤销权限
    /// </summary>
    /// <param name="type">权限类型</param>
    /// <param name="scope">权限范围</param>
    void RevokePermission(PromptPermissionType type, string scope);
}
