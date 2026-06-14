using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using DesktopMascot.Agent.Models;

namespace DesktopMascot.Agent.Tools;

/// <summary>
/// 屏幕动作执行器 - 执行基于屏幕理解的操作
/// </summary>
public class ScreenActionExecutor
{
    [DllImport("user32.dll")]
    private static extern bool SetCursorPos(int X, int Y);

    [DllImport("user32.dll")]
    private static extern void mouse_event(uint dwFlags, int dx, int dy, uint dwData, int dwExtraInfo);

    [DllImport("user32.dll")]
    private static extern void SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool OpenClipboard(IntPtr hWndNewOwner);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool EmptyClipboard();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetClipboardData(uint uFormat, IntPtr hMem);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool CloseClipboard();

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GlobalAlloc(uint uFlags, UIntPtr dwBytes);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GlobalLock(IntPtr hMem);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GlobalUnlock(IntPtr hMem);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GlobalFree(IntPtr hMem);

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT { public uint type; public INPUTUNION u; }
    [StructLayout(LayoutKind.Explicit)]
    private struct INPUTUNION { [FieldOffset(0)] public KEYBDINPUT ki; }
    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT { public ushort wVk; public ushort wScan; public uint dwFlags; public uint time; public IntPtr dwExtraInfo; }

    private const uint INPUT_KEYBOARD = 1;
    private const uint KEYEVENTF_KEYUP = 0x0002;
    private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
    private const uint MOUSEEVENTF_LEFTUP = 0x0004;
    private const uint CF_UNICODETEXT = 13;
    private const uint GMEM_MOVEABLE = 0x0002;
    private const uint GMEM_ZEROINIT = 0x0040;

    /// <summary>
    /// 执行屏幕动作
    /// </summary>
    public async Task<ScreenActionResult> ExecuteActionAsync(ScreenAction action, CancellationToken ct = default)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var result = action.ActionType.ToLower() switch
            {
                "click" => await ExecuteClickAsync(action),
                "type" => await ExecuteTypeAsync(action),
                "hotkey" => await ExecuteHotkeyAsync(action),
                "scroll" => await ExecuteScrollAsync(action),
                "open_url" => await OpenUrlAsync(action),
                "copy_text" => await CopyTextAsync(action),
                "run_command" => await RunCommandAsync(action),
                _ => new ScreenActionResult { Success = false, Error = $"不支持的操作类型: {action.ActionType}" }
            };

