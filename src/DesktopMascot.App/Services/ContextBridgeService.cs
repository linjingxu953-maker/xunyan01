using DesktopMascot.Agent.Context;
using DesktopMascot.Core.Interfaces;
using DesktopMascot.Core.Models;

namespace DesktopMascot.App.Services;

/// <summary>
/// 上下文桥接服务 - App 层实现，供 UI 层调用
/// 隔离 UI 与 Agent 层的直接依赖
/// </summary>
public interface IContextBridgeService
{
    /// <summary>获取当前窗口信息</summary>
    Task<WindowInfo> GetActiveWindowAsync(CancellationToken ct = default);

    /// <summary>获取选中文本</summary>
    Task<string> GetSelectedTextAsync(CancellationToken ct = default);

    /// <summary>获取屏幕截图路径</summary>
    Task<string?> CaptureScreenshotAsync(CancellationToken ct = default);

    /// <summary>读取文件内容</summary>
    Task<string?> ReadFileAsync(string filePath, CancellationToken ct = default);
}

/// <summary>
/// 窗口信息（UI 层可用的简化模型）
/// </summary>
public class WindowInfo
{
    public string Title { get; set; } = string.Empty;
    public string Application { get; set; } = string.Empty;
    public bool IsBrowser { get; set; }
    public DateTime CapturedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// 上下文桥接服务实现
/// </summary>
public class ContextBridgeService : IContextBridgeService
{
    private readonly IContextProvider _contextProvider;

    public ContextBridgeService(IContextProvider contextProvider)
    {
        _contextProvider = contextProvider;
    }

    public async Task<WindowInfo> GetActiveWindowAsync(CancellationToken ct = default)
    {
        var snapshot = await _contextProvider.GetActiveWindowContextAsync(ct);

        return new WindowInfo
        {
            Title = snapshot.ActiveWindowTitle,
            Application = snapshot.ActiveApplication,
            IsBrowser = IsBrowserApp(snapshot.ActiveApplication),
            CapturedAt = snapshot.CapturedAt
        };
    }

    public async Task<string> GetSelectedTextAsync(CancellationToken ct = default)
    {
        return await _contextProvider.GetSelectedTextAsync(ct);
    }

    public async Task<string?> CaptureScreenshotAsync(CancellationToken ct = default)
    {
        return await _contextProvider.CaptureScreenshotAsync(ct: ct);
    }

    public async Task<string?> ReadFileAsync(string filePath, CancellationToken ct = default)
    {
        return await _contextProvider.ReadFileAsync(filePath, ct);
    }

    private static bool IsBrowserApp(string appName)
    {
        var browsers = new[] { "chrome", "firefox", "msedge", "opera", "brave", "vivaldi" };
        return browsers.Any(b => appName.Contains(b, StringComparison.OrdinalIgnoreCase));
    }
}
