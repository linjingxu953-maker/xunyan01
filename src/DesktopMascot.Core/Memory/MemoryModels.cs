namespace DesktopMascot.Core.Memory;

/// <summary>
/// 记忆类型
/// </summary>
public enum MemoryType
{
    /// <summary>用户偏好、习惯</summary>
    User,
    /// <summary>项目信息、技术栈</summary>
    Project,
    /// <summary>可复用操作流程</summary>
    Skill,
    /// <summary>任务执行历史</summary>
    History
}

/// <summary>
/// 记忆条目
/// </summary>
public class MemoryEntry
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public MemoryType Type { get; set; }
    public string Key { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string? Source { get; set; }
    public string? TaskId { get; set; }
    public Dictionary<string, string> Tags { get; set; } = new();
    public bool IsConfirmed { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ExpiresAt { get; set; }
}

/// <summary>
/// 记忆搜索结果
/// </summary>
public class MemorySearchResult
{
    public List<MemoryEntry> Entries { get; set; } = new();
    public int TotalCount { get; set; }
    public string Query { get; set; } = string.Empty;
}

/// <summary>
/// 记忆确认请求
/// </summary>
public class MemoryConfirmRequest
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public MemoryEntry ProposedMemory { get; set; } = new();
    public string Reason { get; set; } = string.Empty;
    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// 记忆统计
/// </summary>
public class MemoryStatistics
{
    public int TotalCount { get; set; }
    public int UserCount { get; set; }
    public int ProjectCount { get; set; }
    public int SkillCount { get; set; }
    public int HistoryCount { get; set; }
    public int UnconfirmedCount { get; set; }
    public DateTime? LastUpdated { get; set; }
}
