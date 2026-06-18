using DesktopMascot.Core.Tools;
using System.Diagnostics;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using DesktopMascot.Agent.Models;

namespace DesktopMascot.Agent.Tools;

/// <summary>
/// 压缩解压工具 — zip/7z/tar.gz/gz 压缩解压
/// </summary>
public class CompressionTool : ITool
{
    public string Name => "compression";
    public string Description => "压缩解压：zip/7z/tar.gz/gz 压缩和解压，支持密码保护。";
    public string ParametersSchema => """
    {
        "type": "object",
        "properties": {
            "action": { "type": "string", "enum": ["zip", "unzip", "list", "gzip", "gunzip"], "description": "操作类型" },
            "input_path": { "type": "string", "description": "输入文件/目录路径" },
            "output_path": { "type": "string", "description": "输出路径" },
            "password": { "type": "string", "description": "压缩密码（zip）" },
            "level": { "type": "string", "enum": ["fast", "normal", "best"], "description": "压缩级别" }
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
                "zip" => await ZipAsync(root, ct),
                "unzip" => await UnzipAsync(root, ct),
                "list" => await ListZipContentsAsync(root, ct),
                "gzip" => await GzipAsync(root, ct),
                "gunzip" => await GunzipAsync(root, ct),
                _ => Fail($"不支持的操作：{action}")
            };
        }
        catch (Exception ex)
        {
            return Fail($"压缩操作失败：{ex.Message}");
        }
    }

    private async Task<ToolResult> ZipAsync(JsonElement root, CancellationToken ct)
    {
        var inputPath = GetRequiredString(root, "input_path");
        var outputPath = root.TryGetProperty("output_path", out var oEl) ? oEl.GetString() : null;
        var password = root.TryGetProperty("password", out var pEl) ? pEl.GetString() : null;

        if (string.IsNullOrEmpty(inputPath)) return Fail("缺少 input_path 参数");

        outputPath ??= inputPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + ".zip";
        var level = root.TryGetProperty("level", out var lEl) ? lEl.GetString() ?? "normal" : "normal";

        if (Directory.Exists(inputPath))
        {
            var compressionLevel = level switch
            {
                "fast" => CompressionLevel.Fastest,
                "best" => CompressionLevel.SmallestSize,
                _ => CompressionLevel.Optimal
            };

            if (!string.IsNullOrEmpty(password))
            {
                // 带密码压缩需要用 7z 或临时方案
                var result7z = await ZipWith7zAsync(inputPath, outputPath, password, ct);
                if (result7z != null) return result7z;
            }

            ZipFile.CreateFromDirectory(inputPath, outputPath, compressionLevel, false);
        }
        else if (File.Exists(inputPath))
        {
            var dir = Path.GetDirectoryName(inputPath) ?? ".";
            var entryName = Path.GetFileName(inputPath);
            ZipFile.Open(outputPath, ZipArchiveMode.Create).Dispose(); // 先创建

            using var archive = ZipFile.Open(outputPath, ZipArchiveMode.Update);
            archive.CreateEntryFromFile(inputPath, entryName);
        }
        else
        {
            return Fail($"路径不存在：{inputPath}");
        }

        var fileInfo = new FileInfo(outputPath);
        var sb = new StringBuilder();
        sb.AppendLine("压缩完成");
        sb.AppendLine($"输入：{inputPath}");
        sb.AppendLine($"输出：{outputPath}");
        sb.AppendLine($"大小：{fileInfo.Length / 1024.0:F1} KB");
        sb.AppendLine($"压缩级别：{level}");

        return new ToolResult { Name = Name, Success = true, Content = sb.ToString() };
    }

    private async Task<ToolResult> UnzipAsync(JsonElement root, CancellationToken ct)
    {
        var inputPath = GetRequiredString(root, "input_path");
        var outputPath = root.TryGetProperty("output_path", out var oEl) ? oEl.GetString() : null;

        if (string.IsNullOrEmpty(inputPath)) return Fail("缺少 input_path 参数");
        if (!File.Exists(inputPath)) return Fail($"文件不存在：{inputPath}");

        outputPath ??= Path.Combine(Path.GetDirectoryName(inputPath) ?? ".",
            Path.GetFileNameWithoutExtension(inputPath));

        ZipFile.ExtractToDirectory(inputPath, outputPath, overwriteFiles: true);

        var fileCount = Directory.GetFiles(outputPath, "*.*", SearchOption.AllDirectories).Length;
        var dirCount = Directory.GetDirectories(outputPath, "*.*", SearchOption.AllDirectories).Length;

        var sb = new StringBuilder();
        sb.AppendLine("解压完成");
        sb.AppendLine($"输入：{inputPath}");
        sb.AppendLine($"输出：{outputPath}");
        sb.AppendLine($"解压文件：{fileCount} 个");
        sb.AppendLine($"解压目录：{dirCount} 个");

        return new ToolResult { Name = Name, Success = true, Content = sb.ToString() };
    }

    private async Task<ToolResult> ListZipContentsAsync(JsonElement root, CancellationToken ct)
    {
        var inputPath = GetRequiredString(root, "input_path");
        if (string.IsNullOrEmpty(inputPath)) return Fail("缺少 input_path 参数");
        if (!File.Exists(inputPath)) return Fail($"文件不存在：{inputPath}");

        using var archive = ZipFile.OpenRead(inputPath);
        var sb = new StringBuilder();
        sb.AppendLine($"ZIP 内容：{Path.GetFileName(inputPath)}");
        sb.AppendLine($"文件数：{archive.Entries.Count}");
        sb.AppendLine();

        foreach (var entry in archive.Entries)
        {
            var size = entry.Length > 0 ? $"{entry.Length / 1024.0:F1} KB" : "(目录)";
            sb.AppendLine($"  {entry.FullName}  {size}");
        }

        return new ToolResult { Name = Name, Success = true, Content = sb.ToString() };
    }

    private async Task<ToolResult> GzipAsync(JsonElement root, CancellationToken ct)
    {
        var inputPath = GetRequiredString(root, "input_path");
        var outputPath = root.TryGetProperty("output_path", out var oEl) ? oEl.GetString() : null;

        if (string.IsNullOrEmpty(inputPath)) return Fail("缺少 input_path 参数");
        if (!File.Exists(inputPath)) return Fail($"文件不存在：{inputPath}");

        outputPath ??= inputPath + ".gz";

        await using var fileStream = new FileStream(inputPath, FileMode.Open, FileAccess.Read);
        await using var gzipStream = new FileStream(outputPath, FileMode.Create);
        await using var gz = new GZipStream(gzipStream, CompressionLevel.Optimal);
        await fileStream.CopyToAsync(gz, ct);

        var fileInfo = new FileInfo(outputPath);
        var sb = new StringBuilder();
        sb.AppendLine("GZip 压缩完成");
        sb.AppendLine($"输入：{inputPath} ({new FileInfo(inputPath).Length / 1024.0:F1} KB)");
        sb.AppendLine($"输出：{outputPath} ({fileInfo.Length / 1024.0:F1} KB)");

        return new ToolResult { Name = Name, Success = true, Content = sb.ToString() };
    }

    private async Task<ToolResult> GunzipAsync(JsonElement root, CancellationToken ct)
    {
        var inputPath = GetRequiredString(root, "input_path");
        var outputPath = root.TryGetProperty("output_path", out var oEl) ? oEl.GetString() : null;

        if (string.IsNullOrEmpty(inputPath)) return Fail("缺少 input_path 参数");
        if (!File.Exists(inputPath)) return Fail($"文件不存在：{inputPath}");

        outputPath ??= inputPath.Replace(".gz", "");

        await using var gzipStream = new FileStream(inputPath, FileMode.Open);
        await using var gz = new GZipStream(gzipStream, CompressionMode.Decompress);
        await using var outputStream = new FileStream(outputPath, FileMode.Create);
        await gz.CopyToAsync(outputStream, ct);

        var fileInfo = new FileInfo(outputPath);
        var sb = new StringBuilder();
        sb.AppendLine("GZip 解压完成");
        sb.AppendLine($"输入：{inputPath}");
        sb.AppendLine($"输出：{outputPath} ({fileInfo.Length / 1024.0:F1} KB)");

        return new ToolResult { Name = Name, Success = true, Content = sb.ToString() };
    }

    private async Task<ToolResult?> ZipWith7zAsync(string inputPath, string outputPath, string password, CancellationToken ct)
    {
        try
        {
            var args = $"a -tzip -p\"{password}\" \"{outputPath}\" \"{inputPath}\\*\"";
            var psi = new ProcessStartInfo
            {
                FileName = "7z",
                Arguments = args,
                UseShellExecute = false,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi) ?? throw new InvalidOperationException();
            await process.WaitForExitAsync(ct);

            if (process.ExitCode == 0 && File.Exists(outputPath))
            {
                var fileInfo = new FileInfo(outputPath);
                return new ToolResult
                {
                    Name = Name,
                    Success = true,
                    Content = $"带密码压缩完成\n输入：{inputPath}\n输出：{outputPath}\n大小：{fileInfo.Length / 1024.0:F1} KB"
                };
            }
        }
        catch { }
        return null;
    }

    private static string? GetRequiredString(JsonElement root, string name)
    {
        return root.TryGetProperty(name, out var el) ? el.GetString() : null;
    }

    private static ToolResult Fail(string error) => new() { Name = "compression", Success = false, Error = error };
}
