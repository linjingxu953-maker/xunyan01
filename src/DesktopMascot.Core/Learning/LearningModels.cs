using System.Collections.Concurrent;
using DesktopMascot.Core.Enums;
using DesktopMascot.Core.Storage;

namespace DesktopMascot.Core.Learning;

using DesktopMascot.Core.Summary;

/// <summary>
/// 学习引擎 - 从用户反馈和任务模式中学习（线程安全）
/// </summary>
public class LearningEngine
{
    private readonly ConcurrentDictionary<string, UserPreference> _preferences = new();
    private readonly ConcurrentBag<EvolutionRecord> _evolutionHistory = new();
    private readonly ConcurrentDictionary<string, int> _taskPatterns = new(); // taskType -> count
    private readonly ConcurrentDictionary<string, ConcurrentBag<string>> _skillSuggestions = new();

    /// <summary>记录用户反馈</summary>
    public void RecordFeedback(string taskId, FeedbackType type, string content)
    {
        switch (type)
        {
            case FeedbackType.Positive:
                RecordPositiveFeedback(taskId, content);
                break;
            case FeedbackType.Negative:
                RecordNegativeFeedback(taskId, content);
                break;
            case FeedbackType.Correction:
                RecordCorrection(taskId, content);
                break;
            case FeedbackType.Preference:
                RecordPreference(content);
                break;
        }
    }

    /// <summary>记录正面反馈</summary>
    private void RecordPositiveFeedback(string taskId, string content)
    {
        _evolutionHistory.Add(new EvolutionRecord
        {
            Type = "positive_feedback",
            Description = content,
            ImpactScore = 0.3f,
            SourceTaskId = taskId
        });
    }

    /// <summary>记录负面反馈</summary>
    private void RecordNegativeFeedback(string taskId, string content)
    {
        _evolutionHistory.Add(new EvolutionRecord
        {
            Type = "negative_feedback",
            Description = content,
            ImpactScore = -0.3f,
            SourceTaskId = taskId
        });
    }

    /// <summary>记录纠正</summary>
    private void RecordCorrection(string taskId, string correction)
    {
        _evolutionHistory.Add(new EvolutionRecord
        {
            Type = "correction",
            Description = correction,
            ImpactScore = 0.5f,
            SourceTaskId = taskId
        });
    }

    /// <summary>记录偏好</summary>
    private void RecordPreference(string preference)
    {
        var parts = preference.Split(':', 2);
        if (parts.Length == 2)
        {
            var key = parts[0].Trim();
            var value = parts[1].Trim();

            _preferences.AddOrUpdate(key,
                _ => new UserPreference
                {
                    Key = key,
                    Value = value,
                    Confidence = 0.5f,
                    ObservationCount = 1
                },
                (_, existing) =>
                {
                    existing.Value = value;
                    existing.Confidence = Math.Min(1.0f, existing.Confidence + 0.1f);
                    existing.ObservationCount++;
                    existing.LastObserved = DateTime.UtcNow;
                    return existing;
                });
        }
    }

    /// <summary>分析任务模式</summary>
    public void AnalyzeTaskPattern(string taskType, bool success)
    {
        var key = $"{taskType}_{(success ? "success" : "failure")}";
        _taskPatterns.AddOrUpdate(key, 1, (_, c) => c + 1);

        // 检测重复模式
        var successKey = $"{taskType}_success";
        var failureKey = $"{taskType}_failure";
        _taskPatterns.TryGetValue(successKey, out var successCount);
        _taskPatterns.TryGetValue(failureKey, out var failureCount);

        if (successCount >= 3 && failureCount == 0)
        {
            SuggestSkill(taskType, "此任务类型连续成功，建议优化流程");
        }
        else if (failureCount >= 2)
        {
            SuggestSkill(taskType, "此任务类型连续失败，建议改进策略");
        }
    }

    /// <summary>建议技能</summary>
    private void SuggestSkill(string taskType, string reason)
    {
        var suggestions = _skillSuggestions.GetOrAdd(taskType, _ => new ConcurrentBag<string>());
        suggestions.Add(reason);
    }

    /// <summary>获取用户偏好</summary>
    public UserPreference? GetPreference(string key)
    {
        return _preferences.TryGetValue(key, out var pref) ? pref : null;
    }

    /// <summary>获取所有偏好</summary>
    public IReadOnlyDictionary<string, UserPreference> GetAllPreferences()
    {
        // 返回快照避免并发问题
        return new Dictionary<string, UserPreference>(_preferences);
    }

    /// <summary>获取进化历史</summary>
    public IReadOnlyList<EvolutionRecord> GetEvolutionHistory()
    {
        return _evolutionHistory.ToArray();
    }

