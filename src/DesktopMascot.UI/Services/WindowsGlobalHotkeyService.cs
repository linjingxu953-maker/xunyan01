using System.Runtime.InteropServices;
using System.Text.Json;
using Avalonia.Threading;

namespace DesktopMascot.UI.Services;

public sealed class WindowsGlobalHotkeyService : IGlobalHotkeyService
{
    private const int ChatHotkeyId = 0x4D49;
    private const int ScreenSelectionHotkeyId = 0x4D4A;
    private const uint WmHotkey = 0x0312;
    private const uint ModAlt = 0x0001;
    private const uint ModControl = 0x0002;
    private const uint ModShift = 0x0004;
    private const uint ModWin = 0x0008;
    private const uint ModNoRepeat = 0x4000;
    private static readonly IntPtr HwndMessage = new(-3);
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _filePath;
    private WndProcDelegate? _wndProc;
    private IntPtr _hwnd;
    private IntPtr _hInstance;
    private string? _className;
    private HotkeyGesture _chatHotkey;
    private HotkeyGesture _screenSelectionHotkey;
    private bool _chatRegistered;
    private bool _screenSelectionRegistered;

    public WindowsGlobalHotkeyService()
    {
        _filePath = BuildConfigPath();
        (_chatHotkey, _screenSelectionHotkey) = LoadConfiguration();
    }

    public event EventHandler? HotkeyPressed;
    public event EventHandler? ScreenSelectionHotkeyPressed;
    public event EventHandler? HotkeysChanged;

    public string DisplayText => _chatHotkey.DisplayText;
    public string ScreenSelectionDisplayText => _screenSelectionHotkey.DisplayText;
    public HotkeyGesture ChatHotkey => _chatHotkey.Clone();
    public HotkeyGesture ScreenSelectionHotkey => _screenSelectionHotkey.Clone();
    public bool IsDefaultHotkeyRegistered => _chatRegistered;
    public bool IsScreenSelectionHotkeyRegistered => _screenSelectionRegistered;

    public bool RegisterDefaultHotkey()
    {
        if (!OperatingSystem.IsWindows())
            return false;

        if (_chatRegistered && _screenSelectionRegistered)
            return true;

        if (_hwnd == IntPtr.Zero && !CreateNativeWindow())
            return false;

        if (!_chatRegistered)
        {
            _chatRegistered = TryRegisterHotkey(ChatHotkeyId, _chatHotkey);
        }

        if (!_screenSelectionRegistered)
        {
            _screenSelectionRegistered = TryRegisterHotkey(ScreenSelectionHotkeyId, _screenSelectionHotkey);
        }

        if (!_chatRegistered && !_screenSelectionRegistered)
        {
            CleanupNativeWindow();
        }

        return _chatRegistered || _screenSelectionRegistered;
    }

    public HotkeyUpdateResult UpdateHotkeys(HotkeyGesture chatHotkey, HotkeyGesture screenSelectionHotkey)
    {
        if (!ValidateHotkeyPair(chatHotkey, screenSelectionHotkey, out var validationError))
        {
            return HotkeyUpdateResult.Failed(
                validationError,
                _chatRegistered,
                _screenSelectionRegistered);
        }

        var previousChat = _chatHotkey.Clone();
        var previousScreenSelection = _screenSelectionHotkey.Clone();

        UnregisterHotkeys();
        _chatHotkey = chatHotkey.Clone();
        _screenSelectionHotkey = screenSelectionHotkey.Clone();

        if (!OperatingSystem.IsWindows())
        {
            SaveConfiguration();
            HotkeysChanged?.Invoke(this, EventArgs.Empty);
            return HotkeyUpdateResult.Succeeded(
                "快捷键已保存。当前平台不支持 Windows 全局热键注册。",
                chatRegistered: false,
                screenSelectionRegistered: false);
        }

        RegisterDefaultHotkey();

        if (_chatRegistered && _screenSelectionRegistered)
        {
            SaveConfiguration();
            HotkeysChanged?.Invoke(this, EventArgs.Empty);
            return HotkeyUpdateResult.Succeeded(
                "快捷键已保存并注册。",
                _chatRegistered,
                _screenSelectionRegistered);
        }

        var failedMessage = CreateRegistrationFailureMessage();
        UnregisterHotkeys();
        _chatHotkey = previousChat;
        _screenSelectionHotkey = previousScreenSelection;
        RegisterDefaultHotkey();

        return HotkeyUpdateResult.Failed(
            $"{failedMessage} 已回滚到上一次可用配置。",
            _chatRegistered,
            _screenSelectionRegistered);
    }

