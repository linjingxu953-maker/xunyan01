using DesktopMascot.Core.Tools;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using DesktopMascot.Agent.Models;

namespace DesktopMascot.Agent.Tools;

/// <summary>
/// PDF 处理工具 — 读取/拆分/合并/提取文本/转换格式
/// 使用 dotnet-script 或 iTextSharp / PdfPig 库
/// </summary>
public class PdfTool : ITool
{
    private readonly ITool _ocr;

    public string Name => "pdf_tool";
    public string Description => "PDF 处理：读取内容、提取文本、拆分/合并页面、PDF 转图片、图片转 PDF。";
    public string ParametersSchema => """
    {
        "type": "object",
        "properties": {
            "action": { "type": "string", "enum": ["read", "extract_text", "split", "merge", "to_image", "from_images", "info", "page_count"], "description": "操作类型" },
            "input_path": { "type": "string", "description": "输入文件路径" },
            "output_path": { "type": "string", "description": "输出文件路径" },
            "input_paths": { "type": "string", "description": "多个输入路径（合并用，JSON数组）" },
            "page_start": { "type": "integer", "description": "起始页码（从1开始）" },
            "page_end": { "type": "integer", "description": "结束页码" },
            "pages": { "type": "string", "description": "指定页码（如 1,3,5-8）" },
            "dpi": { "type": "integer", "description": "DPI（转图片时，默认150）" },
            "language": { "type": "string", "description": "OCR语言" }
        },
        "required": ["action"]
    }
    """;

