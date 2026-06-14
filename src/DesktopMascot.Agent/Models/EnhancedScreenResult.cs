namespace DesktopMascot.Agent.Models;

/// <summary>
/// 内容类型
/// </summary>
public enum ScreenContentType
{
    Unknown,
    Code,
    Error,
    Document,
    WebPage,
    UI,
    Image,
    Data,
    Chat,
    Terminal
}

/// <summary>
/// 增强的屏幕理解结果
/// </summary>
public class EnhancedScreenResult
{
    public string Identification { get; set; } = string.Empty;
    public string Understanding { get; set; } = string.Empty;
    public string? UserIntent { get; set; }
    public ScreenContentType ContentType { get; set; }
    public string? DetectedLanguage { get; set; }
    public string? ExtractedText { get; set; }
    public List<string> Suggestions { get; set; } = new();
    public bool NeedsAction { get; set; }
    public List<ScreenAction> RecommendedActions { get; set; } = new();
    public float Confidence { get; set; }
    public string? ErrorType { get; set; }
    public string? ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }
    public List<string> KeyElements { get; set; } = new();
    public Dictionary<string, string> Metadata { get; set; } = new();
    public string RawResponse { get; set; } = string.Empty;
}

/// <summary>
/// 屏幕动作执行结果
/// </summary>
public class ScreenActionResult
{
    public bool Success { get; set; }
    public string? Output { get; set; }
    public string? Error { get; set; }
    public string ActionType { get; set; } = string.Empty;
    public TimeSpan Duration { get; set; }
}
