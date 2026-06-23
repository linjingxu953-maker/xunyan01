namespace DesktopMascot.UI.Services;

public sealed record VoiceInputRecognitionResult(
    bool Success,
    string Text,
    string? AudioFilePath,
    string Message,
    string? Error = null);
