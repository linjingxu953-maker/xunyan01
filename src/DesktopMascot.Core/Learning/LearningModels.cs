namespace DesktopMascot.Core.Learning;

using DesktopMascot.Core.Summary;

/// <summary>
/// 学习引擎 - 从用户反馈和任务模式中学习
/// </summary>
public class LearningEngine
{
    private readonly Dictionary<string, UserPreference> _preferences = new();
    private readonly List<EvolutionRecord> _evolutionHistory = new();
    private readonly Dictionary<string, int> _taskPatterns = new(); // taskType -> count
    private readonly Dictionary<string, List<string>> _skillSuggestions = new();

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

            if (_preferences.TryGetValue(key, out var existing))
            {
                existing.Value = value;
                existing.Confidence = Math.Min(1.0f, existing.Confidence + 0.1f);
                existing.ObservationCount++;
                existing.LastObserved = DateTime.UtcNow;
            }
            else
            {
                _preferences[key] = new UserPreference
                {
                    Key = key,
                    Value = value,
                    Confidence = 0.5f,
                    ObservationCount = 1
                };
            }
        }
    }

    /// <summary>分析任务模式</summary>
    public void AnalyzeTaskPattern(string taskType, bool success)
    {
        var key = $"{taskType}_{(success ? "success" : "failure")}";
        _taskPatterns.TryGetValue(key, out var count);
        _taskPatterns[key] = count + 1;

        // 检测重复模式
        var successKey = $"{taskType}_success";
        var failureKey = $"{taskType}_failure";
        _taskPatterns.TryGetValue(successKey, out var successCount);
        _taskPatterns.TryGetValue(failureKey, out var failureCount);

        if (successCount >= 3 && failureCount == 0)
        {
            // 连续成功，建议优化
            SuggestSkill(taskType, "此任务类型连续成功，建议优化流程");
        }
        else if (failureCount >= 2)
        {
            // 连续失败，建议改进
            SuggestSkill(taskType, "此任务类型连续失败，建议改进策略");
        }
    }

    /// <summary>建议技能</summary>
    private void SuggestSkill(string taskType, string reason)
    {
        if (!_skillSuggestions.ContainsKey(taskType))
        {
            _skillSuggestions[taskType] = new List<string>();
        }
        _skillSuggestions[taskType].Add(reason);
    }

    /// <summary>获取用户偏好</summary>
    public UserPreference? GetPreference(string key)
    {
        return _preferences.TryGetValue(key, out var pref) ? pref : null;
    }

    /// <summary>获取所有偏好</summary>
    public IReadOnlyDictionary<string, UserPreference> GetAllPreferences()
    {
        return _preferences;
    }

    /// <summary>获取进化历史</summary>
    public IReadOnlyList<EvolutionRecord> GetEvolutionHistory()
    {
        return _evolutionHistory.AsReadOnly();
    }

    /// <summary>获取技能建议</summary>
    public IReadOnlyDictionary<string, List<string>> GetSkillSuggestions()
    {
        return _skillSuggestions;
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
