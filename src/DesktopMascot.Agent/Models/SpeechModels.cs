namespace DesktopMascot.Agent.Models;

/// <summary>
/// 语音识别结果
/// </summary>
public class SpeechRecognitionResult
{
    public bool Success { get; set; }
    public string Text { get; set; } = string.Empty;
    public string Language { get; set; } = string.Empty;
    public float Confidence { get; set; }
    public TimeSpan Duration { get; set; }
    public string? Error { get; set; }
    public List<SpeechSegment> Segments { get; set; } = new();
}

/// <summary>
/// 语音片段
/// </summary>
public class SpeechSegment
{
    public string Text { get; set; } = string.Empty;
    public float StartSeconds { get; set; }
    public float EndSeconds { get; set; }
    public float Confidence { get; set; }
}
