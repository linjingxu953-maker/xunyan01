using DesktopMascot.Core.Enums;

namespace DesktopMascot.Core.Memory;

/// <summary>
/// 记忆决策枚举
/// </summary>
public enum MemoryDecision
{
    /// <summary>保存到长期记忆</summary>
    Save,
    /// <summary>仅本次有效</summary>
    TempOnly,
    /// <summary>不保存</summary>
    Reject
}

/// <summary>
/// 记忆确认请求
/// </summary>
public record MemoryConfirmationRequest
{
    /// <summary>请求 ID</summary>
    public string RequestId { get; init; } = Guid.NewGuid().ToString();

    /// <summary>任务 ID</summary>
    public string TaskId { get; init; } = string.Empty;

    /// <summary>记忆类型</summary>
    public MemoryType MemoryType { get; init; }

    /// <summary>记忆内容</summary>
    public string Content { get; init; } = string.Empty;

    /// <summary>记忆来源</summary>
    public string Source { get; init; } = string.Empty;

    /// <summary>建议保存的原因</summary>
    public string Reason { get; init; } = string.Empty;

    /// <summary>附加信息</summary>
    public Dictionary<string, string> Metadata { get; init; } = new();

    /// <summary>请求时间</summary>
    public DateTime RequestedAt { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// 记忆确认响应
/// </summary>
public record MemoryConfirmationResponse
{
    /// <summary>请求 ID</summary>
    public string RequestId { get; init; } = string.Empty;

    /// <summary>决策</summary>
    public MemoryDecision Decision { get; init; }

    /// <summary>用户编辑后的内容（可选）</summary>
    public string? EditedContent { get; init; }

    /// <summary>响应时间</summary>
    public DateTime RespondedAt { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// 记忆确认提示接口 - 由 UI 层实现
/// </summary>
public interface IMemoryConfirmationPrompt
{
    /// <summary>
    /// 请求记忆保存确认
    /// </summary>
    /// <param name="request">记忆确认请求</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>记忆确认响应</returns>
    Task<MemoryConfirmationResponse> PromptAsync(
        MemoryConfirmationRequest request,
        CancellationToken ct = default);
}