    public HotkeyUpdateResult ResetHotkeys()
    {
        return UpdateHotkeys(HotkeyGesture.DefaultChat(), HotkeyGesture.DefaultScreenSelection());
    }

    public void Dispose()
    {
        CleanupNativeWindow();
        GC.SuppressFinalize(this);
    }

    private bool TryRegisterHotkey(int id, HotkeyGesture gesture)
    {
        if (!gesture.TryGetVirtualKey(out var virtualKey))
            return false;

        return RegisterHotKey(_hwnd, id, GetModifierFlags(gesture), virtualKey);
    }

    private bool CreateNativeWindow()
    {
        _className = $"DesktopMascotHotkeyWindow_{Guid.NewGuid():N}";
        _hInstance = GetModuleHandle(null);
        _wndProc = WndProc;

        var windowClass = new WindowClassEx
        {
            Size = (uint)Marshal.SizeOf<WindowClassEx>(),
            WindowProc = Marshal.GetFunctionPointerForDelegate(_wndProc),
            Instance = _hInstance,
            ClassName = _className
        };

        if (RegisterClassEx(ref windowClass) == 0)
        {
            CleanupNativeWindow();
            return false;
        }

        _hwnd = CreateWindowEx(
            0,
            _className,
            "DesktopMascotHotkeyWindow",
            0,
            0,
            0,
            0,
            0,
            HwndMessage,
            IntPtr.Zero,
            _hInstance,
            IntPtr.Zero);

        if (_hwnd != IntPtr.Zero)
        {
            return true;
        }

        CleanupNativeWindow();
        return false;
    }

    private IntPtr WndProc(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam)
    {
        if (message == WmHotkey)
        {
            switch (wParam.ToInt32())
            {
                case ChatHotkeyId:
                    Dispatcher.UIThread.Post(() => HotkeyPressed?.Invoke(this, EventArgs.Empty));
                    return IntPtr.Zero;
                case ScreenSelectionHotkeyId:
                    Dispatcher.UIThread.Post(() => ScreenSelectionHotkeyPressed?.Invoke(this, EventArgs.Empty));
                    return IntPtr.Zero;
            }
        }

        return DefWindowProc(hwnd, message, wParam, lParam);
    }

    private void CleanupNativeWindow()
    {
        UnregisterHotkeys();

        if (_hwnd != IntPtr.Zero)
        {
            DestroyWindow(_hwnd);
            _hwnd = IntPtr.Zero;
        }

        if (!string.IsNullOrWhiteSpace(_className) && _hInstance != IntPtr.Zero)
        {
            UnregisterClass(_className, _hInstance);
        }

        _className = null;
        _hInstance = IntPtr.Zero;
        _wndProc = null;
    }

    private void UnregisterHotkeys()
    {
        if (_chatRegistered && _hwnd != IntPtr.Zero)
        {
            UnregisterHotKey(_hwnd, ChatHotkeyId);
        }

        if (_screenSelectionRegistered && _hwnd != IntPtr.Zero)
        {
            UnregisterHotKey(_hwnd, ScreenSelectionHotkeyId);
        }

        _chatRegistered = false;
        _screenSelectionRegistered = false;
    }

    private void SaveConfiguration()
    {
        try
        {
            var directory = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var configuration = new HotkeyConfiguration
            {
                Chat = _chatHotkey.Clone(),
                ScreenSelection = _screenSelectionHotkey.Clone()
            };
            var json = JsonSerializer.Serialize(configuration, SerializerOptions);
            File.WriteAllText(_filePath, json);
        }
        catch
        {
            // Hotkey persistence must not block app startup or shutdown.
        }
    }

