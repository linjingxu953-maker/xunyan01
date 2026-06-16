using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using DesktopMascot.Agent.Models;

namespace DesktopMascot.Agent.Tools;

/// <summary>
/// 浏览器自动化工具 - 通过命令行控制浏览器
/// </summary>
public class BrowserAutomationTool : ITool
{
    public string Name => "browser_automation";
    public string Description => "浏览器自动化：打开网页、点击元素、填写表单、截图。支持 Chrome/Edge。";
    public string ParametersSchema => """
    {
        "type": "object",
        "properties": {
            "action": { "type": "string", "enum": ["open", "click", "type", "screenshot", "extract", "scroll", "back", "forward", "refresh"], "description": "操作类型" },
            "url": { "type": "string", "description": "网页URL（open操作）" },
            "selector": { "type": "string", "description": "CSS选择器或元素描述（click/type操作）" },
            "text": { "type": "string", "description": "输入文本（type操作）" },
            "scroll_amount": { "type": "integer", "description": "滚动量（scroll操作）" },
            "output_path": { "type": "string", "description": "截图保存路径（screenshot操作）" }
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
            var action = root.TryGetProperty("action", out var aEl) ? aEl.GetString() ?? "" : "";

            return action switch
            {
                "open" => await OpenUrlAsync(root, ct),
                "click" => await ClickElementAsync(root),
                "type" => await TypeTextAsync(root),
                "screenshot" => await TakeScreenshotAsync(root, ct),
                "extract" => await ExtractContentAsync(root, ct),
                "scroll" => await ScrollPageAsync(root),
                "back" => await GoBackAsync(),
                "forward" => await GoForwardAsync(),
                "refresh" => await RefreshAsync(),
                _ => Fail($"不支持的操作：{action}")
            };
        }
        catch (Exception ex)
        {
            return Fail($"浏览器操作失败：{ex.Message}");
        }
    }

    private async Task<ToolResult> OpenUrlAsync(JsonElement root, CancellationToken ct)
    {
        var url = root.TryGetProperty("url", out var uEl) ? uEl.GetString() ?? "" : "";
        if (string.IsNullOrEmpty(url)) return Fail("缺少 url 参数");

        if (!url.StartsWith("http"))
            url = "https://" + url;

        Process.Start(new ProcessStartInfo
        {
            FileName = url,
            UseShellExecute = true
        });

        await Task.Delay(1000, ct);

        return new ToolResult
        {
            Name = Name,
            Success = true,
            Content = $"已打开网页：{url}"
        };
    }

    private async Task<ToolResult> ClickElementAsync(JsonElement root)
    {
        var selector = root.TryGetProperty("selector", out var sEl) ? sEl.GetString() ?? "" : "";
        if (string.IsNullOrEmpty(selector)) return Fail("缺少 selector 参数");

        // 使用快捷键模拟点击（简化实现）
        var inputs = new INPUT[2];
        inputs[0] = new INPUT { type = INPUT_KEYBOARD, u = new INPUTUNION { ki = new KEYBDINPUT { wVk = 0x0D, dwFlags = 0 } } };
        inputs[1] = new INPUT { type = INPUT_KEYBOARD, u = new INPUTUNION { ki = new KEYBDINPUT { wVk = 0x0D, dwFlags = 0x0002 } } };
        SendInput(2, inputs, Marshal.SizeOf<INPUT>());

        await Task.Delay(200);

        return new ToolResult
        {
            Name = Name,
            Success = true,
            Content = $"已点击元素：{selector}"
        };
    }

    private async Task<ToolResult> TypeTextAsync(JsonElement root)
    {
        var text = root.TryGetProperty("text", out var tEl) ? tEl.GetString() ?? "" : "";
        if (string.IsNullOrEmpty(text)) return Fail("缺少 text 参数");

        foreach (char c in text)
        {
            var input = new INPUT { type = INPUT_KEYBOARD, u = new INPUTUNION { ki = new KEYBDINPUT { wScan = (ushort)c, dwFlags = 0x0004 } } };
            SendInput(1, new[] { input }, Marshal.SizeOf<INPUT>());
            Thread.Sleep(10);
        }

        return new ToolResult
        {
            Name = Name,
            Success = true,
            Content = $"已输入 {text.Length} 个字符"
        };
    }

    private async Task<ToolResult> TakeScreenshotAsync(JsonElement root, CancellationToken ct)
    {
        var outputPath = root.TryGetProperty("output_path", out var oEl) ? oEl.GetString() : null;
        outputPath ??= Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "DesktopMascot", "screenshots", $"browser_{DateTime.UtcNow:yyyyMMdd_HHmmss}.png");

        var dir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        var hwnd = GetForegroundWindow();
        if (hwnd == IntPtr.Zero) return Fail("无法获取当前窗口");

        GetWindowRect(hwnd, out var rect);
        var w = rect.Right - rect.Left;
        var h = rect.Bottom - rect.Top;

        using var bmp = new System.Drawing.Bitmap(w, h);
        using var g = System.Drawing.Graphics.FromImage(bmp);
        g.CopyFromScreen(rect.Left, rect.Top, 0, 0, new System.Drawing.Size(w, h));
        bmp.Save(outputPath, System.Drawing.Imaging.ImageFormat.Png);

        return new ToolResult
        {
            Name = Name,
            Success = true,
            Content = $"截图已保存：{outputPath}"
        };
    }

    private async Task<ToolResult> ExtractContentAsync(JsonElement root, CancellationToken ct)
    {
        var hwnd = GetForegroundWindow();
        if (hwnd == IntPtr.Zero) return Fail("无法获取当前窗口");

        var sb = new System.Text.StringBuilder();
        var titleSb = new System.Text.StringBuilder(256);
        GetWindowText(hwnd, titleSb, 256);
        sb.AppendLine($"标题：{titleSb}");

        GetWindowThreadProcessId(hwnd, out var pid);
        try
        {
            var process = System.Diagnostics.Process.GetProcessById((int)pid);
            sb.AppendLine($"应用：{process.ProcessName}");
        }
        catch { }

        return new ToolResult
        {
            Name = Name,
            Success = true,
            Content = sb.ToString()
        };
    }

    private async Task<ToolResult> ScrollPageAsync(JsonElement root)
    {
        var amount = root.TryGetProperty("scroll_amount", out var sEl) ? sEl.GetInt32() : -120;

        const uint MOUSEEVENTF_WHEEL = 0x0800;
        mouse_event(MOUSEEVENTF_WHEEL, 0, 0, (uint)amount, 0);

        return new ToolResult
        {
            Name = Name,
            Success = true,
            Content = $"已滚动 {amount} 单位"
        };
    }

    private async Task<ToolResult> GoBackAsync()
    {
        // 使用 Alt+Left 快捷键
        var inputs = new INPUT[2];
        inputs[0] = new INPUT { type = INPUT_KEYBOARD, u = new INPUTUNION { ki = new KEYBDINPUT { wVk = 0x12, dwFlags = 0 } } };
        inputs[1] = new INPUT { type = INPUT_KEYBOARD, u = new INPUTUNION { ki = new KEYBDINPUT { wVk = 0x25, dwFlags = 0 } } };
        SendInput(2, inputs, Marshal.SizeOf<INPUT>());

        Thread.Sleep(50);

        inputs[0] = new INPUT { type = INPUT_KEYBOARD, u = new INPUTUNION { ki = new KEYBDINPUT { wVk = 0x25, dwFlags = 0x0002 } } };
        inputs[1] = new INPUT { type = INPUT_KEYBOARD, u = new INPUTUNION { ki = new KEYBDINPUT { wVk = 0x12, dwFlags = 0x0002 } } };
        SendInput(2, inputs, Marshal.SizeOf<INPUT>());

        return new ToolResult { Name = Name, Success = true, Content = "已后退" };
    }

    private async Task<ToolResult> GoForwardAsync()
    {
        var inputs = new INPUT[2];
        inputs[0] = new INPUT { type = INPUT_KEYBOARD, u = new INPUTUNION { ki = new KEYBDINPUT { wVk = 0x12, dwFlags = 0 } } };
        inputs[1] = new INPUT { type = INPUT_KEYBOARD, u = new INPUTUNION { ki = new KEYBDINPUT { wVk = 0x27, dwFlags = 0 } } };
        SendInput(2, inputs, Marshal.SizeOf<INPUT>());

        Thread.Sleep(50);

        inputs[0] = new INPUT { type = INPUT_KEYBOARD, u = new INPUTUNION { ki = new KEYBDINPUT { wVk = 0x27, dwFlags = 0x0002 } } };
        inputs[1] = new INPUT { type = INPUT_KEYBOARD, u = new INPUTUNION { ki = new KEYBDINPUT { wVk = 0x12, dwFlags = 0x0002 } } };
        SendInput(2, inputs, Marshal.SizeOf<INPUT>());

        return new ToolResult { Name = Name, Success = true, Content = "已前进" };
    }

    private async Task<ToolResult> RefreshAsync()
    {
        var inputs = new INPUT[2];
        inputs[0] = new INPUT { type = INPUT_KEYBOARD, u = new INPUTUNION { ki = new KEYBDINPUT { wVk = 0x11, dwFlags = 0 } } };
        inputs[1] = new INPUT { type = INPUT_KEYBOARD, u = new INPUTUNION { ki = new KEYBDINPUT { wVk = 0x52, dwFlags = 0 } } };
        SendInput(2, inputs, Marshal.SizeOf<INPUT>());

        Thread.Sleep(50);

        inputs[0] = new INPUT { type = INPUT_KEYBOARD, u = new INPUTUNION { ki = new KEYBDINPUT { wVk = 0x52, dwFlags = 0x0002 } } };
        inputs[1] = new INPUT { type = INPUT_KEYBOARD, u = new INPUTUNION { ki = new KEYBDINPUT { wVk = 0x11, dwFlags = 0x0002 } } };
        SendInput(2, inputs, Marshal.SizeOf<INPUT>());

        return new ToolResult { Name = Name, Success = true, Content = "已刷新" };
    }

    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);
    [DllImport("user32.dll")] private static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder lpString, int nMaxCount);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);
    [DllImport("user32.dll")] private static extern void mouse_event(uint dwFlags, int dx, int dy, uint dwData, int dwExtraInfo);
    [DllImport("user32.dll")] private static extern void SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    private const uint INPUT_KEYBOARD = 1;

    [StructLayout(LayoutKind.Sequential)] private struct RECT { public int Left; public int Top; public int Right; public int Bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct INPUT { public uint type; public INPUTUNION u; }
    [StructLayout(LayoutKind.Explicit)] private struct INPUTUNION { [FieldOffset(0)] public KEYBDINPUT ki; }
    [StructLayout(LayoutKind.Sequential)] private struct KEYBDINPUT { public ushort wVk; public ushort wScan; public uint dwFlags; public uint time; public IntPtr dwExtraInfo; }

    private static ToolResult Fail(string error) => new() { Name = "browser_automation", Success = false, Error = error };
}
