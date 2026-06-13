using System.Text.Json;
using DesktopMascot.Agent.Context;
using DesktopMascot.Agent.Models;

namespace DesktopMascot.Agent.Tools;

/// <summary>
/// 屏幕截图工具
/// </summary>
public class ScreenCaptureTool : ITool
{
    private readonly IContextProvider _contextProvider;

    public ScreenCaptureTool(IContextProvider contextProvider)
    {
        _contextProvider = contextProvider;
    }

    public string Name => "screen_capture";
    public string Description => "截取当前屏幕截图并保存到文件";
    public string ParametersSchema => """
    {
        "type": "object",
        "properties": {
            "output_path": {
                "type": "string",
                "description": "截图保存路径（可选，默认自动生成）"
            }
        }
    }
    """;

    public async Task<ToolResult> ExecuteAsync(string arguments, CancellationToken ct = default)
    {
        try
        {
            string? outputPath = null;

            if (!string.IsNullOrEmpty(arguments) && arguments != "{}")
            {
                var doc = JsonDocument.Parse(arguments);
                if (doc.RootElement.TryGetProperty("output_path", out var pathElement))
                {
                    outputPath = pathElement.GetString();
                }
            }

            var result = await _contextProvider.CaptureScreenshotAsync(outputPath, ct);

            if (result == null)
            {
                return new ToolResult
                {
                    Name = Name,
                    Success = false,
                    Error = "截图失败"
                };
            }

            if (result.StartsWith("["))
            {
                return new ToolResult
                {
                    Name = Name,
                    Success = false,
                    Error = result
                };
            }

            return new ToolResult
            {
                Name = Name,
                Success = true,
                Content = $"截图已保存到: {result}"
            };
        }
        catch (Exception ex)
        {
            return new ToolResult
            {
                Name = Name,
                Success = false,
                Error = $"截图失败: {ex.Message}"
            };
        }
    }
}

/// <summary>
/// 浏览器上下文工具
/// </summary>
public class BrowserContextTool : ITool
{
    private readonly IContextProvider _contextProvider;

    public BrowserContextTool(IContextProvider contextProvider)
    {
        _contextProvider = contextProvider;
    }

    public string Name => "browser_context";
    public string Description => "获取当前浏览器页面内容，包括窗口标题、URL 和页面文本";
    public string ParametersSchema => "{}";

    public async Task<ToolResult> ExecuteAsync(string arguments, CancellationToken ct = default)
    {
        try
        {
            var snapshot = await _contextProvider.GetActiveWindowContextAsync(ct);
            var screenshotPath = await _contextProvider.CaptureScreenshotAsync(ct: ct);

            var result = new System.Text.StringBuilder();
            result.AppendLine($"窗口标题: {snapshot.ActiveWindowTitle}");
            result.AppendLine($"应用程序: {snapshot.ActiveApplication}");

            if (!string.IsNullOrEmpty(snapshot.BrowserTitle))
                result.AppendLine($"浏览器标签: {snapshot.BrowserTitle}");

            if (!string.IsNullOrEmpty(snapshot.BrowserUrl))
                result.AppendLine($"URL: {snapshot.BrowserUrl}");

            if (!string.IsNullOrEmpty(screenshotPath) && !screenshotPath.StartsWith("["))
                result.AppendLine($"截图已保存: {screenshotPath}");

            if (!string.IsNullOrEmpty(snapshot.BrowserContent) && !snapshot.BrowserContent.StartsWith("["))
            {
                var content = snapshot.BrowserContent.Length > 8000
                    ? snapshot.BrowserContent[..8000] + "\n...(内容已截断)"
                    : snapshot.BrowserContent;
                result.AppendLine($"页面内容: {content}");
            }

            return new ToolResult
            {
                Name = Name,
                Success = true,
                Content = result.ToString()
            };
        }
        catch (Exception ex)
        {
            return new ToolResult
            {
                Name = Name,
                Success = false,
                Error = $"获取浏览器内容失败: {ex.Message}"
            };
        }
    }
}

/// <summary>
/// 剪贴板工具
/// </summary>
public class ClipboardTool : ITool
{
    private readonly IContextProvider _contextProvider;

    public ClipboardTool(IContextProvider contextProvider)
    {
        _contextProvider = contextProvider;
    }

    public string Name => "clipboard";
    public string Description => "获取当前剪贴板内容（选中文本）";
    public string ParametersSchema => "{}";

    public async Task<ToolResult> ExecuteAsync(string arguments, CancellationToken ct = default)
    {
        try
        {
            var text = await _contextProvider.GetSelectedTextAsync(ct);

            if (string.IsNullOrEmpty(text))
            {
                return new ToolResult
                {
                    Name = Name,
                    Success = true,
                    Content = "剪贴板为空或没有选中文本"
                };
            }

            return new ToolResult
            {
                Name = Name,
                Success = true,
                Content = text
            };
        }
        catch (Exception ex)
        {
            return new ToolResult
            {
                Name = Name,
                Success = false,
                Error = $"获取剪贴板内容失败: {ex.Message}"
            };
        }
    }
}
