using DesktopMascot.Agent.Models;

namespace DesktopMascot.Agent.Providers;

/// <summary>
/// LLM 提供者接口
/// </summary>
public interface ILlmProvider
{
    /// <summary>
    /// 发送消息并获取响应
    /// </summary>
    Task<LlmResponse> ChatAsync(
        IEnumerable<LlmMessage> messages,
        IEnumerable<ToolDefinition>? tools = null,
        CancellationToken ct = default);

    /// <summary>
    /// 流式响应
    /// </summary>
    IAsyncEnumerable<string> ChatStreamAsync(
        IEnumerable<LlmMessage> messages,
        CancellationToken ct = default);
}
