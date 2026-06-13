namespace DesktopMascot.UI.Services;

public interface IGlobalHotkeyService : IDisposable
{
    event EventHandler? HotkeyPressed;
    string DisplayText { get; }
    bool RegisterDefaultHotkey();
}
