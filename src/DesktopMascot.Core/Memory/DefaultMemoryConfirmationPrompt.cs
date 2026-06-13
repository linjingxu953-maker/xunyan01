namespace DesktopMascot.Core.Memory;

/// <summary>
/// 默认记忆确认处理器 - 用于 UI 未实现时的降级处理
/// </summary>
public class DefaultMemoryConfirmationPrompt : IMemoryConfirmationPrompt
{
    /// <summary>
    /// 默认决策 - 可配置
    /// </summary>
    public MemoryDecision DefaultDecision { get; set; } = MemoryDecision.Reject;

    /// <summary>
    /// 自动批准的记忆类型
    /// </summary>
    public HashSet<MemoryType> AutoApproveTypes { get; set; } = new();

    /// <summary>
    /// 自动拒绝的记忆类型
    /// </summary>
    public HashSet<MemoryType> AutoRejectTypes { get; set; } = new();

    public Task<MemoryConfirmationResponse> PromptAsync(
        MemoryConfirmationRequest request,
        CancellationToken ct = default)
    {
        // 检查是否自动批准
        if (AutoApproveTypes.Contains(request.MemoryType))
        {
            return Task.FromResult(new MemoryConfirmationResponse
            {
                RequestId = request.RequestId,
                Decision = MemoryDecision.Save,
                EditedContent = null
            });
        }

        // 检查是否自动拒绝
        if (AutoRejectTypes.Contains(request.MemoryType))
        {
            return Task.FromResult(new MemoryConfirmationResponse
            {
                RequestId = request.RequestId,
                Decision = MemoryDecision.Reject,
                EditedContent = null
            });
        }

        // 使用默认决策
        return Task.FromResult(new MemoryConfirmationResponse
        {
            RequestId = request.RequestId,
            Decision = DefaultDecision,
            EditedContent = null
        });
    }
}
