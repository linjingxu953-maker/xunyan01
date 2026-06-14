using System.Runtime.InteropServices;
using System.Text.Json;
using DesktopMascot.Agent.Models;

namespace DesktopMascot.Agent.Tools;

/// <summary>
/// 计算机使用工具 - 鼠标点击、键盘输入、窗口操作
/// </summary>
public class ComputerUseTool : ITool
{
    [DllImport("user32.dll")]
    private static extern bool SetCursorPos(int X, int Y);

    [DllImport("user32.dll")]
    private static extern void mouse_event(uint dwFlags, int dx, int dy, uint dwData, int dwExtraInfo);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    private static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(ref POINT lpPoint);

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);

    [DllImport("user32.dll")]
    private static extern void SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left; public int Top; public int Right; public int Bottom; }
    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int X; public int Y; }
    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT { public uint type; public InputUnion u; }
    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion { [FieldOffset(0)] public MOUSEINPUT mi; [FieldOffset(0)] public KEYBDINPUT ki; [FieldOffset(0)] public HARDWAREINPUT hi; }
    [StructLayout(LayoutKind.Sequential)]
    private struct MOUSEINPUT { public int dx; public int dy; public uint mouseData; public uint dwFlags; public uint time; public IntPtr dwExtraInfo; }
    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT { public ushort wVk; public ushort wScan; public uint dwFlags; public uint time; public IntPtr dwExtraInfo; }
    [StructLayout(LayoutKind.Sequential)]
    private struct HARDWAREINPUT { public uint uMsg; public ushort wParamL; public ushort wParamH; }

    private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
    private const uint MOUSEEVENTF_LEFTUP = 0x0004;
    private const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
    private const uint MOUSEEVENTF_RIGHTUP = 0x0010;
    private const uint MOUSEEVENTF_WHEEL = 0x0800;
    private const int SW_MAXIMIZE = 3;
    private const int SW_MINIMIZE = 6;
    private const int SW_RESTORE = 9;
    private const string TOOL_NAME = "computer_use";

    public string Name => TOOL_NAME;
    public string Description => "控制计算机：鼠标点击、键盘输入、窗口操作。需要用户确认权限。";
    public bool RequiresConfirmation => true;
    public string ConfirmationMessage => "AI 想要操作计算机（鼠标/键盘），是否允许？";
    public string ParametersSchema => """
    {
        "type": "object",
        "properties": {
            "action": {
                "type": "string",
                "enum": ["click","double_click","right_click","type","hotkey","scroll","move_cursor","drag","screenshot","get_cursor_pos","get_screen_size","maximize_window","minimize_window","restore_window","get_active_window"],
                "description": "要执行的操作"
            },
            "x": { "type": "integer" },
            "y": { "type": "integer" },
            "text": { "type": "string" },
            "keys": { "type": "array", "items": { "type": "string" } },
            "delta": { "type": "integer" },
            "end_x": { "type": "integer" },
            "end_y": { "type": "integer" }
        },
        "required": ["action"]
    }
    """;

    public async Task<ToolResult> ExecuteAsync(string arguments, CancellationToken ct = default)
    {
        try
        {
            var doc = JsonDocument.Parse(arguments);
            var root = doc.RootElement;
            if (!root.TryGetProperty("action", out var actionElement))
                return Fail("缺少 action 参数");

            return actionElement.GetString() switch
            {
                "click" => HandleClick(root, false),
                "double_click" => HandleClick(root, true),
                "right_click" => HandleRightClick(root),
                "type" => HandleType(root),
                "hotkey" => HandleHotkey(root),
                "scroll" => HandleScroll(root),
                "move_cursor" => HandleMoveCursor(root),
                "drag" => HandleDrag(root),
                "screenshot" => HandleScreenshot(),
                "get_cursor_pos" => HandleGetCursorPos(),
                "get_screen_size" => HandleGetScreenSize(),
                "maximize_window" => HandleWindowOp(SW_MAXIMIZE),
                "minimize_window" => HandleWindowOp(SW_MINIMIZE),
                "restore_window" => HandleWindowOp(SW_RESTORE),
                "get_active_window" => HandleGetActiveWindow(),
                var a => Fail($"不支持的操作：{a}")
            };
        }
        catch (Exception ex) { return Fail($"操作失败：{ex.Message}"); }
    }

    private static ToolResult HandleClick(JsonElement root, bool dbl)
    {
        if (!root.TryGetProperty("x", out var xEl) || !root.TryGetProperty("y", out var yEl)) return Fail("缺少坐标");
        var x = xEl.GetInt32(); var y = yEl.GetInt32();
        SetCursorPos(x, y); Thread.Sleep(50);
        mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, 0); mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, 0);
        if (dbl) { Thread.Sleep(50); mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, 0); mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, 0); }
        return new ToolResult { Name = TOOL_NAME, Success = true, Content = $"已{(dbl ? "双击" : "单击")} ({x}, {y})" };
    }

    private static ToolResult HandleRightClick(JsonElement root)
    {
        if (!root.TryGetProperty("x", out var xEl) || !root.TryGetProperty("y", out var yEl)) return Fail("缺少坐标");
        var x = xEl.GetInt32(); var y = yEl.GetInt32();
        SetCursorPos(x, y); Thread.Sleep(50);
        mouse_event(MOUSEEVENTF_RIGHTDOWN, 0, 0, 0, 0); mouse_event(MOUSEEVENTF_RIGHTUP, 0, 0, 0, 0);
        return new ToolResult { Name = TOOL_NAME, Success = true, Content = $"已右键 ({x}, {y})" };
    }

    private static ToolResult HandleType(JsonElement root)
    {
        if (!root.TryGetProperty("text", out var textEl)) return Fail("缺少 text");
        var text = textEl.GetString() ?? "";
        foreach (char c in text)
        {
            var input = new INPUT { type = 1, u = new InputUnion { ki = new KEYBDINPUT { wVk = 0, wScan = (ushort)c, dwFlags = 0x0004 } } };
            SendInput(1, new[] { input }, Marshal.SizeOf<INPUT>()); Thread.Sleep(10);
        }
        return new ToolResult { Name = TOOL_NAME, Success = true, Content = $"已输入 {text.Length} 字符" };
    }

    private static ToolResult HandleHotkey(JsonElement root)
    {
        if (!root.TryGetProperty("keys", out var keysEl) || keysEl.ValueKind != JsonValueKind.Array) return Fail("缺少 keys");
        var keys = keysEl.EnumerateArray().Select(k => k.GetString() ?? "").ToList();
        var vks = keys.Select(ParseKey).ToList();
        foreach (var vk in vks) { var input = new INPUT { type = 1, u = new InputUnion { ki = new KEYBDINPUT { wVk = (ushort)vk, dwFlags = 0 } } }; SendInput(1, new[] { input }, Marshal.SizeOf<INPUT>()); Thread.Sleep(10); }
        foreach (var vk in vks.AsEnumerable().Reverse()) { var input = new INPUT { type = 1, u = new InputUnion { ki = new KEYBDINPUT { wVk = (ushort)vk, dwFlags = 0x0002 } } }; SendInput(1, new[] { input }, Marshal.SizeOf<INPUT>()); Thread.Sleep(10); }
        return new ToolResult { Name = TOOL_NAME, Success = true, Content = $"已执行：{string.Join("+", keys)}" };
    }

    private static ToolResult HandleScroll(JsonElement root)
    {
        if (!root.TryGetProperty("x", out var xEl) || !root.TryGetProperty("y", out var yEl)) return Fail("缺少坐标");
        var x = xEl.GetInt32(); var y = yEl.GetInt32();
        var delta = root.TryGetProperty("delta", out var dEl) ? dEl.GetInt32() : -120;
        SetCursorPos(x, y); Thread.Sleep(50);
        mouse_event(MOUSEEVENTF_WHEEL, 0, 0, (uint)delta, 0);
        return new ToolResult { Name = TOOL_NAME, Success = true, Content = $"已滚动 {delta}" };
    }

    private static ToolResult HandleMoveCursor(JsonElement root)
    {
        if (!root.TryGetProperty("x", out var xEl) || !root.TryGetProperty("y", out var yEl)) return Fail("缺少坐标");
        SetCursorPos(xEl.GetInt32(), yEl.GetInt32());
        return new ToolResult { Name = TOOL_NAME, Success = true, Content = $"已移动到 ({xEl.GetInt32()}, {yEl.GetInt32()})" };
    }

    private static ToolResult HandleDrag(JsonElement root)
    {
        if (!root.TryGetProperty("x", out var xEl) || !root.TryGetProperty("y", out var yEl)) return Fail("缺少起始坐标");
        if (!root.TryGetProperty("end_x", out var exEl) || !root.TryGetProperty("end_y", out var eyEl)) return Fail("缺少结束坐标");
        var sx = xEl.GetInt32(); var sy = yEl.GetInt32(); var ex = exEl.GetInt32(); var ey = eyEl.GetInt32();
        SetCursorPos(sx, sy); Thread.Sleep(50);
        mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, 0);
        for (int i = 1; i <= 20; i++) { SetCursorPos(sx + (ex - sx) * i / 20, sy + (ey - sy) * i / 20); Thread.Sleep(10); }
        mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, 0);
        return new ToolResult { Name = TOOL_NAME, Success = true, Content = $"已拖拽 ({sx},{sy}) → ({ex},{ey})" };
    }

    private static ToolResult HandleScreenshot()
    {
        try
        {
            var hwnd = GetForegroundWindow();
            if (hwnd == IntPtr.Zero) return Fail("无法获取窗口");
            GetWindowRect(hwnd, out var rect);
            var w = rect.Right - rect.Left; var h = rect.Bottom - rect.Top;
            if (w <= 0 || h <= 0) return Fail("窗口尺寸无效");
            using var bmp = new System.Drawing.Bitmap(w, h);
            using var g = System.Drawing.Graphics.FromImage(bmp);
            g.CopyFromScreen(rect.Left, rect.Top, 0, 0, new System.Drawing.Size(w, h));
            var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DesktopMascot", "screenshots", $"screenshot_{DateTime.UtcNow:yyyyMMdd_HHmmss}.png");
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            bmp.Save(path, System.Drawing.Imaging.ImageFormat.Png);
            return new ToolResult { Name = TOOL_NAME, Success = true, Content = $"截图：{path}" };
        }
        catch (Exception ex) { return Fail($"截图失败：{ex.Message}"); }
    }

    private static ToolResult HandleGetCursorPos()
    {
        var pt = new POINT(); GetCursorPos(ref pt);
        return new ToolResult { Name = TOOL_NAME, Success = true, Content = $"光标：({pt.X}, {pt.Y})" };
    }

    private static ToolResult HandleGetScreenSize()
    {
        return new ToolResult { Name = TOOL_NAME, Success = true, Content = $"屏幕：{GetSystemMetrics(0)} x {GetSystemMetrics(1)}" };
    }

    private static ToolResult HandleWindowOp(int op)
    {
        var hwnd = GetForegroundWindow();
        if (hwnd == IntPtr.Zero) return Fail("无法获取窗口");
        ShowWindow(hwnd, op);
        var sb = new System.Text.StringBuilder(256); GetWindowText(hwnd, sb, 256);
        return new ToolResult { Name = TOOL_NAME, Success = true, Content = $"已{op switch{SW_MAXIMIZE=>"最大化",SW_MINIMIZE=>"最小化",_=>"还原"}}：{sb}" };
    }

    private static ToolResult HandleGetActiveWindow()
    {
        var hwnd = GetForegroundWindow();
        if (hwnd == IntPtr.Zero) return Fail("无法获取窗口");
        var sb = new System.Text.StringBuilder(256); GetWindowText(hwnd, sb, 256);
        GetWindowThreadProcessId(hwnd, out var pid);
        var proc = "unknown";
        try { proc = System.Diagnostics.Process.GetProcessById((int)pid).ProcessName; } catch { }
        GetWindowRect(hwnd, out var r);
        return new ToolResult { Name = TOOL_NAME, Success = true, Content = $"标题：{sb}\n应用：{proc}\n位置：({r.Left},{r.Top})\n尺寸：{r.Right-r.Left}x{r.Bottom-r.Top}" };
    }

    private static int ParseKey(string key) => key.ToLowerInvariant() switch
    {
        "ctrl" or "control" => 0x11, "shift" => 0x10, "alt" => 0x12, "enter" or "return" => 0x0D,
        "tab" => 0x09, "escape" or "esc" => 0x1B, "space" => 0x20, "backspace" => 0x08,
        "delete" or "del" => 0x2E, "home" => 0x24, "end" => 0x23,
        "pageup" or "page_up" => 0x21, "pagedown" or "page_down" => 0x22,
        "left" => 0x25, "up" => 0x26, "right" => 0x27, "down" => 0x28,
        "f1" => 0x70, "f2" => 0x71, "f3" => 0x72, "f4" => 0x73,
        "f5" => 0x74, "f6" => 0x75, "f7" => 0x76, "f8" => 0x77,
        "f9" => 0x78, "f10" => 0x79, "f11" => 0x7A, "f12" => 0x7B,
        _ when key.Length == 1 => (int)char.ToUpper(key[0]), _ => 0
    };

    private static ToolResult Fail(string error) => new() { Name = TOOL_NAME, Success = false, Error = error };
}
