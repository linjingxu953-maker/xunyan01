using DesktopMascot.Core.Enums;
using DesktopMascot.Core.Logging;
using DesktopMascot.Core.Memory;
using DesktopMascot.Core.Models;

namespace DesktopMascot.Agent.Memory;

/// <summary>
/// 记忆集成服务 - 桥接 MemoryManager 与 Agent 执行流程
/// </summary>
public class MemoryIntegrationService
{
    private readonly MemoryManager _memoryManager;
    private readonly ILogger _logger;

    public MemoryIntegrationService(MemoryManager memoryManager, ILogger logger)
    {
        _memoryManager = memoryManager;
        _logger = logger;
    }

    /// <summary>
    /// 执行前：搜索相关记忆，注入上下文
    /// </summary>
    public async Task<MemoryContext> SearchRelevantMemoriesAsync(
        AgentTask task, CancellationToken ct = default)
    {
        var context = new MemoryContext();

        try
        {
            var userMemories = await _memoryManager.SearchAsync(task.Input, MemoryType.User, ct);
            context.UserPreferences = userMemories.Entries;

            if (task.Parameters.ContainsKey("ProjectId"))
            {
                var projectMemories = await _memoryManager.SearchAsync(task.Input, MemoryType.Project, ct);
                context.ProjectInfo = projectMemories.Entries;
            }

            var skillMemories = await _memoryManager.SearchAsync(task.Input, MemoryType.Skill, ct);
            context.RelevantSkills = skillMemories.Entries;

            var historyMemories = await _memoryManager.SearchAsync(task.Input, MemoryType.History, ct);
            context.SimilarHistory = historyMemories.Entries;

            _logger.Information($"记忆检索完成：用户{context.UserPreferences.Count}条，项目{context.ProjectInfo.Count}条，技能{context.RelevantSkills.Count}条，历史{context.SimilarHistory.Count}条");
        }
        catch (Exception ex)
        {
            _logger.Warning($"记忆检索失败: {ex.Message}");
        }

        return context;
    }

    /// <summary>
    /// 执行后：分析结果，提议保存记忆
    /// </summary>
    public async Task<List<MemoryProposal>> ProposeMemoriesAsync(
        AgentTask task, TaskResult result, CancellationToken ct = default)
    {
        var proposals = new List<MemoryProposal>();

        try
        {
            var userProposal = AnalyzeUserPreference(task, result);
            if (userProposal != null) proposals.Add(userProposal);

            var projectProposal = AnalyzeProjectInfo(task, result);
            if (projectProposal != null) proposals.Add(projectProposal);

            var skillProposals = await AnalyzeSkillPatternsAsync(task, result, ct);
            proposals.AddRange(skillProposals);

            var historyProposal = CreateHistoryEntry(task, result);
            if (historyProposal != null) proposals.Add(historyProposal);

            _logger.Information($"记忆提议生成：{proposals.Count}条");
        }
        catch (Exception ex)
        {
            _logger.Warning($"记忆提议生成失败: {ex.Message}");
        }

        return proposals;
    }

    /// <summary>
    /// 保存提议的记忆
    /// </summary>
    public async Task<List<MemoryEntry>> SaveProposedMemoriesAsync(
        List<MemoryProposal> proposals, CancellationToken ct = default)
    {
        var saved = new List<MemoryEntry>();

        foreach (var proposal in proposals)
        {
            try
            {
                var entry = await _memoryManager.SaveWithConfirmationAsync(
                    proposal.Entry, proposal.Reason, ct);

                if (entry != null)
                {
                    saved.Add(entry);
                    _logger.Information($"记忆已保存: {entry.Type} - {entry.Key}");
                }
            }
            catch (Exception ex)
            {
                _logger.Warning($"保存记忆失败: {ex.Message}");
            }
        }

        return saved;
    }

