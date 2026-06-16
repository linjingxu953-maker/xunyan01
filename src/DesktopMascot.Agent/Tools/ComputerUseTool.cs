using System.Runtime.InteropServices;
using System.Text.Json;
using DesktopMascot.Agent.Interop;
using DesktopMascot.Agent.Models;

namespace DesktopMascot.Agent.Tools;

/// <summary>
/// 计算机使用工具 - 鼠标点击、键盘输入、窗口操作
/// 底层 Win32 P/Invoke 委托到 User32Interop
/// </summary>
public class ComputerUseTool : ITool
{
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
                "maximize_window" => HandleWindowOp(User32Interop.SW_MAXIMIZE),
                "minimize_window" => HandleWindowOp(User32Interop.SW_MINIMIZE),
                "restore_window" => HandleWindowOp(User32Interop.SW_RESTORE),
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
        User32Interop.SetCursorPos(x, y); Thread.Sleep(50);
        User32Interop.mouse_event(User32Interop.MOUSEEVENTF_LEFTDOWN, 0, 0, 0, 0);
        User32Interop.mouse_event(User32Interop.MOUSEEVENTF_LEFTUP, 0, 0, 0, 0);
        if (dbl) { Thread.Sleep(50); User32Interop.mouse_event(User32Interop.MOUSEEVENTF_LEFTDOWN, 0, 0, 0, 0); User32Interop.mouse_event(User32Interop.MOUSEEVENTF_LEFTUP, 0, 0, 0, 0); }
        return new ToolResult { Name = TOOL_NAME, Success = true, Content = $"已{(dbl ? "双击" : "单击")} ({x}, {y})" };
    }

    private static ToolResult HandleRightClick(JsonElement root)
    {
        if (!root.TryGetProperty("x", out var xEl) || !root.TryGetProperty("y", out var yEl)) return Fail("缺少坐标");
        var x = xEl.GetInt32(); var y = yEl.GetInt32();
        User32Interop.SetCursorPos(x, y); Thread.Sleep(50);
        User32Interop.mouse_event(User32Interop.MOUSEEVENTF_RIGHTDOWN, 0, 0, 0, 0);
        User32Interop.mouse_event(User32Interop.MOUSEEVENTF_RIGHTUP, 0, 0, 0, 0);
        return new ToolResult { Name = TOOL_NAME, Success = true, Content = $"已右键 ({x}, {y})" };
    }

    private static ToolResult HandleType(JsonElement root)
    {
        if (!root.TryGetProperty("text", out var textEl)) return Fail("缺少 text");
        var text = textEl.GetString() ?? "";
        foreach (char c in text)
        {
            var input = new INPUT { type = User32Interop.INPUT_KEYBOARD, u = new InputUnion { ki = new KEYBDINPUT { wVk = 0, wScan = (ushort)c, dwFlags = 0x0004 } } };
            User32Interop.SendInput(1, new[] { input }, Marshal.SizeOf<INPUT>()); Thread.Sleep(10);
        }
        return new ToolResult { Name = TOOL_NAME, Success = true, Content = $"已输入 {text.Length} 字符" };
    }

    private static ToolResult HandleHotkey(JsonElement root)
    {
        if (!root.TryGetProperty("keys", out var keysEl) || keysEl.ValueKind != JsonValueKind.Array) return Fail("缺少 keys");
        var keys = keysEl.EnumerateArray().Select(k => k.GetString() ?? "").ToList();
        var vks = keys.Select(ParseKey).ToList();
        foreach (var vk in vks) { var input = new INPUT { type = User32Interop.INPUT_KEYBOARD, u = new InputUnion { ki = new KEYBDINPUT { wVk = (ushort)vk, dwFlags = 0 } } }; User32Interop.SendInput(1, new[] { input }, Marshal.SizeOf<INPUT>()); Thread.Sleep(10); }
        foreach (var vk in vks.AsEnumerable().Reverse()) { var input = new INPUT { type = User32Interop.INPUT_KEYBOARD, u = new InputUnion { ki = new KEYBDINPUT { wVk = (ushort)vk, dwFlags = 0x0002 } } }; User32Interop.SendInput(1, new[] { input }, Marshal.SizeOf<INPUT>()); Thread.Sleep(10); }
        return new ToolResult { Name = TOOL_NAME, Success = true, Content = $"已执行：{string.Join("+", keys)}" };
    }

    private static ToolResult HandleScroll(JsonElement root)
    {
        if (!root.TryGetProperty("x", out var xEl) || !root.TryGetProperty("y", out var yEl)) return Fail("缺少坐标");
        var x = xEl.GetInt32(); var y = yEl.GetInt32();
        var delta = root.TryGetProperty("delta", out var dEl) ? dEl.GetInt32() : -120;
        User32Interop.SetCursorPos(x, y); Thread.Sleep(50);
        User32Interop.mouse_event(User32Interop.MOUSEEVENTF_WHEEL, 0, 0, (uint)delta, 0);
        return new ToolResult { Name = TOOL_NAME, Success = true, Content = $"已滚动 {delta}" };
    }

    private static ToolResult HandleMoveCursor(JsonElement root)
    {
        if (!root.TryGetProperty("x", out var xEl) || !root.TryGetProperty("y", out var yEl)) return Fail("缺少坐标");
        User32Interop.SetCursorPos(xEl.GetInt32(), yEl.GetInt32());
        return new ToolResult { Name = TOOL_NAME, Success = true, Content = $"已移动到 ({xEl.GetInt32()}, {yEl.GetInt32()})" };
    }

    private static ToolResult HandleDrag(JsonElement root)
    {
        if (!root.TryGetProperty("x", out var xEl) || !root.TryGetProperty("y", out var yEl)) return Fail("缺少起始坐标");
        if (!root.TryGetProperty("end_x", out var exEl) || !root.TryGetProperty("end_y", out var eyEl)) return Fail("缺少结束坐标");
        var sx = xEl.GetInt32(); var sy = yEl.GetInt32(); var ex = exEl.GetInt32(); var ey = eyEl.GetInt32();
        User32Interop.SetCursorPos(sx, sy); Thread.Sleep(50);
        User32Interop.mouse_event(User32Interop.MOUSEEVENTF_LEFTDOWN, 0, 0, 0, 0);
        for (int i = 1; i <= 20; i++) { User32Interop.SetCursorPos(sx + (ex - sx) * i / 20, sy + (ey - sy) * i / 20); Thread.Sleep(10); }
        User32Interop.mouse_event(User32Interop.MOUSEEVENTF_LEFTUP, 0, 0, 0, 0);
        return new ToolResult { Name = TOOL_NAME, Success = true, Content = $"已拖拽 ({sx},{sy}) → ({ex},{ey})" };
    }

    private static ToolResult HandleScreenshot()
    {
        try
        {
            var hwnd = User32Interop.GetForegroundWindow();
            if (hwnd == IntPtr.Zero) return Fail("无法获取窗口");
            User32Interop.GetWindowRect(hwnd, out var rect);
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
        var pt = new POINT();
        User32Interop.GetCursorPos(ref pt);
        return new ToolResult { Name = TOOL_NAME, Success = true, Content = $"光标：({pt.X}, {pt.Y})" };
    }

    private static ToolResult HandleGetScreenSize()
    {
        return new ToolResult { Name = TOOL_NAME, Success = true, Content = $"屏幕：{User32Interop.GetSystemMetrics(User32Interop.SM_CXSCREEN)} x {User32Interop.GetSystemMetrics(User32Interop.SM_CYSCREEN)}" };
    }

    private static ToolResult HandleWindowOp(int op)
    {
        var hwnd = User32Interop.GetForegroundWindow();
        if (hwnd == IntPtr.Zero) return Fail("无法获取窗口");
        User32Interop.ShowWindow(hwnd, op);
        var sb = new System.Text.StringBuilder(256);
        User32Interop.GetWindowText(hwnd, sb, 256);
        return new ToolResult { Name = TOOL_NAME, Success = true, Content = $"已{op switch { User32Interop.SW_MAXIMIZE => "最大化", User32Interop.SW_MINIMIZE => "最小化", _ => "还原" }}：{sb}" };
    }

    private static ToolResult HandleGetActiveWindow()
    {
        var hwnd = User32Interop.GetForegroundWindow();
        if (hwnd == IntPtr.Zero) return Fail("无法获取窗口");
        var sb = new System.Text.StringBuilder(256);
        User32Interop.GetWindowText(hwnd, sb, 256);
        User32Interop.GetWindowThreadProcessId(hwnd, out var pid);
        var proc = "unknown";
        try { proc = System.Diagnostics.Process.GetProcessById((int)pid).ProcessName; } catch { }
        User32Interop.GetWindowRect(hwnd, out var r);
        return new ToolResult { Name = TOOL_NAME, Success = true, Content = $"标题：{sb}\n应用：{proc}\n位置：({r.Left},{r.Top})\n尺寸：{r.Right - r.Left}x{r.Bottom - r.Top}" };
    }

    private static int ParseKey(string key) => key.ToLowerInvariant() switch
    {
        "ctrl" or "control" => VkKeys.CTRL, "shift" => VkKeys.SHIFT, "alt" => VkKeys.ALT,
        "enter" or "return" => VkKeys.ENTER, "tab" => VkKeys.TAB, "escape" or "esc" => VkKeys.ESC,
        "space" => VkKeys.SPACE, "backspace" => VkKeys.BACKSPACE, "delete" or "del" => VkKeys.DELETE,
        "home" => VkKeys.HOME, "end" => VkKeys.END,
        "pageup" or "page_up" => VkKeys.PAGEUP, "pagedown" or "page_down" => VkKeys.PAGEDOWN,
        "left" => VkKeys.LEFT, "up" => VkKeys.UP, "right" => VkKeys.RIGHT, "down" => VkKeys.DOWN,
        "f1" => VkKeys.F1, "f2" => VkKeys.F2, "f3" => VkKeys.F3, "f4" => VkKeys.F4,
        "f5" => VkKeys.F5, "f6" => VkKeys.F6, "f7" => VkKeys.F7, "f8" => VkKeys.F8,
        "f9" => VkKeys.F9, "f10" => VkKeys.F10, "f11" => VkKeys.F11, "f12" => VkKeys.F12,
        _ when key.Length == 1 => (int)char.ToUpper(key[0]), _ => 0
    };

    private static ToolResult Fail(string error) => new() { Name = TOOL_NAME, Success = false, Error = error };
}
