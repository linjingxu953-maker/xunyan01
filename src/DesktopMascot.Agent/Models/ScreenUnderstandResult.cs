namespace DesktopMascot.Agent.Models;

/// <summary>
/// 屏幕理解结果
/// </summary>
public class ScreenUnderstandResult
{
    /// <summary>识别结果 - 这是什么</summary>
    public string Identification { get; set; } = string.Empty;

    /// <summary>理解结果 - 用户可能想做什么</summary>
    public string Understanding { get; set; } = string.Empty;

    /// <summary>用户意图（未理解时返回）</summary>
    public string? UserIntent { get; set; }

    /// <summary>建议操作列表</summary>
    public List<string> Suggestions { get; set; } = new();

    /// <summary>是否需要执行操作</summary>
    public bool NeedsAction { get; set; }

    /// <summary>建议执行的操作</summary>
    public List<ScreenAction> RecommendedActions { get; set; } = new();

    /// <summary>置信度（0-1）</summary>
    public float Confidence { get; set; }

    /// <summary>原始 LLM 响应</summary>
    public string RawResponse { get; set; } = string.Empty;
}

/// <summary>
/// 屏幕操作建议
/// </summary>
public class ScreenAction
{
    /// <summary>操作名称</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>操作描述</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>操作类型（read_file, run_command, open_url, copy_text 等）</summary>
    public string ActionType { get; set; } = string.Empty;

    /// <summary>操作参数</summary>
    public Dictionary<string, string> Parameters { get; set; } = new();

    /// <summary>风险等级（low, medium, high）</summary>
    public string RiskLevel { get; set; } = "low";
}
