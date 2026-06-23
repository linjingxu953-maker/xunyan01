namespace DesktopMascot.UI.Services;

public sealed record VoiceInputStartResult(
    bool Success,
    string Message,
    string? Error = null);
