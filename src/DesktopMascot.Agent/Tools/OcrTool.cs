using DesktopMascot.Core.Tools;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using DesktopMascot.Agent.Models;

namespace DesktopMascot.Agent.Tools;

/// <summary>
/// OCR 工具 — 图片文字提取、截图识别、批量识别
/// 基于 Windows 内置 OCR API（Windows.Media.Ocr）
/// </summary>
public class OcrTool : ITool
{
    private readonly ITool _screenUnderstand;

    public string Name => "ocr";
    public string Description => "OCR 文字提取：图片/截图/屏幕区域的文字识别、批量识别、语言检测。";
    public string ParametersSchema => """
    {
        "type": "object",
        "properties": {
            "action": { "type": "string", "enum": ["extract", "screen", "batch", "region"], "description": "操作类型" },
            "image_path": { "type": "string", "description": "图片路径" },
            "language": { "type": "string", "enum": ["zh", "en", "auto"], "description": "识别语言" },
            "output_path": { "type": "string", "description": "输出文件路径" },
            "x": { "type": "integer", "description": "区域左上角 X" },
            "y": { "type": "integer", "description": "区域左上角 Y" },
            "width": { "type": "integer", "description": "区域宽度" },
            "height": { "type": "integer", "description": "区域高度" }
        },
        "required": ["action"]
    }
    """;

    public OcrTool(ITool screenUnderstand)
    {
        _screenUnderstand = screenUnderstand;
    }

    public async Task<ToolResult> ExecuteAsync(string arguments, CancellationToken ct = default)
    {
        try
        {
            var doc = JsonDocument.Parse(arguments);
            var root = doc.RootElement;
            var action = root.TryGetProperty("action", out var aEl) ? aEl.GetString() ?? "" : "";

            return action switch
            {
                "extract" => await ExtractFromImageAsync(root, ct),
                "screen" => await ExtractFromScreenAsync(root, ct),
                "batch" => await BatchExtractAsync(root, ct),
                "region" => await ExtractFromRegionAsync(root, ct),
                _ => Fail($"不支持的操作：{action}")
            };
        }
        catch (Exception ex)
        {
            return Fail($"OCR 识别失败：{ex.Message}");
        }
    }

    private async Task<ToolResult> ExtractFromImageAsync(JsonElement root, CancellationToken ct)
    {
        var imagePath = GetRequiredString(root, "image_path");
        if (string.IsNullOrEmpty(imagePath)) return Fail("缺少 image_path 参数");
        if (!File.Exists(imagePath)) return Fail($"文件不存在：{imagePath}");

        var text = await ExtractTextFromImageAsync(imagePath, ct);

        var sb = new StringBuilder();
        sb.AppendLine("OCR 识别结果");
        sb.AppendLine($"图片：{Path.GetFileName(imagePath)}");
        sb.AppendLine($"大小：{new FileInfo(imagePath).Length / 1024.0:F1} KB");
        sb.AppendLine();
        sb.AppendLine("提取文字：");
        sb.AppendLine(text);

        // 保存结果
        var outputPath = root.TryGetProperty("output_path", out var oEl) ? oEl.GetString() : null;
        if (!string.IsNullOrEmpty(outputPath))
        {
            var dir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            await File.WriteAllTextAsync(outputPath, text, ct);
            sb.AppendLine();
            sb.AppendLine($"已保存到：{outputPath}");
        }

        return new ToolResult { Name = Name, Success = true, Content = sb.ToString() };
    }

    private async Task<ToolResult> ExtractFromScreenAsync(JsonElement root, CancellationToken ct)
    {
        // 使用 ScreenUnderstandTool 截屏 + 视觉识别
        var result = await _screenUnderstand.ExecuteAsync(
            """{"action":"understand","hint":"请识别并提取屏幕上的所有文字内容，保持原始格式"}""", ct);

        if (!result.Success)
            return Fail($"屏幕识别失败：{result.Error}");

        var sb = new StringBuilder();
        sb.AppendLine("屏幕 OCR 识别结果");
        sb.AppendLine();
        sb.AppendLine(result.Content);

        return new ToolResult { Name = Name, Success = true, Content = sb.ToString() };
    }