            stopwatch.Stop();
            result.Duration = stopwatch.Elapsed;
            result.ActionType = action.ActionType;
            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return new ScreenActionResult
            {
                Success = false,
                Error = $"执行失败: {ex.Message}",
                ActionType = action.ActionType,
                Duration = stopwatch.Elapsed
            };
        }
    }

    private async Task<ScreenActionResult> ExecuteClickAsync(ScreenAction action)
    {
        if (action.Parameters.TryGetValue("x", out var xStr) && int.TryParse(xStr, out var x) &&
            action.Parameters.TryGetValue("y", out var yStr) && int.TryParse(yStr, out var y))
        {
            SetCursorPos(x, y);
            await Task.Delay(50);
            mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, 0);
            mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, 0);

            return new ScreenActionResult
            {
                Success = true,
                Output = $"已点击坐标 ({x}, {y})"
            };
        }

        return new ScreenActionResult
        {
            Success = false,
            Error = "缺少 x 或 y 坐标"
        };
    }

    private async Task<ScreenActionResult> ExecuteTypeAsync(ScreenAction action)
    {
        if (action.Parameters.TryGetValue("text", out var text) && !string.IsNullOrEmpty(text))
        {
            foreach (char c in text)
            {
                var input = new INPUT { type = INPUT_KEYBOARD, u = new INPUTUNION { ki = new KEYBDINPUT { wScan = (ushort)c, dwFlags = 0x0004 } } };
                SendInput(1, new[] { input }, Marshal.SizeOf<INPUT>());
                await Task.Delay(10);
            }

            return new ScreenActionResult
            {
                Success = true,
                Output = $"已输入 {text.Length} 个字符"
            };
        }

        return new ScreenActionResult
        {
            Success = false,
            Error = "缺少 text 参数"
        };
    }

    private async Task<ScreenActionResult> ExecuteHotkeyAsync(ScreenAction action)
    {
        if (action.Parameters.TryGetValue("keys", out var keysStr) && !string.IsNullOrEmpty(keysStr))
        {
            var keys = keysStr.Split('+');
            var vkCodes = new List<ushort>();

            foreach (var key in keys)
            {
                var vk = ParseKey(key.Trim());
                if (vk > 0) vkCodes.Add(vk);
            }

            // 按下所有键
            foreach (var vk in vkCodes)
            {
                var input = new INPUT { type = INPUT_KEYBOARD, u = new INPUTUNION { ki = new KEYBDINPUT { wVk = vk, dwFlags = 0 } } };
                SendInput(1, new[] { input }, Marshal.SizeOf<INPUT>());
                await Task.Delay(10);
            }

            // 释放所有键（反序）
            foreach (var vk in vkCodes.AsEnumerable().Reverse())
            {
                var input = new INPUT { type = INPUT_KEYBOARD, u = new INPUTUNION { ki = new KEYBDINPUT { wVk = vk, dwFlags = 0x0002 } } };
                SendInput(1, new[] { input }, Marshal.SizeOf<INPUT>());
                await Task.Delay(10);
            }

            return new ScreenActionResult
            {
                Success = true,
                Output = $"已执行快捷键: {keysStr}"
            };
        }

        return new ScreenActionResult
        {
            Success = false,
            Error = "缺少 keys 参数"
        };
    }

    private Task<ScreenActionResult> ExecuteScrollAsync(ScreenAction action)
    {
        if (action.Parameters.TryGetValue("delta", out var deltaStr) && int.TryParse(deltaStr, out var delta))
        {
            const uint MOUSEEVENTF_WHEEL = 0x0800;
            mouse_event(MOUSEEVENTF_WHEEL, 0, 0, (uint)delta, 0);

            return Task.FromResult(new ScreenActionResult
            {
                Success = true,
                Output = $"已滚动 {delta} 单位"
            });
        }

        return Task.FromResult(new ScreenActionResult
        {
            Success = false,
            Error = "缺少 delta 参数"
        });
    }

    private Task<ScreenActionResult> OpenUrlAsync(ScreenAction action)
    {
        if (action.Parameters.TryGetValue("url", out var url) && !string.IsNullOrEmpty(url))
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });

            return Task.FromResult(new ScreenActionResult
            {
                Success = true,
                Output = $"已打开: {url}"
            });
        }

        return Task.FromResult(new ScreenActionResult
        {
            Success = false,
            Error = "缺少 url 参数"
        });
    }

    private Task<ScreenActionResult> CopyTextAsync(ScreenAction action)
    {
        if (action.Parameters.TryGetValue("text", out var text) && !string.IsNullOrEmpty(text))
        {
            if (!TrySetClipboardText(text))
            {
                return Task.FromResult(new ScreenActionResult
                {
                    Success = false,
                    Error = "写入剪贴板失败"
                });
            }

            return Task.FromResult(new ScreenActionResult
            {
                Success = true,
                Output = $"已复制 {text.Length} 个字符到剪贴板"
            });
        }

        return Task.FromResult(new ScreenActionResult
        {
            Success = false,
            Error = "缺少 text 参数"
        });
    }

    private static bool TrySetClipboardText(string text)
    {
        if (!OperatingSystem.IsWindows())
            return false;

        if (!OpenClipboard(IntPtr.Zero))
            return false;

        var handle = IntPtr.Zero;
        try
        {
            if (!EmptyClipboard())
                return false;

            var bytes = Encoding.Unicode.GetBytes(text + '\0');
            handle = GlobalAlloc(GMEM_MOVEABLE | GMEM_ZEROINIT, (UIntPtr)bytes.Length);
            if (handle == IntPtr.Zero)
                return false;

            var target = GlobalLock(handle);
            if (target == IntPtr.Zero)
                return false;

            try
            {
                Marshal.Copy(bytes, 0, target, bytes.Length);
            }
            finally
            {
                GlobalUnlock(handle);
            }

            if (SetClipboardData(CF_UNICODETEXT, handle) == IntPtr.Zero)
                return false;

            handle = IntPtr.Zero;
            return true;
        }
        finally
        {
            if (handle != IntPtr.Zero)
            {
                GlobalFree(handle);
            }

            CloseClipboard();
        }
    }

    private async Task<ScreenActionResult> RunCommandAsync(ScreenAction action)
    {
        if (!action.Parameters.TryGetValue("command", out var command) || string.IsNullOrEmpty(command))
        {
            return new ScreenActionResult
            {
                Success = false,
                Error = "缺少 command 参数"
            };
        }

        // 默认超时 30 秒，可通过 action.Parameters["timeout_seconds"] 覆盖
        var timeoutSeconds = action.Parameters.TryGetValue("timeout_seconds", out var tsStr)
            && int.TryParse(tsStr, out var tsParsed) ? tsParsed : 30;

        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
        // 合并外部 CancellationToken 和超时
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
            CancellationToken.None, timeoutCts.Token);

        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c {command}",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                },
                EnableRaisingEvents = true
            };

            process.Start();

            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();
            var exitTask = process.WaitForExitAsync(linkedCts.Token);

            await exitTask;
            var output = await outputTask;
            var error = await errorTask;

            return new ScreenActionResult
            {
                Success = process.ExitCode == 0,
                Output = output.Length > 0 ? output : null,
                Error = string.IsNullOrEmpty(error) ? null : error
            };
        }
        catch (OperationCanceledException)
        {
            return new ScreenActionResult
            {
                Success = false,
                Error = $"命令执行超时（{timeoutSeconds}秒）"
            };
        }
    }

    private static ushort ParseKey(string key)
    {
        return key.ToLower() switch
        {
            "ctrl" or "control" => 0x11,
            "shift" => 0x10,
            "alt" => 0x12,
            "enter" or "return" => 0x0D,
            "tab" => 0x09,
            "escape" or "esc" => 0x1B,
            "space" => 0x20,
            "backspace" => 0x08,
            "delete" or "del" => 0x2E,
            "home" => 0x24,
            "end" => 0x23,
            "pageup" => 0x21,
            "pagedown" => 0x22,
            "left" => 0x25,
            "up" => 0x26,
            "right" => 0x27,
            "down" => 0x28,
            "f1" => 0x70, "f2" => 0x71, "f3" => 0x72, "f4" => 0x73,
            "f5" => 0x74, "f6" => 0x75, "f7" => 0x76, "f8" => 0x77,
            "f9" => 0x78, "f10" => 0x79, "f11" => 0x7A, "f12" => 0x7B,
            _ when key.Length == 1 => (ushort)char.ToUpper(key[0]),
            _ => 0
        };
    }
}