    private MemoryProposal? AnalyzeUserPreference(AgentTask task, TaskResult result)
    {
        var signals = new List<(string Content, int Weight)>();

        // --- 语言偏好（输入 + 输出双端检测）---
        var inputChinese = ContainsCjk(task.Input);
        var outputChinese = ContainsCjk(result.Content);
        if (inputChinese || outputChinese)
            signals.Add(("用户使用中文交流", 3));

        // --- 回复风格偏好 ---
        if (ContainsKeyword(task.Input, "简洁", "简短", "一句话", "简单说")) signals.Add(("用户偏好简洁回答", 4));
        if (ContainsKeyword(task.Input, "详细", "详细说明", "详细解释", "展开", "深入")) signals.Add(("用户偏好详细回答", 4));
        if (ContainsKeyword(task.Input, "代码", "code", "示例", "example", "demo")) signals.Add(("用户偏好带代码示例的回答", 3));

        // --- 任务模式偏好 ---
        if (task.Type == TaskType.SolveProblem) signals.Add(("用户经常解决题目类问题", 2));
        if (task.Type == TaskType.InspectProject) signals.Add(("用户关注项目结构分析", 2));
        if (task.Type == TaskType.AnalyzeError) signals.Add(("用户经常排查错误", 2));
        if (task.Type == TaskType.WriteFile) signals.Add(("用户需要生成文件", 2));

        // --- 输出内容推断 ---
        if (result.Content.Contains("\n- ") || result.Content.Contains("\n1. ")) signals.Add(("用户偏好结构化/列表式回答", 2));
        if (result.Content.Contains("```")) signals.Add(("用户偏好带代码块的回答", 2));

        // 信号去重并按权重排序，取 Top 3
        var merged = signals
            .GroupBy(s => s.Content)
            .Select(g => (Content: g.Key, Weight: g.Sum(s => s.Weight)))
            .OrderByDescending(s => s.Weight)
            .Take(3)
            .ToList();

        if (merged.Count == 0) return null;

        var preferenceText = string.Join("；", merged.Select(m => m.Content));
        var confidence = merged.Sum(m => m.Weight) / (float)(merged.Count * 5);

        return new MemoryProposal
        {
            Entry = new MemoryEntry
            {
                Type = MemoryType.User,
                Key = $"preference_{task.Type}_{DateTime.UtcNow:yyyyMMdd}",
                Content = preferenceText,
                Source = $"任务 {task.Id}",
                Tags = new Dictionary<string, string>
                {
                    ["taskType"] = task.Type.ToString(),
                    ["source"] = "auto_detect",
                    ["confidence"] = confidence.ToString("F2")
                }
            },
            Reason = confidence >= 0.6 ? "检测到明确用户偏好" : "检测到潜在用户偏好",
            Priority = confidence >= 0.6 ? MemoryPriority.Medium : MemoryPriority.Low
        };
    }

    /// <summary>检测是否包含 CJK 字符</summary>
    private static bool ContainsCjk(string text)
    {
        foreach (char c in text)
            if (c >= 0x4E00 && c <= 0x9FFF || c >= 0x3400 && c <= 0x4DBF) return true;
        return false;
    }

    /// <summary>批量关键词检测</summary>
    private static bool ContainsKeyword(string text, params string[] keywords)
    {
        return keywords.Any(k => text.Contains(k, StringComparison.OrdinalIgnoreCase));
    }

    private MemoryProposal? AnalyzeProjectInfo(AgentTask task, TaskResult result)
    {
        if (!task.Parameters.ContainsKey("ProjectPath")) return null;
        var projectPath = task.Parameters["ProjectPath"]?.ToString() ?? "";
        if (string.IsNullOrEmpty(projectPath)) return null;

        return new MemoryProposal
        {
            Entry = new MemoryEntry
            {
                Type = MemoryType.Project,
                Key = $"project_{Path.GetFileName(projectPath)}",
                Content = $"路径: {projectPath}\n类型: {task.Type}\n摘要: {result.Content[..Math.Min(200, result.Content.Length)]}",
                Source = $"任务 {task.Id}",
                Tags = new Dictionary<string, string> { ["path"] = projectPath, ["taskType"] = task.Type.ToString() }
            },
            Reason = "记录项目信息",
            Priority = MemoryPriority.Medium
        };
    }

