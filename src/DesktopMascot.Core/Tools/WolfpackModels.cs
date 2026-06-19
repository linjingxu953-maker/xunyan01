namespace DesktopMascot.Core.Tools;

/// <summary>
/// Wolfpack 子 Agent 类型
/// </summary>
public enum WolfpackAgentType
{
    /// <summary>写代码</summary>
    Coder,
    /// <summary>探索/调研</summary>
    Explore,
    /// <summary>规划</summary>
    Plan,
    /// <summary>验证/测试</summary>
    Verify,
    /// <summary>写文档</summary>
    Writer
}

/// <summary>
/// Wolfpack 子 Agent 定义
/// </summary>
public class WolfpackAgent
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public WolfpackAgentType Type { get; set; }
    public string Task { get; set; } = "";
    public string? Context { get; set; }
    public WolfpackAgentStatus Status { get; set; } = WolfpackAgentStatus.Pending;
    public string? Result { get; set; }
    public string? Error { get; set; }
    public int TokensUsed { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    public List<string> DependsOn { get; set; } = new();
    public List<string> Dependents { get; set; } = new();
}

/// <summary>
/// Wolfpack 任务包 — 多个子 Agent 并行执行
/// </summary>
public class WolfpackPack
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public List<WolfpackAgent> Agents { get; set; } = new();
    public WolfpackPackStatus Status { get; set; } = WolfpackPackStatus.Pending;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    public bool AutoApprove { get; set; }
}

public enum WolfpackAgentStatus
{
    Pending,
    Running,
    Completed,
    Failed,
    Blocked
}

public enum WolfpackPackStatus
{
    Pending,
    Running,
    PartiallyCompleted,
    Completed,
    Failed
}
