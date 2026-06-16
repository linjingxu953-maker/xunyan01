using System.Runtime.InteropServices;

namespace DesktopMascot.Agent.Interop;

/// <summary>
/// Win32 User32 API P/Invoke 声明 — 统一管理鼠标/键盘/窗口操作的原生调用
/// </summary>
internal static class User32Interop
{
    public const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
    public const uint MOUSEEVENTF_LEFTUP = 0x0004;
    public const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
    public const uint MOUSEEVENTF_RIGHTUP = 0x0010;
    public const uint MOUSEEVENTF_MIDDLEDOWN = 0x0020;
    public const uint MOUSEEVENTF_MIDDLEUP = 0x0040;
    public const uint MOUSEEVENTF_WHEEL = 0x0800;

    public const int SW_MAXIMIZE = 3;
    public const int SW_MINIMIZE = 6;
    public const int SW_RESTORE = 9;

    public const uint INPUT_KEYBOARD = 1;

    public const int SM_CXSCREEN = 0;
    public const int SM_CYSCREEN = 1;

    [DllImport("user32.dll")]
    public static extern bool SetCursorPos(int X, int Y);

    [DllImport("user32.dll")]
    public static extern void mouse_event(uint dwFlags, int dx, int dy, uint dwData, int dwExtraInfo);

    [DllImport("user32.dll")]
    public static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    public static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll")]
    public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    [DllImport("user32.dll")]
    public static extern bool GetCursorPos(ref POINT lpPoint);

    [DllImport("user32.dll")]
    public static extern int GetSystemMetrics(int nIndex);

    [DllImport("user32.dll")]
    public static extern void SendInput(uint nInputs, INPUT[] pInputs, int cbSize);
}

[StructLayout(LayoutKind.Sequential)]
internal struct RECT { public int Left; public int Top; public int Right; public int Bottom; }

[StructLayout(LayoutKind.Sequential)]
internal struct POINT { public int X; public int Y; }

[StructLayout(LayoutKind.Sequential)]
internal struct INPUT { public uint type; public InputUnion u; }

[StructLayout(LayoutKind.Explicit)]
internal struct InputUnion
{
    [FieldOffset(0)] public MOUSEINPUT mi;
    [FieldOffset(0)] public KEYBDINPUT ki;
    [FieldOffset(0)] public HARDWAREINPUT hi;
}

[StructLayout(LayoutKind.Sequential)]
internal struct MOUSEINPUT
{
    public int dx; public int dy; public uint mouseData;
    public uint dwFlags; public uint time; public IntPtr dwExtraInfo;
}

[StructLayout(LayoutKind.Sequential)]
internal struct KEYBDINPUT
{
    public ushort wVk; public ushort wScan;
    public uint dwFlags; public uint time; public IntPtr dwExtraInfo;
}

[StructLayout(LayoutKind.Sequential)]
internal struct HARDWAREINPUT
{
    public uint uMsg; public ushort wParamL; public ushort wParamH;
}

/// <summary>常用虚拟键码</summary>
internal static class VkKeys
{
    public const ushort CTRL = 0x11;
    public const ushort SHIFT = 0x10;
    public const ushort ALT = 0x12;
    public const ushort ENTER = 0x0D;
    public const ushort TAB = 0x09;
    public const ushort ESC = 0x1B;
    public const ushort SPACE = 0x20;
    public const ushort BACKSPACE = 0x08;
    public const ushort DELETE = 0x2E;
    public const ushort HOME = 0x24;
    public const ushort END = 0x23;
    public const ushort PAGEUP = 0x21;
    public const ushort PAGEDOWN = 0x22;
    public const ushort LEFT = 0x25;
    public const ushort UP = 0x26;
    public const ushort RIGHT = 0x27;
    public const ushort DOWN = 0x28;
    public const ushort F1 = 0x70; public const ushort F2 = 0x71; public const ushort F3 = 0x72; public const ushort F4 = 0x73;
    public const ushort F5 = 0x74; public const ushort F6 = 0x75; public const ushort F7 = 0x76; public const ushort F8 = 0x77;
    public const ushort F9 = 0x78; public const ushort F10 = 0x79; public const ushort F11 = 0x7A; public const ushort F12 = 0x7B;
}
