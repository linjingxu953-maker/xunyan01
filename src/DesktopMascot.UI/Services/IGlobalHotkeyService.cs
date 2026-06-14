namespace DesktopMascot.UI.Services;

public interface IGlobalHotkeyService : IDisposable
{
    event EventHandler? HotkeyPressed;
    event EventHandler? ScreenSelectionHotkeyPressed;
    event EventHandler? HotkeysChanged;
    string DisplayText { get; }
    string ScreenSelectionDisplayText { get; }
    HotkeyGesture ChatHotkey { get; }
    HotkeyGesture ScreenSelectionHotkey { get; }
    bool IsDefaultHotkeyRegistered { get; }
    bool IsScreenSelectionHotkeyRegistered { get; }
    bool RegisterDefaultHotkey();
    HotkeyUpdateResult UpdateHotkeys(HotkeyGesture chatHotkey, HotkeyGesture screenSelectionHotkey);
    HotkeyUpdateResult ResetHotkeys();
}
