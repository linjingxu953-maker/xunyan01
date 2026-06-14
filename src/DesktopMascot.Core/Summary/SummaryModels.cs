namespace DesktopMascot.Core.Summary;

/// <summary>
/// 任务总结
/// </summary>
public class TaskSummary
{
    public string TaskId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Input { get; set; } = string.Empty;
    public string Output { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public List<string> KeyPoints { get; set; } = new();
    public List<string> ToolsUsed { get; set; } = new();
    public List<string> FilesModified { get; set; } = new();
    public bool Success { get; set; }
    public string? Error { get; set; }
    public TimeSpan? Duration { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Dictionary<string, string> Metadata { get; set; } = new();
}

/// <summary>
/// 对话总结
/// </summary>
public class ConversationSummaryResult
{
    public string ConversationId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public List<string> KeyTopics { get; set; } = new();
    public List<string> ActionItems { get; set; } = new();
    public List<string> DecisionsMade { get; set; } = new();
    public List<string> QuestionsUnanswered { get; set; } = new();
    public int MessageCount { get; set; }
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// 自我进化记录
/// </summary>
public class EvolutionRecord
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Type { get; set; } = string.Empty; // "skill_learned", "preference_detected", "pattern_optimized"
    public string Description { get; set; } = string.Empty;
    public string BeforeState { get; set; } = string.Empty;
    public string AfterState { get; set; } = string.Empty;
    public float ImpactScore { get; set; } // 0-1, 影响程度
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? SourceTaskId { get; set; }
}

/// <summary>
/// 用户偏好
/// </summary>
public class UserPreference
{
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty; // "response_style", "tool_usage", "language", etc.
    public float Confidence { get; set; } = 0.5f;
    public int ObservationCount { get; set; } = 1;
    public DateTime LastObserved { get; set; } = DateTime.UtcNow;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
