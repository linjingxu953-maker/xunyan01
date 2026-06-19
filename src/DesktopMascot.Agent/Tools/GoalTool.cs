using System.Text;
using System.Text.Json;
using DesktopMascot.Agent.Models;
using DesktopMascot.Core.Tools;

namespace DesktopMascot.Agent.Tools;

/// <summary>
/// Goal 循环工具 — AI 可调用的目标管理工具
/// 支持创建目标、执行迭代、查看状态、暂停/恢复/取消
/// </summary>
public class GoalTool : ITool
{
    private readonly Core.Tools.GoalEngine _goalEngine;

    public string Name => "goal";
    public string Description => "目标循环：创建目标、自主多轮执行、裁判评估、预算控制。支持暂停/恢复/取消。";
    public string ParametersSchema => """
    {
        "type": "object",
        "properties": {
            "action": { "type": "string", "enum": ["create", "execute", "pause", "resume", "cancel", "status", "history"], "description": "操作类型" },
            "description": { "type": "string", "description": "目标描述" },
            "success_criteria": { "type": "string", "description": "成功标准" },
            "max_iterations": { "type": "integer", "description": "最大迭代次数（默认10）" },
            "max_tokens": { "type": "integer", "description": "最大Token数（默认50000）" },
            "max_time_seconds": { "type": "integer", "description": "最大执行时间秒数（默认300）" }
        },
        "required": ["action"]
    }
    """;

    public GoalTool(Core.Tools.GoalEngine goalEngine)
    {
        _goalEngine = goalEngine;
    }

    public async Task<ToolResult> ExecuteAsync(string arguments, CancellationToken ct = default)
    {
        try
        {
            var doc = JsonDocument.Parse(arguments);
            var root = doc.RootElement;
            var action = root.TryGetProperty("action", out var aEl) ? aEl.GetString() ?? "" : "";

            return action switch
            {
                "create" => CreateGoal(root),
                "execute" => await ExecuteIterationAsync(ct),
                "pause" => PauseGoal(),
                "resume" => ResumeGoal(),
                "cancel" => CancelGoal(),
                "status" => GetStatus(),
                "history" => GetHistory(),
                _ => Fail($"不支持的操作：{action}")
            };
        }
        catch (Exception ex)
        {
            return Fail($"目标操作失败：{ex.Message}");
        }
    }

    private ToolResult CreateGoal(JsonElement root)
    {
        var description = root.TryGetProperty("description", out var dEl) ? dEl.GetString() ?? "" : "";
        var criteria = root.TryGetProperty("success_criteria", out var scEl) ? scEl.GetString() ?? "" : "";
        var maxIter = root.TryGetProperty("max_iterations", out var miEl) ? miEl.GetInt32() : 10;
        var maxTokens = root.TryGetProperty("max_tokens", out var mtEl) ? mtEl.GetInt32() : 50000;
        var maxTime = root.TryGetProperty("max_time_seconds", out var mtEl2) ? mtEl2.GetInt32() : 300;

        if (string.IsNullOrEmpty(description)) return Fail("缺少 description 参数");
        if (string.IsNullOrEmpty(criteria)) return Fail("缺少 success_criteria 参数");

        var goal = _goalEngine.CreateGoal(description, criteria, maxIter, maxTokens, maxTime);

        var sb = new StringBuilder();
        sb.AppendLine("✅ 目标已创建");
        sb.AppendLine($"  ID：{goal.Id}");
        sb.AppendLine($"  目标：{goal.Description}");
        sb.AppendLine($"  成功标准：{goal.SuccessCriteria}");
        sb.AppendLine($"  最大轮次：{goal.MaxIterations}");
        sb.AppendLine($"  最大 Token：{goal.MaxTokens}");
        sb.AppendLine($"  最大时间：{goal.MaxTimeSeconds}秒");
        sb.AppendLine();
        sb.AppendLine("使用 execute 操作开始执行。");

        return new ToolResult { Name = Name, Success = true, Content = sb.ToString() };
    }

    private async Task<ToolResult> ExecuteIterationAsync(CancellationToken ct)
    {
        if (_goalEngine.CurrentGoal == null)
            return Fail("请先创建目标（create）");

        // 这里需要外部提供 agentExecute 和 judgeEvaluate
        // 在实际集成中，这两个回调由 AgentOrchestrator 提供
        // 暂时返回提示
        var sb = new StringBuilder();
        sb.AppendLine("🎯 Goal 迭代执行");
        sb.AppendLine();
        sb.AppendLine("此操作需要 AgentOrchestrator 集成后才能执行。");
        sb.AppendLine("执行流程：");
        sb.AppendLine("  1. Agent 根据目标上下文执行操作");
        sb.AppendLine("  2. 裁判评估执行结果");
        sb.AppendLine("  3. 评分 ≥ 80 则目标完成");
        sb.AppendLine("  4. 未达标则进入下一轮迭代");
        sb.AppendLine();
        sb.AppendLine(_goalEngine.GetSummary());

        return new ToolResult { Name = Name, Success = true, Content = sb.ToString() };
    }

    private ToolResult PauseGoal()
    {
        if (_goalEngine.CurrentGoal == null)
            return Fail("无活动目标");

        _goalEngine.Pause();
        return new ToolResult { Name = Name, Success = true, Content = "⏸️ 目标已暂停" };
    }

    private ToolResult ResumeGoal()
    {
        if (_goalEngine.CurrentGoal == null)
            return Fail("无活动目标");

        _goalEngine.Resume();
        return new ToolResult { Name = Name, Success = true, Content = "▶️ 目标已恢复" };
    }

    private ToolResult CancelGoal()
    {
        if (_goalEngine.CurrentGoal == null)
            return Fail("无活动目标");

        _goalEngine.Cancel();
        return new ToolResult { Name = Name, Success = true, Content = "🛑 目标已取消" };
    }

    private ToolResult GetStatus()
    {
        return new ToolResult { Name = Name, Success = true, Content = _goalEngine.GetSummary() };
    }

    private ToolResult GetHistory()
    {
        var goal = _goalEngine.CurrentGoal;
        if (goal == null) return Fail("无活动目标");

        var sb = new StringBuilder();
        sb.AppendLine("Goal 执行历史");
        sb.AppendLine($"目标：{goal.Description}");
        sb.AppendLine($"状态：{goal.Status}");
        sb.AppendLine($"总轮次：{goal.CurrentIteration}");
        sb.AppendLine();

        foreach (var iter in goal.Iterations)
        {
            var status = iter.Success ? "✅" : "❌";
            sb.AppendLine($"第 {iter.Round} 轮 {status} ({iter.Timestamp:HH:mm:ss})");
            sb.AppendLine($"  Token：{iter.TokensUsed}");
            if (!string.IsNullOrEmpty(iter.Error))
                sb.AppendLine($"  错误：{iter.Error}");
            if (!string.IsNullOrEmpty(iter.Result))
                sb.AppendLine($"  结果：{iter.Result[..Math.Min(150, iter.Result.Length)]}...");
            sb.AppendLine();
        }

        return new ToolResult { Name = Name, Success = true, Content = sb.ToString() };
    }

    private static ToolResult Fail(string error) => new() { Name = "goal", Success = false, Error = error };
}