    private async Task<ToolResult> BatchExtractAsync(JsonElement root, CancellationToken ct)
    {
        var directory = GetRequiredString(root, "image_path");
        if (string.IsNullOrEmpty(directory)) return Fail("缺少 image_path 参数（目录路径）");
        if (!Directory.Exists(directory)) return Fail($"目录不存在：{directory}");

        var extensions = new[] { ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".webp", ".tiff" };
        var files = Directory.GetFiles(directory, "*.*", SearchOption.AllDirectories)
            .Where(f => extensions.Contains(Path.GetExtension(f).ToLower()))
            .ToList();

        var sb = new StringBuilder();
        sb.AppendLine($"批量 OCR 识别");
        sb.AppendLine($"目录：{directory}");
        sb.AppendLine($"图片数量：{files.Count}");
        sb.AppendLine();

        var allText = new StringBuilder();

        foreach (var file in files)
        {
            ct.ThrowIfCancellationRequested();
            var text = await ExtractTextFromImageAsync(file, ct);
            if (!string.IsNullOrWhiteSpace(text))
            {
                sb.AppendLine($"── {Path.GetFileName(file)} ──");
                sb.AppendLine(text);
                sb.AppendLine();
                allText.AppendLine($"## {Path.GetFileName(file)}\n{text}\n");
            }
        }

        var outputPath = root.TryGetProperty("output_path", out var oEl) ? oEl.GetString() : null;
        if (!string.IsNullOrEmpty(outputPath))
        {
            var dir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            await File.WriteAllTextAsync(outputPath, allText.ToString(), ct);
            sb.AppendLine($"已保存到：{outputPath}");
        }

        return new ToolResult { Name = Name, Success = true, Content = sb.ToString() };
    }

    private async Task<ToolResult> ExtractFromRegionAsync(JsonElement root, CancellationToken ct)
    {
        var x = root.TryGetProperty("x", out var xEl) ? xEl.GetInt32() : 0;
        var y = root.TryGetProperty("y", out var yEl) ? yEl.GetInt32() : 0;
        var width = root.TryGetProperty("width", out var wEl) ? wEl.GetInt32() : 800;
        var height = root.TryGetProperty("height", out var hEl) ? hEl.GetInt32() : 600;

        // 截取指定区域
        var tempPath = Path.Combine(Path.GetTempPath(), $"ocr_region_{Guid.NewGuid():N}.png");

        try
        {
            using var bmp = new Bitmap(width, height);
            using var g = Graphics.FromImage(bmp);
            g.CopyFromScreen(x, y, 0, 0, new Size(width, height));
            bmp.Save(tempPath, ImageFormat.Png);

            var text = await ExtractTextFromImageAsync(tempPath, ct);

            var sb = new StringBuilder();
            sb.AppendLine("区域 OCR 识别结果");
            sb.AppendLine($"区域：({x}, {y}) {width}x{height}");
            sb.AppendLine();
            sb.AppendLine("提取文字：");
            sb.AppendLine(text);

            return new ToolResult { Name = Name, Success = true, Content = sb.ToString() };
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }

    /// <summary>
    /// 从图片提取文字（使用 .NET 内置 OCR）
    /// </summary>
    private static async Task<string> ExtractTextFromImageAsync(string imagePath, CancellationToken ct)
    {
        try
        {
            // Windows.Media.Ocr API（Windows 10 17763+）
            // 通过 PowerShell 调用避免复杂的 WinRT 互操作
            var script = $@"
                Add-Type -AssemblyName System.Runtime.WindowsRuntime
                [Windows.Media.Ocr.OcrEngine, Windows.Media.Ocr, ContentType = WindowsRuntime] | Out-Null
                [Windows.Graphics.Imaging.SoftwareBitmap, Windows.Graphics.Imaging, ContentType = WindowsRuntime] | Out-Null

                $file = Get-Item '{imagePath.Replace("\\", "/")}'
                $stream = $file.OpenRead()
                $decoder = [Windows.Graphics.Imaging.BitmapDecoder]::CreateAsync($stream).AsTask().Result
                $bitmap = $decoder.GetSoftwareBitmapAsync().AsTask().Result
                $engine = [Windows.Media.Ocr.OcrEngine]::TryCreateFromLanguage([Windows.Globalization.Language]::new('zh-Hans-CN'))
                if (-not $engine) {{ $engine = [Windows.Media.Ocr.OcrEngine]::TryCreateFromLanguage([Windows.Globalization.Language]::new('en-US')) }}
                if (-not $engine) {{ $engine = [Windows.Media.Ocr.OcrEngine]::TryCreateFromUserProfileLanguages() }}
                $result = $engine.RecognizeAsync($bitmap).AsTask().Result
                $result.Text
            ";

            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -Command \"{script.Replace("\"", "\\\"")}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = System.Diagnostics.Process.Start(psi) ?? throw new InvalidOperationException("无法启动 PowerShell");
            var output = await process.StandardOutput.ReadToEndAsync(ct);
            var error = await process.StandardError.ReadToEndAsync(ct);
            await process.WaitForExitAsync(ct);

            if (process.ExitCode == 0 && !string.IsNullOrWhiteSpace(output))
                return output.Trim();

            // 降级：使用 ScreenUnderstand 的视觉识别
            return $"[OCR API 不可用，错误：{error.Trim()}]\n提示：请使用 screen_understand 工具的视觉识别功能替代";
        }
        catch
        {
            return "[OCR 识别失败，建议使用 screen_understand 工具替代]";
        }
    }

    private static string? GetRequiredString(JsonElement root, string name)
    {
        return root.TryGetProperty(name, out var el) ? el.GetString() : null;
    }

    private static ToolResult Fail(string error) => new() { Name = "ocr", Success = false, Error = error };
}