    /// <summary>获取技能建议</summary>
    public IReadOnlyDictionary<string, List<string>> GetSkillSuggestions()
    {
        return _skillSuggestions.ToDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value.ToList());
    }

    /// <summary>获取任务成功率</summary>
    public float GetTaskSuccessRate(string taskType)
    {
        var successKey = $"{taskType}_success";
        var failureKey = $"{taskType}_failure";
        _taskPatterns.TryGetValue(successKey, out var successCount);
        _taskPatterns.TryGetValue(failureKey, out var failureCount);
        var total = successCount + failureCount;
        return total == 0 ? 0f : (float)successCount / total;
    }

    /// <summary>记录工具使用</summary>
    public void RecordToolUsage(string toolName, bool success)
    {
        var key = $"tool_{toolName}_{(success ? "success" : "failure")}";
        _taskPatterns.AddOrUpdate(key, 1, (_, c) => c + 1);
    }

    /// <summary>记录任务耗时</summary>
    public void RecordTaskDuration(string taskType, TimeSpan duration)
    {
        var key = $"duration_{taskType}";
        _taskPatterns.AddOrUpdate(key, (int)duration.TotalSeconds, (_, c) => (c + (int)duration.TotalSeconds) / 2);
    }

    /// <summary>记录用户活跃时间</summary>
    public void RecordActiveTime(DateTime time)
    {
        var hour = time.Hour;
        var key = $"active_hour_{hour}";
        _taskPatterns.AddOrUpdate(key, 1, (_, c) => c + 1);
    }

    /// <summary>从任务历史自动生成 Skill</summary>
    public List<GeneratedSkill> GenerateSkillsFromHistory(List<TaskHistoryRecord> history)
    {
        var skills = new List<GeneratedSkill>();
        var taskGroups = history.GroupBy(h => h.Type).ToList();

        foreach (var group in taskGroups)
        {
            if (group.Count() < 3) continue; // 至少3次才生成技能

            var successCount = group.Count(h => h.Status == AppTaskStatus.Completed);
            var successRate = (float)successCount / group.Count();

            if (successRate < 0.5f) continue; // 成功率太低不生成

            var avgDuration = group.Where(h => h.CompletedAt.HasValue && h.CreatedAt != default)
                .Select(h => (h.CompletedAt!.Value - h.CreatedAt).TotalSeconds)
                .DefaultIfEmpty(0)
                .Average();

            var skill = new GeneratedSkill
            {
                TaskType = group.Key.ToString(),
                Name = $"{group.Key}技能",
                Description = $"自动从 {group.Count()} 次任务历史中生成",
                SuccessRate = successRate,
                AverageDurationSeconds = avgDuration,
                GeneratedFrom = "task_history",
                TaskCount = group.Count()
            };

            skills.Add(skill);

            _evolutionHistory.Add(new EvolutionRecord
            {
                Type = "skill_generated",
                Description = $"自动生成 {group.Key} 技能（基于 {group.Count()} 次任务，成功率 {successRate:P0}）",
                ImpactScore = 0.8f
            });
        }

        return skills;
    }

    /// <summary>学习用户行为模式</summary>
    public UserBehaviorPattern AnalyzeBehaviorPattern(List<TaskHistoryRecord> history)
    {
        var pattern = new UserBehaviorPattern();

        // 分析常用工具
        var toolUsage = history
            .SelectMany(h => h.ToolCalls)
            .GroupBy(t => t.ToolName)
            .OrderByDescending(g => g.Count())
            .Take(5)
            .ToList();

        pattern.MostUsedTools = toolUsage.Select(g => g.Key).ToList();

        // 分析常用任务类型
        var taskTypeUsage = history
            .GroupBy(h => h.Type)
            .OrderByDescending(g => g.Count())
            .Take(5)
            .ToList();

        pattern.MostFrequentTasks = taskTypeUsage.Select(g => g.Key.ToString()).ToList();

        // 分析活跃时间段
        var hourGroups = history
            .GroupBy(h => h.CreatedAt.Hour)
            .OrderByDescending(g => g.Count())
            .Take(3)
            .ToList();

        pattern.PeakHours = hourGroups.Select(g => g.Key).ToList();

        // 分析平均任务耗时
        pattern.AverageTaskDurationSeconds = history
            .Where(h => h.CompletedAt.HasValue && h.CreatedAt != default)
            .Select(h => (h.CompletedAt!.Value - h.CreatedAt).TotalSeconds)
            .DefaultIfEmpty(0)
            .Average();

        return pattern;
    }

    /// <summary>生成进化报告</summary>
    public EvolutionReport GenerateReport()
    {
        return new EvolutionReport
        {
            TotalTasks = _taskPatterns.Values.Sum(),
            SuccessRate = CalculateOverallSuccessRate(),
            TopPreferences = _preferences.Values
                .OrderByDescending(p => p.Confidence)
                .Take(5)
                .ToList(),
            RecentEvolutions = _evolutionHistory
                .OrderByDescending(e => e.CreatedAt)
                .Take(10)
                .ToList(),
            SkillSuggestions = _skillSuggestions
                .Where(s => s.Value.Count > 0)
                .ToDictionary(s => s.Key, s => s.Value.Last())
        };
    }

    private float CalculateOverallSuccessRate()
    {
        var successCount = _taskPatterns
            .Where(kvp => kvp.Key.EndsWith("_success"))
            .Sum(kvp => kvp.Value);
        var failureCount = _taskPatterns
            .Where(kvp => kvp.Key.EndsWith("_failure"))
            .Sum(kvp => kvp.Value);
        var total = successCount + failureCount;
        return total == 0 ? 0f : (float)successCount / total;
    }
}

/// <summary>
/// 反馈类型
/// </summary>
public enum FeedbackType
{
    Positive,
    Negative,
    Correction,
    Preference
}

/// <summary>
/// 进化报告
/// </summary>
public class EvolutionReport
{
    public int TotalTasks { get; set; }
    public float SuccessRate { get; set; }
    public List<UserPreference> TopPreferences { get; set; } = new();
    public List<EvolutionRecord> RecentEvolutions { get; set; } = new();
    public Dictionary<string, string> SkillSuggestions { get; set; } = new();
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// 自动生成的技能
/// </summary>
public class GeneratedSkill
{
    public string TaskType { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public float SuccessRate { get; set; }
    public double AverageDurationSeconds { get; set; }
    public string GeneratedFrom { get; set; } = string.Empty;
    public int TaskCount { get; set; }
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// 用户行为模式
/// </summary>
public class UserBehaviorPattern
{
    public List<string> MostUsedTools { get; set; } = new();
    public List<string> MostFrequentTasks { get; set; } = new();
    public List<int> PeakHours { get; set; } = new();
    public double AverageTaskDurationSeconds { get; set; }
}
