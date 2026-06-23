using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

namespace DesktopMascot.Agent.Context;

/// <summary>
/// Windows 上下文提供者
/// </summary>
public class WindowsContextProvider : IContextProvider
{
    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    private readonly string _screenshotDirectory;

    public WindowsContextProvider()
    {
        _screenshotDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "DesktopMascot",
            "screenshots");
        Directory.CreateDirectory(_screenshotDirectory);
    }

    public async Task<ContextSnapshot> GetActiveWindowContextAsync(CancellationToken ct = default)
    {
        var snapshot = new ContextSnapshot();

        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var hwnd = GetForegroundWindow();
                if (hwnd != IntPtr.Zero)
                {
                    // 获取窗口标题
                    var sb = new StringBuilder(256);
                    GetWindowText(hwnd, sb, 256);
                    snapshot.ActiveWindowTitle = sb.ToString();

                    // 获取进程名
                    GetWindowThreadProcessId(hwnd, out var processId);
                    try
                    {
                        var process = Process.GetProcessById((int)processId);
                        snapshot.ActiveApplication = process.ProcessName;

                        // 检查是否是浏览器
                        if (IsBrowserProcess(process.ProcessName))
                        {
                            snapshot.BrowserTitle = snapshot.ActiveWindowTitle;
                            // 浏览器 URL 需要通过其他方式获取（如浏览器扩展）
                        }
                    }
                    catch
                    {
                        snapshot.ActiveApplication = "unknown";
                    }
                }
            }
            else
            {
                snapshot.ActiveWindowTitle = "Non-Windows Platform";
                snapshot.ActiveApplication = "unknown";
            }
        }
        catch (Exception ex)
        {
            snapshot.ActiveWindowTitle = $"Error: {ex.Message}";
        }

        if (string.IsNullOrWhiteSpace(snapshot.ActiveWindowTitle))
        {
            snapshot.ActiveWindowTitle = "Untitled Window";
        }

        if (string.IsNullOrWhiteSpace(snapshot.ActiveApplication))
        {
            snapshot.ActiveApplication = "unknown";
        }

        return await Task.FromResult(snapshot);
    }

    public Task<string> GetSelectedTextAsync(CancellationToken ct = default)
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // 使用剪贴板读取选中文本
                var text = GetClipboardText();
                return Task.FromResult(text ?? string.Empty);
            }
        }
        catch
        {
            // 忽略异常
        }

        return Task.FromResult(string.Empty);
    }

    public async Task<string?> ReadFileAsync(string filePath, CancellationToken ct = default)
    {
        try
        {
            if (!File.Exists(filePath))
                return null;

            // 安全检查：限制文件大小
            var fileInfo = new FileInfo(filePath);
            if (fileInfo.Length > 1024 * 1024) // 1MB 限制
                return $"[文件过大: {fileInfo.Length / 1024}KB]";

            // 检查文件类型
            var extension = Path.GetExtension(filePath).ToLower();
            if (!IsTextFile(extension))
                return $"[不支持的文件类型: {extension}]";

            return await File.ReadAllTextAsync(filePath, ct);
        }
        catch (Exception ex)
        {
            return $"[读取失败: {ex.Message}]";
        }
    }

    public Task<string?> CaptureScreenshotAsync(string? outputPath = null, CancellationToken ct = default)
    {
        try
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return Task.FromResult<string?>(null);

            var hwnd = GetForegroundWindow();
            if (hwnd == IntPtr.Zero)
                return Task.FromResult<string?>(null);

            // 获取窗口尺寸
            GetWindowRect(hwnd, out var rect);
            var width = rect.Right - rect.Left;
            var height = rect.Bottom - rect.Top;

            if (width <= 0 || height <= 0)
                return Task.FromResult<string?>(null);

            // 截取屏幕
            using var bitmap = new Bitmap(width, height);
            using var graphics = Graphics.FromImage(bitmap);
            graphics.CopyFromScreen(rect.Left, rect.Top, 0, 0, new Size(width, height));

            // 保存截图
            var fileName = outputPath ?? Path.Combine(
                _screenshotDirectory,
                $"screenshot_{DateTime.UtcNow:yyyyMMdd_HHmmss}.png");

            bitmap.Save(fileName, ImageFormat.Png);

            return Task.FromResult<string?>(fileName);
        }
        catch (Exception ex)
        {
            return Task.FromResult<string?>($"[截图失败: {ex.Message}]");
        }
    }

    public Task<string?> CaptureScreenshotRegionAsync(
        int x,
        int y,
        int width,
        int height,
        string? outputPath = null,
        CancellationToken ct = default)
    {
        try
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return Task.FromResult<string?>(null);

            if (width <= 0 || height <= 0)
                return Task.FromResult<string?>("[截图失败: 区域宽高必须大于 0]");

            using var bitmap = new Bitmap(width, height);
            using var graphics = Graphics.FromImage(bitmap);
            graphics.CopyFromScreen(x, y, 0, 0, new Size(width, height));

            var fileName = outputPath ?? Path.Combine(
                _screenshotDirectory,
                $"region_{DateTime.UtcNow:yyyyMMdd_HHmmss_fff}.png");

            var directory = Path.GetDirectoryName(fileName);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);

            bitmap.Save(fileName, ImageFormat.Png);
            return Task.FromResult<string?>(fileName);
        }
        catch (Exception ex)
        {
            return Task.FromResult<string?>($"[区域截图失败: {ex.Message}]");
        }
    }

    public Task<string?> GetBrowserContentAsync(CancellationToken ct = default)
    {
        // 浏览器内容读取需要浏览器扩展或自动化工具支持
        // 初版返回占位实现
        return Task.FromResult<string?>("[浏览器内容读取需要浏览器扩展支持]");
    }

    public async Task<ContextSnapshot> GetFullContextAsync(CancellationToken ct = default)
    {
        var snapshot = await GetActiveWindowContextAsync(ct);
        snapshot.SelectedText = await GetSelectedTextAsync(ct);
        return snapshot;
    }

    /// <summary>
    /// 检查是否是浏览器进程
    /// </summary>
    private static bool IsBrowserProcess(string processName)
    {
        var browsers = new[] { "chrome", "firefox", "msedge", "opera", "brave", "vivaldi" };
        return browsers.Any(b => processName.Contains(b, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// 检查是否是文本文件
    /// </summary>
    private static bool IsTextFile(string extension)
    {
        var textExtensions = new[]
        {
            ".txt", ".md", ".json", ".xml", ".csv", ".log", ".tmp",
            ".cs", ".js", ".ts", ".py", ".java", ".cpp", ".h",
            ".html", ".css", ".yaml", ".yml", ".toml", ".ini",
            ".sql", ".sh", ".bat", ".ps1", ".cmd", ".config"
        };
        return textExtensions.Contains(extension);
    }

    /// <summary>
    /// 获取剪贴板文本
    /// </summary>
    [DllImport("user32.dll")]
    private static extern bool OpenClipboard(IntPtr hWndNewOwner);

    [DllImport("user32.dll")]
    private static extern bool CloseClipboard();

    [DllImport("user32.dll")]
    private static extern IntPtr GetClipboardData(uint uFormat);

    [DllImport("user32.dll")]
    private static extern bool IsClipboardFormatAvailable(uint uFormat);

    private const uint CF_UNICODETEXT = 13;

    private static string? GetClipboardText()
    {
        try
        {
            if (!IsClipboardFormatAvailable(CF_UNICODETEXT))
                return null;

            if (!OpenClipboard(IntPtr.Zero))
                return null;

            try
            {
                var handle = GetClipboardData(CF_UNICODETEXT);
                if (handle == IntPtr.Zero)
                    return null;

                var ptr = GlobalLock(handle);
                if (ptr == IntPtr.Zero)
                    return null;

                try
                {
                    return Marshal.PtrToStringUni(ptr);
                }
                finally
                {
                    GlobalUnlock(handle);
                }
            }
            finally
            {
                CloseClipboard();
            }
        }
        catch
        {
            return null;
        }
    }

    [DllImport("kernel32.dll")]
    private static extern IntPtr GlobalLock(IntPtr hMem);

    [DllImport("kernel32.dll")]
    private static extern bool GlobalUnlock(IntPtr hMem);
}

/// <summary>
/// 模拟上下文提供者（用于测试）
/// </summary>
public class MockContextProvider : IContextProvider
{
    public string MockWindowTitle { get; set; } = "Test Window";
    public string MockAppName { get; set; } = "TestApp";
    public string MockSelectedText { get; set; } = string.Empty;
    public string? MockFileContent { get; set; }
    public string? MockScreenshotPath { get; set; }
    public string? MockRegionScreenshotPath { get; set; }
    public string? MockBrowserContent { get; set; }
    public int FullScreenshotCaptureCount { get; private set; }
    public int RegionScreenshotCaptureCount { get; private set; }
    public (int X, int Y, int Width, int Height)? LastCapturedRegion { get; private set; }

    public Task<ContextSnapshot> GetActiveWindowContextAsync(CancellationToken ct = default)
    {
        return Task.FromResult(new ContextSnapshot
        {
            ActiveWindowTitle = MockWindowTitle,
            ActiveApplication = MockAppName
        });
    }

    public Task<string> GetSelectedTextAsync(CancellationToken ct = default)
    {
        return Task.FromResult(MockSelectedText);
    }

    public Task<string?> ReadFileAsync(string filePath, CancellationToken ct = default)
    {
        return Task.FromResult(MockFileContent);
    }

    public Task<string?> CaptureScreenshotAsync(string? outputPath = null, CancellationToken ct = default)
    {
        FullScreenshotCaptureCount++;
        return Task.FromResult(MockScreenshotPath);
    }

    public Task<string?> CaptureScreenshotRegionAsync(
        int x,
        int y,
        int width,
        int height,
        string? outputPath = null,
        CancellationToken ct = default)
    {
        RegionScreenshotCaptureCount++;
        LastCapturedRegion = (x, y, width, height);
        return Task.FromResult(MockRegionScreenshotPath ?? MockScreenshotPath);
    }

    public Task<string?> GetBrowserContentAsync(CancellationToken ct = default)
    {
        return Task.FromResult(MockBrowserContent);
    }

    public Task<ContextSnapshot> GetFullContextAsync(CancellationToken ct = default)
    {
        return Task.FromResult(new ContextSnapshot
        {
            ActiveWindowTitle = MockWindowTitle,
            ActiveApplication = MockAppName,
            SelectedText = MockSelectedText,
            ScreenshotPath = MockScreenshotPath,
            BrowserContent = MockBrowserContent
        });
    }
}