    private async Task<List<MemoryProposal>> AnalyzeSkillPatternsAsync(
        AgentTask task, TaskResult result, CancellationToken ct)
    {
        var proposals = new List<MemoryProposal>();
        var historySearch = await _memoryManager.SearchAsync(task.Type.ToString(), MemoryType.History, ct);

        var similarTasks = historySearch.Entries
            .Where(h => h.Tags.ContainsKey("taskType") && h.Tags["taskType"] == task.Type.ToString())
            .ToList();

        if (similarTasks.Count >= 3)
        {
            var skillKey = $"skill_{task.Type.ToString().ToLower()}";
            var existingSkill = await _memoryManager.GetAsync(skillKey, MemoryType.Skill, ct);

            if (existingSkill == null)
            {
                proposals.Add(new MemoryProposal
                {
                    Entry = new MemoryEntry
                    {
                        Type = MemoryType.Skill,
                        Key = skillKey,
                        Content = $"重复模式：{task.Type}\n执行次数：{similarTasks.Count}\n建议流程：{ExtractWorkflow(result)}",
                        Source = "自动检测",
                        Tags = new Dictionary<string, string> { ["taskType"] = task.Type.ToString(), ["count"] = similarTasks.Count.ToString(), ["auto_generated"] = "true" }
                    },
                    Reason = $"检测到重复模式（{similarTasks.Count}次），自动生成技能",
                    Priority = MemoryPriority.High
                });
            }
        }

        return proposals;
    }

    private MemoryProposal? CreateHistoryEntry(AgentTask task, TaskResult result)
    {
        return new MemoryProposal
        {
            Entry = new MemoryEntry
            {
                Type = MemoryType.History,
                Key = $"history_{task.Id}",
                Content = $"任务: {task.Title}\n输入: {task.Input}\n结果: {result.Content[..Math.Min(500, result.Content.Length)]}",
                TaskId = task.Id,
                Source = "自动记录",
                Tags = new Dictionary<string, string> { ["taskType"] = task.Type.ToString(), ["success"] = result.Success.ToString(), ["completedAt"] = result.CompletedAt.ToString("o") }
            },
            Reason = "记录任务历史",
            Priority = MemoryPriority.Low
        };
    }

    private static string ExtractWorkflow(TaskResult result)
    {
        var steps = new List<string>();
        if (result.Content.Contains("步骤")) steps.Add("按步骤执行");
        if (result.Content.Contains("分析")) steps.Add("先分析再执行");
        if (result.Content.Contains("验证")) steps.Add("执行后验证");
        return steps.Count > 0 ? string.Join(" → ", steps) : "标准流程";
    }
}

/// <summary>记忆上下文</summary>
public class MemoryContext
{
    public List<MemoryEntry> UserPreferences { get; set; } = new();
    public List<MemoryEntry> ProjectInfo { get; set; } = new();
    public List<MemoryEntry> RelevantSkills { get; set; } = new();
    public List<MemoryEntry> SimilarHistory { get; set; } = new();

    public bool HasRelevantMemories => UserPreferences.Count > 0 || ProjectInfo.Count > 0 || RelevantSkills.Count > 0 || SimilarHistory.Count > 0;

    public string ToPromptContext()
    {
        var parts = new List<string>();
        if (UserPreferences.Count > 0) { parts.Add("用户偏好："); foreach (var p in UserPreferences.Take(3)) parts.Add($"  - {p.Content}"); }
        if (RelevantSkills.Count > 0) { parts.Add("相关技能："); foreach (var s in RelevantSkills.Take(2)) parts.Add($"  - {s.Content}"); }
        if (SimilarHistory.Count > 0) { parts.Add("历史经验："); foreach (var h in SimilarHistory.Take(2)) parts.Add($"  - {h.Content}"); }
        return parts.Count > 0 ? "\n\n参考记忆：\n" + string.Join("\n", parts) : "";
    }
}

/// <summary>记忆提议</summary>
public class MemoryProposal
{
    public MemoryEntry Entry { get; set; } = new();
    public string Reason { get; set; } = string.Empty;
    public MemoryPriority Priority { get; set; } = MemoryPriority.Low;
}

/// <summary>记忆优先级</summary>
public enum MemoryPriority { Low, Medium, High }