    private (HotkeyGesture Chat, HotkeyGesture ScreenSelection) LoadConfiguration()
    {
        var defaultChat = HotkeyGesture.DefaultChat();
        var defaultScreenSelection = HotkeyGesture.DefaultScreenSelection();

        try
        {
            if (!File.Exists(_filePath))
                return (defaultChat, defaultScreenSelection);

            var json = File.ReadAllText(_filePath);
            var configuration = JsonSerializer.Deserialize<HotkeyConfiguration>(json, SerializerOptions);
            var chat = configuration?.Chat ?? defaultChat;
            var screenSelection = configuration?.ScreenSelection ?? defaultScreenSelection;

            return ValidateHotkeyPair(chat, screenSelection, out _)
                ? (chat.Clone(), screenSelection.Clone())
                : (defaultChat, defaultScreenSelection);
        }
        catch
        {
            return (defaultChat, defaultScreenSelection);
        }
    }

    private static bool ValidateHotkeyPair(
        HotkeyGesture chatHotkey,
        HotkeyGesture screenSelectionHotkey,
        out string error)
    {
        if (!chatHotkey.IsValid(out var chatError))
        {
            error = $"聊天唤起快捷键无效：{chatError}";
            return false;
        }

        if (!screenSelectionHotkey.IsValid(out var screenSelectionError))
        {
            error = $"屏幕圈选快捷键无效：{screenSelectionError}";
            return false;
        }

        if (chatHotkey.Equals(screenSelectionHotkey))
        {
            error = "聊天唤起和屏幕圈选不能使用同一个快捷键。";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private string CreateRegistrationFailureMessage()
    {
        if (!_chatRegistered && !_screenSelectionRegistered)
        {
            return "两个快捷键都注册失败，可能已被系统或其他软件占用。";
        }

        if (!_chatRegistered)
        {
            return $"聊天唤起快捷键 {DisplayText} 注册失败，可能已被占用。";
        }

        return $"屏幕圈选快捷键 {ScreenSelectionDisplayText} 注册失败，可能已被占用。";
    }

    private static uint GetModifierFlags(HotkeyGesture gesture)
    {
        var flags = ModNoRepeat;

        if (gesture.Control)
        {
            flags |= ModControl;
        }

        if (gesture.Alt)
        {
            flags |= ModAlt;
        }

        if (gesture.Shift)
        {
            flags |= ModShift;
        }

        if (gesture.Win)
        {
            flags |= ModWin;
        }

        return flags;
    }

    private static string BuildConfigPath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        if (string.IsNullOrWhiteSpace(appData))
        {
            appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        }

        return Path.Combine(appData, "DesktopAIMascot", "config", "hotkeys.json");
    }

    private sealed class HotkeyConfiguration
    {
        public HotkeyGesture? Chat { get; set; }
        public HotkeyGesture? ScreenSelection { get; set; }
    }

    private delegate IntPtr WndProcDelegate(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WindowClassEx
    {
        public uint Size;
        public uint Style;
        public IntPtr WindowProc;
        public int ClassExtra;
        public int WindowExtra;
        public IntPtr Instance;
        public IntPtr Icon;
        public IntPtr Cursor;
        public IntPtr Background;
        public string? MenuName;
        public string ClassName;
        public IntPtr IconSmall;
    }

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern ushort RegisterClassEx(ref WindowClassEx windowClass);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool UnregisterClass(string className, IntPtr instance);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr CreateWindowEx(
        uint extendedStyle,
        string className,
        string windowName,
        uint style,
        int x,
        int y,
        int width,
        int height,
        IntPtr parent,
        IntPtr menu,
        IntPtr instance,
        IntPtr parameter);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyWindow(IntPtr hwnd);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hwnd, int id, uint modifiers, uint virtualKey);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hwnd, int id);

    [DllImport("user32.dll")]
    private static extern IntPtr DefWindowProc(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr GetModuleHandle(string? moduleName);
}
