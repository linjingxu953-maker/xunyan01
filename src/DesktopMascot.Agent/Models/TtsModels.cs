namespace DesktopMascot.Agent.Models;

/// <summary>
/// 文本转语音结果
/// </summary>
public class TextToSpeechResult
{
    public bool Success { get; set; }
    public string? AudioFilePath { get; set; }
    public byte[]? AudioData { get; set; }
    public string Voice { get; set; } = string.Empty;
    public float Speed { get; set; } = 1.0f;
    public TimeSpan? Duration { get; set; }
    public string? Error { get; set; }
}

/// <summary>
/// TTS 语音选项
/// </summary>
public enum TtsVoice
{
    /// <summary>Alloy - 平衡</summary>
    Alloy,
    /// <summary>echo - 低沉</summary>
    Echo,
    /// <summary>fable - 叙事</summary>
    Fable,
    /// <summary>onyx - 深沉</summary>
    Onyx,
    /// <summary>nova - 活泼</summary>
    Nova,
    /// <summary>shimmer - 柔和</summary>
    Shimmer
}