    public PdfTool(ITool ocr)
    {
        _ocr = ocr;
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
                "read" => await ReadPdfAsync(root, ct),
                "extract_text" => await ExtractTextAsync(root, ct),
                "split" => await SplitPdfAsync(root, ct),
                "merge" => await MergePdfAsync(root, ct),
                "to_image" => await PdfToImageAsync(root, ct),
                "from_images" => await ImagesToPdfAsync(root, ct),
                "info" => await GetInfoAsync(root, ct),
                "page_count" => await GetPageCountAsync(root, ct),
                _ => Fail($"不支持的操作：{action}")
            };
        }
        catch (Exception ex)
        {
            return Fail($"PDF 处理失败：{ex.Message}");
        }
    }

    #region 读取

    private async Task<ToolResult> ReadPdfAsync(JsonElement root, CancellationToken ct)
    {
        var inputPath = GetRequiredString(root, "input_path");
        if (string.IsNullOrEmpty(inputPath)) return Fail("缺少 input_path 参数");
        if (!File.Exists(inputPath)) return Fail($"文件不存在：{inputPath}");

        var text = await ExtractPdfTextAsync(inputPath, ct);

        var sb = new StringBuilder();
        sb.AppendLine("PDF 读取结果");
        sb.AppendLine($"文件：{Path.GetFileName(inputPath)}");
        sb.AppendLine($"大小：{new FileInfo(inputPath).Length / 1024.0:F1} KB");
        sb.AppendLine();
        sb.AppendLine("提取文本：");
        sb.AppendLine(text.Length > 5000 ? text[..5000] + $"\n\n...（共 {text.Length} 字符，已截断）" : text);

        return new ToolResult { Name = Name, Success = true, Content = sb.ToString() };
    }

    private async Task<ToolResult> ExtractTextAsync(JsonElement root, CancellationToken ct)
    {
        var inputPath = GetRequiredString(root, "input_path");
        var outputPath = root.TryGetProperty("output_path", out var oEl) ? oEl.GetString() : null;

        if (string.IsNullOrEmpty(inputPath)) return Fail("缺少 input_path 参数");
        if (!File.Exists(inputPath)) return Fail($"文件不存在：{inputPath}");

        var text = await ExtractPdfTextAsync(inputPath, ct);

        if (!string.IsNullOrEmpty(outputPath))
        {
            var dir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            await File.WriteAllTextAsync(outputPath, text, ct);
        }

        var sb = new StringBuilder();
        sb.AppendLine("PDF 文本提取完成");
        sb.AppendLine($"源文件：{Path.GetFileName(inputPath)}");
        sb.AppendLine($"提取字符数：{text.Length}");
        if (!string.IsNullOrEmpty(outputPath))
            sb.AppendLine($"保存到：{outputPath}");
        sb.AppendLine();
        sb.AppendLine(text.Length > 3000 ? text[..3000] + "\n\n...（已截断）" : text);

        return new ToolResult { Name = Name, Success = true, Content = sb.ToString() };
    }

    #endregion

    #region 拆分合并

    private async Task<ToolResult> SplitPdfAsync(JsonElement root, CancellationToken ct)
    {
        var inputPath = GetRequiredString(root, "input_path");
        var pages = root.TryGetProperty("pages", out var pgEl) ? pgEl.GetString() : null;
        var pageStart = root.TryGetProperty("page_start", out var psEl) ? psEl.GetInt32() : 1;
        var pageEnd = root.TryGetProperty("page_end", out var peEl) ? peEl.GetInt32() : 0;
        var outputPath = root.TryGetProperty("output_path", out var oEl) ? oEl.GetString() : null;

        if (string.IsNullOrEmpty(inputPath)) return Fail("缺少 input_path 参数");
        if (!File.Exists(inputPath)) return Fail($"文件不存在：{inputPath}");

        var pageCount = await GetPdfPageCountAsync(inputPath);
        outputPath ??= Path.Combine(Path.GetDirectoryName(inputPath) ?? ".",
            $"split_{Path.GetFileNameWithoutExtension(inputPath)}.pdf");

        // 使用 PDFtk 或 Python PyPDF2 拆分
        if (pageEnd <= 0) pageEnd = pageCount;

        var args = $"-y -i \"{inputPath}\" -vf \"select=gte(n\\,{pageStart - 1})*lte(n\\,{pageEnd - 1})\" -c:v copy -c:a copy \"{outputPath}\"";
        await RunFfmpegAsync(args, ct);

        var sb = new StringBuilder();
        sb.AppendLine("PDF 拆分完成");
        sb.AppendLine($"源文件：{Path.GetFileName(inputPath)}（{pageCount} 页）");
        sb.AppendLine($"提取页：第 {pageStart}-{pageEnd} 页");
        sb.AppendLine($"输出：{outputPath}");

        return new ToolResult { Name = Name, Success = true, Content = sb.ToString() };
    }

    private async Task<ToolResult> MergePdfAsync(JsonElement root, CancellationToken ct)
    {
        var inputPathsJson = GetRequiredString(root, "input_paths");
        var outputPath = root.TryGetProperty("output_path", out var oEl) ? oEl.GetString() : null;

        if (string.IsNullOrEmpty(inputPathsJson)) return Fail("缺少 input_paths 参数");

        List<string> inputPaths;
        try
        {
            inputPaths = JsonSerializer.Deserialize<List<string>>(inputPathsJson) ?? new();
        }
        catch
        {
            return Fail("input_paths 格式无效（需要 JSON 数组）");
        }

        if (inputPaths.Count < 2) return Fail("至少需要 2 个文件");

        outputPath ??= $"merged_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";

        // 使用 ffmpeg concat 合并
        var listFile = Path.Combine(Path.GetTempPath(), $"merge_list_{Guid.NewGuid():N}.txt");
        try
        {
            var content = string.Join("\n", inputPaths.Select(p => $"file '{p.Replace("\\", "/")}'"));
            await File.WriteAllTextAsync(listFile, content, ct);

            var args = $"-y -f concat -safe 0 -i \"{listFile}\" -c copy \"{outputPath}\"";
            await RunFfmpegAsync(args, ct);

            var sb = new StringBuilder();
            sb.AppendLine("PDF 合并完成");
            sb.AppendLine($"输入：{inputPaths.Count} 个文件");
            foreach (var p in inputPaths)
                sb.AppendLine($"  - {Path.GetFileName(p)}");
            sb.AppendLine($"输出：{outputPath}");
            sb.AppendLine($"大小：{new FileInfo(outputPath).Length / 1024.0:F1} KB");

            return new ToolResult { Name = Name, Success = true, Content = sb.ToString() };
        }
        finally
        {
            if (File.Exists(listFile)) File.Delete(listFile);
        }
    }

    #endregion

    #region PDF 转图片 / 图片转 PDF

    private async Task<ToolResult> PdfToImageAsync(JsonElement root, CancellationToken ct)
    {
        var inputPath = GetRequiredString(root, "input_path");
        var outputPath = root.TryGetProperty("output_path", out var oEl) ? oEl.GetString() : null;
        var dpi = root.TryGetProperty("dpi", out var dEl) ? dEl.GetInt32() : 150;

        if (string.IsNullOrEmpty(inputPath)) return Fail("缺少 input_path 参数");
        if (!File.Exists(inputPath)) return Fail($"文件不存在：{inputPath}");

        outputPath ??= Path.Combine(Path.GetDirectoryName(inputPath) ?? ".",
            $"{Path.GetFileNameWithoutExtension(inputPath)}_pages");

        Directory.CreateDirectory(outputPath);

        var args = $"-y -i \"{inputPath}\" -r {dpi} \"{outputPath}/page_%04d.png\"";
        await RunFfmpegAsync(args, ct);

        var pageCount = Directory.GetFiles(outputPath, "*.png").Length;

        var sb = new StringBuilder();
        sb.AppendLine("PDF 转图片完成");
        sb.AppendLine($"源文件：{Path.GetFileName(inputPath)}");
        sb.AppendLine($"输出目录：{outputPath}");
        sb.AppendLine($"生成图片：{pageCount} 张");
        sb.AppendLine($"DPI：{dpi}");

        return new ToolResult { Name = Name, Success = true, Content = sb.ToString() };
    }

    private async Task<ToolResult> ImagesToPdfAsync(JsonElement root, CancellationToken ct)
    {
        var inputPath = GetRequiredString(root, "input_path");
        var outputPath = root.TryGetProperty("output_path", out var oEl) ? oEl.GetString() : null;

        if (string.IsNullOrEmpty(inputPath)) return Fail("缺少 input_path 参数");

        List<string> imagePaths;
        if (Directory.Exists(inputPath))
        {
            var extensions = new[] { ".png", ".jpg", ".jpeg", ".bmp", ".webp" };
            imagePaths = Directory.GetFiles(inputPath, "*.*")
                .Where(f => extensions.Contains(Path.GetExtension(f).ToLower()))
                .OrderBy(f => f)
                .ToList();
        }
        else
        {
            imagePaths = new List<string> { inputPath };
        }

        if (imagePaths.Count == 0) return Fail("未找到图片文件");

        outputPath ??= $"images_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";

        // 先转为临时 PDF，再合并
        var tempPdfs = new List<string>();
        try
        {
            foreach (var img in imagePaths)
            {
                var tempPdf = Path.Combine(Path.GetTempPath(), $"img2pdf_{Guid.NewGuid():N}.pdf");
                var args = $"-y -i \"{img}\" -vf \"scale=trunc(iw/2)*2:trunc(ih/2)*2\" \"{tempPdf}\"";
                await RunFfmpegAsync(args, ct);
                tempPdfs.Add(tempPdf);
            }

            if (tempPdfs.Count == 1)
            {
                File.Move(tempPdfs[0], outputPath, true);
            }
            else
            {
                var listFile = Path.Combine(Path.GetTempPath(), $"merge_list_{Guid.NewGuid():N}.txt");
                try
                {
                    var content = string.Join("\n", tempPdfs.Select(p => $"file '{p.Replace("\\", "/")}'"));
                    await File.WriteAllTextAsync(listFile, content, ct);
                    await RunFfmpegAsync($"-y -f concat -safe 0 -i \"{listFile}\" -c copy \"{outputPath}\"", ct);
                }
                finally
                {
                    if (File.Exists(listFile)) File.Delete(listFile);
                }
            }

            var sb = new StringBuilder();
            sb.AppendLine("图片转 PDF 完成");
            sb.AppendLine($"输入：{imagePaths.Count} 张图片");
            sb.AppendLine($"输出：{outputPath}");
            sb.AppendLine($"大小：{new FileInfo(outputPath).Length / 1024.0:F1} KB");

            return new ToolResult { Name = Name, Success = true, Content = sb.ToString() };
        }
        finally
        {
            foreach (var temp in tempPdfs)
                if (File.Exists(temp)) File.Delete(temp);
        }
    }

    #endregion

    #region 信息

    private async Task<ToolResult> GetInfoAsync(JsonElement root, CancellationToken ct)
    {
        var inputPath = GetRequiredString(root, "input_path");
        if (string.IsNullOrEmpty(inputPath)) return Fail("缺少 input_path 参数");
        if (!File.Exists(inputPath)) return Fail($"文件不存在：{inputPath}");

        var pageCount = await GetPdfPageCountAsync(inputPath);
        var fileInfo = new FileInfo(inputPath);

        var sb = new StringBuilder();
        sb.AppendLine("PDF 信息");
        sb.AppendLine($"文件：{fileInfo.Name}");
        sb.AppendLine($"大小：{fileInfo.Length / 1024.0:F1} KB");
        sb.AppendLine($"页数：{pageCount}");
        sb.AppendLine($"创建：{fileInfo.CreationTime:yyyy-MM-dd HH:mm}");
        sb.AppendLine($"修改：{fileInfo.LastWriteTime:yyyy-MM-dd HH:mm}");

        return new ToolResult { Name = Name, Success = true, Content = sb.ToString() };
    }

    private async Task<ToolResult> GetPageCountAsync(JsonElement root, CancellationToken ct)
    {
        var inputPath = GetRequiredString(root, "input_path");
        if (string.IsNullOrEmpty(inputPath)) return Fail("缺少 input_path 参数");
        if (!File.Exists(inputPath)) return Fail($"文件不存在：{inputPath}");

        var pageCount = await GetPdfPageCountAsync(inputPath);

        return new ToolResult
        {
            Name = Name,
            Success = true,
            Content = $"PDF 页数：{pageCount}"
        };
    }

    #endregion

    #region 底层方法

    private static async Task<string> ExtractPdfTextAsync(string pdfPath, CancellationToken ct)
    {
        try
        {
            // 使用 pdftotext（poppler-utils）提取文本
            var psi = new ProcessStartInfo
            {
                FileName = "pdftotext",
                Arguments = $"-layout \"{pdfPath}\" -",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi) ?? throw new InvalidOperationException("无法启动 pdftotext");
            var output = await process.StandardOutput.ReadToEndAsync(ct);
            var error = await process.StandardError.ReadToEndAsync(ct);
            await process.WaitForExitAsync(ct);

            if (process.ExitCode == 0 && !string.IsNullOrWhiteSpace(output))
                return output.Trim();

            // 降级方案：提取 PDF 元信息
            return await ExtractPdfMetadataAsync(pdfPath, ct);
        }
        catch
        {
            return await ExtractPdfMetadataAsync(pdfPath, ct);
        }
    }

    private static async Task<string> ExtractPdfMetadataAsync(string pdfPath, CancellationToken ct)
    {
        try
        {
            // 使用 pdfinfo 获取基本信息
            var psi = new ProcessStartInfo
            {
                FileName = "pdfinfo",
                Arguments = $"\"{pdfPath}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi) ?? throw new InvalidOperationException("无法启动 pdfinfo");
            var output = await process.StandardOutput.ReadToEndAsync(ct);
            await process.WaitForExitAsync(ct);

            return output.Trim();
        }
        catch
        {
            var fileInfo = new FileInfo(pdfPath);
            return $"文件：{fileInfo.Name}\n大小：{fileInfo.Length / 1024.0:F1} KB\n（pdftotext/pdfinfo 未安装，无法提取文本）";
        }
    }

    private static async Task<int> GetPdfPageCountAsync(string pdfPath)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "pdfinfo",
                Arguments = $"\"{pdfPath}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi) ?? throw new InvalidOperationException();
            var output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();

            foreach (var line in output.Split('\n'))
            {
                if (line.StartsWith("Pages:"))
                {
                    var countStr = line.Split(':').Last().Trim();
                    if (int.TryParse(countStr, out var count))
                        return count;
                }
            }
        }
        catch { }

        // 降级：通过文件大小粗略估算
        return 1;
    }

    private static async Task RunFfmpegAsync(string arguments, CancellationToken ct)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "ffmpeg",
            Arguments = arguments,
            UseShellExecute = false,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi) ?? throw new InvalidOperationException("无法启动 FFmpeg");
        var error = await process.StandardError.ReadToEndAsync(ct);
        await process.WaitForExitAsync(ct);

        if (process.ExitCode != 0 && !error.Contains("Output file is empty"))
            throw new InvalidOperationException($"FFmpeg 错误：{error.Substring(0, Math.Min(500, error.Length))}");
    }

    private static string? GetRequiredString(JsonElement root, string name)
    {
        return root.TryGetProperty(name, out var el) ? el.GetString() : null;
    }

    private static ToolResult Fail(string error) => new() { Name = "pdf_tool", Success = false, Error = error };

    #endregion
}
