namespace DesktopMascot.UI.Services;

public sealed record TextToSpeechPreviewResult(
    bool Success,
    string? AudioFilePath,
    string Message,
    string? Error = null);
