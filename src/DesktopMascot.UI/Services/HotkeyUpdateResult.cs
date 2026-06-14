namespace DesktopMascot.UI.Services;

public sealed class HotkeyUpdateResult
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
    public bool ChatRegistered { get; init; }
    public bool ScreenSelectionRegistered { get; init; }

    public static HotkeyUpdateResult Succeeded(
        string message,
        bool chatRegistered,
        bool screenSelectionRegistered)
    {
        return new HotkeyUpdateResult
        {
            Success = true,
            Message = message,
            ChatRegistered = chatRegistered,
            ScreenSelectionRegistered = screenSelectionRegistered
        };
    }

    public static HotkeyUpdateResult Failed(
        string message,
        bool chatRegistered,
        bool screenSelectionRegistered)
    {
        return new HotkeyUpdateResult
        {
            Success = false,
            Message = message,
            ChatRegistered = chatRegistered,
            ScreenSelectionRegistered = screenSelectionRegistered
        };
    }
}
