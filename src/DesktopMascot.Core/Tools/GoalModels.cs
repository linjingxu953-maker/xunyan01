namespace DesktopMascot.Core.Tools;

/// <summary>
/// Goal 循环模型 — 自主目标驱动执行
/// </summary>
public class GoalDefinition
{
    /// <summary>目标 ID</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];

    /// <summary>目标描述</summary>
    public string Description { get; set; } = "";

    /// <summary>目标完成的判断标准</summary>
    public string SuccessCriteria { get; set; } = "";

    /// <summary>目标状态</summary>
    public GoalStatus Status { get; set; } = GoalStatus.Pending;

    /// <summary>当前迭代轮次</summary>
    public int CurrentIteration { get; set; }

    /// <summary>最大迭代轮次</summary>
    public int MaxIterations { get; set; } = 10;

    /// <summary>已消耗 Token 数</summary>
    public int TokensUsed { get; set; }

    /// <summary>最大 Token 数</summary>
    public int MaxTokens { get; set; } = 50000;

    /// <summary>最大执行时间（秒）</summary>
    public int MaxTimeSeconds { get; set; } = 300;

    /// <summary>开始时间</summary>
    public DateTime StartedAt { get; set; }

    /// <summary>完成时间</summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>每轮执行记录</summary>
    public List<GoalIteration> Iterations { get; set; } = new();

    /// <summary>最终结果</summary>
    public string? FinalResult { get; set; }

    /// <summary>裁判评分（0-100）</summary>
    public int JudgeScore { get; set; }

    /// <summary>裁判评语</summary>
    public string? JudgeComment { get; set; }
}

public class GoalIteration
{
    public int Round { get; set; }
    public DateTime Timestamp { get; set; }
    public string Action { get; set; } = "";
    public string Result { get; set; } = "";
    public bool Success { get; set; }
    public int TokensUsed { get; set; }
    public string? Error { get; set; }
}

public enum GoalStatus
{
    Pending,
    Running,
    Paused,
    Completed,
    Failed,
    Cancelled
}
