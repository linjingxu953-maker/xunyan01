namespace DesktopMascot.UI.Services;

public sealed record AudioPlaybackResult(bool Success, string Message, string? Error = null);
