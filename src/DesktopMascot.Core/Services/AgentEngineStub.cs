using DesktopMascot.Core.Enums;
using DesktopMascot.Core.Interfaces;
using DesktopMascot.Core.Models;

namespace DesktopMascot.Core.Services;

/// <summary>
/// 假 Agent 引擎（第一阶段用）
/// </summary>
public class AgentEngineStub : IAgentEngine
{
    public async Task<TaskResult> ExecuteAsync(AgentTask task, CancellationToken ct = default)
    {
        // 模拟处理延迟
        await Task.Delay(Random.Shared.Next(300, 800), ct);

        // 返回假响应
        var response = task.Type switch
        {
            TaskType.Chat => $"收到你的消息：\"{task.Input}\"。这是一个假响应，用于验证任务流。",
            TaskType.SummarizePage => "这是一个网页总结的假响应。",
            TaskType.AnalyzeError => "这是一个报错分析的假响应。",
            _ => $"任务 \"{task.Title}\" 已完成（假响应）。"
        };

        return new TaskResult
        {
            TaskId = task.Id,
            Success = true,
            Content = response
        };
    }

    public async IAsyncEnumerable<string> ExecuteStreamingAsync(AgentTask task, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        var chunks = new[] { "收到你的消息：\"", task.Input, "\"。这是流式响应。" };
        foreach (var chunk in chunks)
        {
            ct.ThrowIfCancellationRequested();
            await Task.Delay(50, ct);
            yield return chunk;
        }
    }
}
