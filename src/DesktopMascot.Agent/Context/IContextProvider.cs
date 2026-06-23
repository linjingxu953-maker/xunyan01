namespace DesktopMascot.Agent.Context;

/// <summary>
/// 上下文快照
/// </summary>
public class ContextSnapshot
{
    /// <summary>当前窗口标题</summary>
    public string ActiveWindowTitle { get; set; } = string.Empty;

    /// <summary>当前应用名</summary>
    public string ActiveApplication { get; set; } = string.Empty;

    /// <summary>选中文本</summary>
    public string SelectedText { get; set; } = string.Empty;

    /// <summary>浏览器 URL</summary>
    public string? BrowserUrl { get; set; }

    /// <summary>浏览器标题</summary>
    public string? BrowserTitle { get; set; }

    /// <summary>浏览器页面内容</summary>
    public string? BrowserContent { get; set; }

    /// <summary>文件内容</summary>
    public string? FileContent { get; set; }

    /// <summary>文件路径</summary>
    public string? FilePath { get; set; }

    /// <summary>屏幕截图路径</summary>
    public string? ScreenshotPath { get; set; }

    /// <summary>采集时间</summary>
    public DateTime CapturedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// 上下文提供者接口
/// </summary>
public interface IContextProvider
{
    /// <summary>获取当前窗口信息</summary>
    Task<ContextSnapshot> GetActiveWindowContextAsync(CancellationToken ct = default);

    /// <summary>获取选中文本</summary>
    Task<string> GetSelectedTextAsync(CancellationToken ct = default);

    /// <summary>读取文件内容</summary>
    Task<string?> ReadFileAsync(string filePath, CancellationToken ct = default);

    /// <summary>获取屏幕截图</summary>
    Task<string?> CaptureScreenshotAsync(string? outputPath = null, CancellationToken ct = default);

    /// <summary>截取指定屏幕区域，坐标为物理屏幕坐标</summary>
    Task<string?> CaptureScreenshotRegionAsync(
        int x,
        int y,
        int width,
        int height,
        string? outputPath = null,
        CancellationToken ct = default);

    /// <summary>获取浏览器页面内容</summary>
    Task<string?> GetBrowserContentAsync(CancellationToken ct = default);

    /// <summary>获取完整上下文</summary>
    Task<ContextSnapshot> GetFullContextAsync(CancellationToken ct = default);
}
