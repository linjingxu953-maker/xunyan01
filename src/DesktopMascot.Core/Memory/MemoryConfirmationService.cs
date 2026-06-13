using DesktopMascot.Core.Interfaces;
using DesktopMascot.Core.Models;

namespace DesktopMascot.Core.Memory;

/// <summary>
/// 记忆确认服务 - 桥接 IMemoryStore 和 IMemoryConfirmationPrompt
/// </summary>
public class MemoryConfirmationService
{
    private readonly IMemoryStore _memoryStore;
    private readonly IMemoryConfirmationPrompt _memoryPrompt;
    private readonly ITaskEventStream? _eventStream;

    public MemoryConfirmationService(
        IMemoryStore memoryStore,
        IMemoryConfirmationPrompt memoryPrompt,
        ITaskEventStream? eventStream = null)
    {
        _memoryStore = memoryStore;
        _memoryPrompt = memoryPrompt;
        _eventStream = eventStream;
    }

    /// <summary>
    /// 请求记忆保存确认
    /// </summary>
    public async Task<MemoryConfirmationResponse> RequestMemorySaveAsync(
        string taskId,
        MemoryType memoryType,
        string content,
        string source,
        string reason,
        CancellationToken ct = default)
    {
        var request = new MemoryConfirmationRequest
        {
            TaskId = taskId,
            MemoryType = memoryType,
            Content = content,
            Source = source,
            Reason = reason
        };

        // 发布记忆保存请求事件
        _eventStream?.Publish(TaskEvent.MemorySaveRequested(
            taskId,
            memoryType.ToString(),
            content));

        // 调用 UI 确认
        var response = await _memoryPrompt.PromptAsync(request, ct);

        // 根据决策处理
        switch (response.Decision)
        {
            case MemoryDecision.Save:
                var entry = new MemoryEntry
                {
                    Type = memoryType,
                    Key = GenerateKey(content),
                    Content = response.EditedContent ?? content,
                    Source = source,
                    TaskId = taskId,
                    IsConfirmed = true
                };
                await _memoryStore.SaveAsync(entry, ct);

                _eventStream?.Publish(TaskEvent.ProgressUpdated(
                    taskId,
                    100,
                    "记忆已保存"));
                break;

            case MemoryDecision.TempOnly:
                // 仅本次有效，不保存到长期记忆
                _eventStream?.Publish(TaskEvent.ProgressUpdated(
                    taskId,
                    100,
                    "记忆仅本次有效"));
                break;

            case MemoryDecision.Reject:
                _eventStream?.Publish(TaskEvent.ProgressUpdated(
                    taskId,
                    100,
                    "记忆未保存"));
                break;
        }

        return response;
    }

    /// <summary>
    /// 生成记忆键
    /// </summary>
    private static string GenerateKey(string content)
    {
        // 简单的键生成：取内容的前 50 个字符
        var key = content.Length > 50 ? content[..50] : content;
        return key.ToLower().Replace(" ", "_");
    }
}
