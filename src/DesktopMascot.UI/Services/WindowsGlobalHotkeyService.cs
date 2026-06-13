using System.Runtime.InteropServices;
using Avalonia.Threading;

namespace DesktopMascot.UI.Services;

public sealed class WindowsGlobalHotkeyService : IGlobalHotkeyService
{
    private const int HotkeyId = 0x4D49;
    private const uint WmHotkey = 0x0312;
    private const uint ModAlt = 0x0001;
    private const uint ModControl = 0x0002;
    private const uint ModNoRepeat = 0x4000;
    private const uint VkSpace = 0x20;
    private static readonly IntPtr HwndMessage = new(-3);

    private WndProcDelegate? _wndProc;
    private IntPtr _hwnd;
    private IntPtr _hInstance;
    private string? _className;
    private bool _registered;

    public event EventHandler? HotkeyPressed;

    public string DisplayText => "Ctrl+Alt+Space";

    public bool RegisterDefaultHotkey()
    {
        if (!OperatingSystem.IsWindows() || _registered)
            return false;

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

        if (_hwnd == IntPtr.Zero)
        {
            CleanupNativeWindow();
            return false;
        }

        _registered = RegisterHotKey(_hwnd, HotkeyId, ModControl | ModAlt | ModNoRepeat, VkSpace);
        if (!_registered)
        {
            CleanupNativeWindow();
        }

        return _registered;
    }

    public void Dispose()
    {
        CleanupNativeWindow();
        GC.SuppressFinalize(this);
    }

    private IntPtr WndProc(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam)
    {
        if (message == WmHotkey && wParam.ToInt32() == HotkeyId)
        {
            Dispatcher.UIThread.Post(() => HotkeyPressed?.Invoke(this, EventArgs.Empty));
            return IntPtr.Zero;
        }

        return DefWindowProc(hwnd, message, wParam, lParam);
    }

    private void CleanupNativeWindow()
    {
        if (_registered && _hwnd != IntPtr.Zero)
        {
            UnregisterHotKey(_hwnd, HotkeyId);
        }

        _registered = false;

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
