using DesktopMascot.Core.Tools;
using System.Text.Json;
using DesktopMascot.Agent.Models;

namespace DesktopMascot.Agent.Tools;

/// <summary>
/// 文件对比工具 - 对比两个文件的差异
/// </summary>
public class FileCompareTool : ITool
{
    public string Name => "file_compare";
    public string Description => "对比两个文件的内容差异。支持文本文件和代码文件。";
    public string ParametersSchema => """
    {
        "type": "object",
        "properties": {
            "file1": { "type": "string", "description": "第一个文件路径" },
            "file2": { "type": "string", "description": "第二个文件路径" },
            "context_lines": { "type": "integer", "description": "上下文行数（默认3）" }
        },
        "required": ["file1", "file2"]
    }
    """;

    public async Task<ToolResult> ExecuteAsync(string arguments, CancellationToken ct = default)
    {
        try
        {
            var doc = JsonDocument.Parse(arguments);
            var root = doc.RootElement;

            if (!root.TryGetProperty("file1", out var f1Element) || !root.TryGetProperty("file2", out var f2Element))
                return Fail("缺少 file1 或 file2 参数");

            var file1 = f1Element.GetString() ?? "";
            var file2 = f2Element.GetString() ?? "";

            if (!File.Exists(file1)) return Fail($"文件不存在：{file1}");
            if (!File.Exists(file2)) return Fail($"文件不存在：{file2}");

            var lines1 = await File.ReadAllLinesAsync(file1, ct);
            var lines2 = await File.ReadAllLinesAsync(file2, ct);

            var diff = CompareLines(lines1, lines2);

            return new ToolResult
            {
                Name = Name,
                Success = true,
                Content = $"文件对比结果：\n{diff}\n\n文件1：{file1}（{lines1.Length} 行）\n文件2：{file2}（{lines2.Length} 行）"
            };
        }
        catch (Exception ex)
        {
            return Fail($"对比失败：{ex.Message}");
        }
    }

    private static string CompareLines(string[] lines1, string[] lines2)
    {
        var sb = new System.Text.StringBuilder();
        var maxLines = Math.Max(lines1.Length, lines2.Length);
        var diffCount = 0;

        for (int i = 0; i < maxLines; i++)
        {
            var line1 = i < lines1.Length ? lines1[i] : null;
            var line2 = i < lines2.Length ? lines2[i] : null;

            if (line1 != line2)
            {
                diffCount++;
                sb.AppendLine($"行 {i + 1}:");
                if (line1 != null) sb.AppendLine($"  - {line1}");
                if (line2 != null) sb.AppendLine($"  + {line2}");
            }
        }

        if (diffCount == 0)
            return "两个文件内容完全相同";

        sb.Insert(0, $"共发现 {diffCount} 处差异\n");
        return sb.ToString();
    }

    private static ToolResult Fail(string error) => new() { Name = "file_compare", Success = false, Error = error };
}

/// <summary>
/// 批量文件处理工具 - 对多个文件执行相同操作
/// </summary>
public class BatchFileProcessorTool : ITool
{
    public string Name => "batch_file_process";
    public string Description => "批量处理文件：重命名、移动、复制、删除。支持通配符和正则。";
    public string ParametersSchema => """
    {
        "type": "object",
        "properties": {
            "directory": { "type": "string", "description": "目标目录" },
            "action": { "type": "string", "enum": ["rename", "copy", "delete", "list"], "description": "操作类型" },
            "pattern": { "type": "string", "description": "文件匹配模式（如 *.txt）" },
            "search": { "type": "string", "description": "搜索关键词（rename模式：替换内容）" },
            "replace": { "type": "string", "description": "替换为（rename模式）" },
            "destination": { "type": "string", "description": "目标路径（copy模式）" }
        },
        "required": ["directory", "action"]
    }
    """;

    public async Task<ToolResult> ExecuteAsync(string arguments, CancellationToken ct = default)
    {
        try
        {
            var doc = JsonDocument.Parse(arguments);
            var root = doc.RootElement;

            var directory = root.TryGetProperty("directory", out var dEl) ? dEl.GetString() ?? "" : "";
            var action = root.TryGetProperty("action", out var aEl) ? aEl.GetString() ?? "" : "";
            var pattern = root.TryGetProperty("pattern", out var pEl) ? pEl.GetString() ?? "*.*" : "*.*";

            if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
                return Fail($"目录不存在：{directory}");

            var files = Directory.GetFiles(directory, pattern, SearchOption.TopDirectoryOnly);

            var results = new System.Text.StringBuilder();
            results.AppendLine($"在 {directory} 中找到 {files.Length} 个匹配文件");

            foreach (var file in files)
            {
                ct.ThrowIfCancellationRequested();
                var fileName = Path.GetFileName(file);

                switch (action)
                {
                    case "rename":
                        var search = root.TryGetProperty("search", out var sEl) ? sEl.GetString() ?? "" : "";
                        var replace = root.TryGetProperty("replace", out var rEl) ? rEl.GetString() ?? "" : "";
                        if (!string.IsNullOrEmpty(search))
                        {
                            var newFileName = fileName.Replace(search, replace);
                            var newPath = Path.Combine(directory, newFileName);
                            File.Move(file, newPath);
                            results.AppendLine($"  重命名：{fileName} → {newFileName}");
                        }
                        break;
                    case "copy":
                        var dest = root.TryGetProperty("destination", out var destEl) ? destEl.GetString() ?? "" : "";
                        if (!string.IsNullOrEmpty(dest))
                        {
                            Directory.CreateDirectory(dest);
                            File.Copy(file, Path.Combine(dest, fileName), true);
                            results.AppendLine($"  复制：{fileName} → {dest}");
                        }
                        break;
                    case "delete":
                        File.Delete(file);
                        results.AppendLine($"  删除：{fileName}");
                        break;
                    case "list":
                        results.AppendLine($"  {fileName}");
                        break;
                }
            }

            return new ToolResult
            {
                Name = Name,
                Success = true,
                Content = results.ToString()
            };
        }
        catch (Exception ex)
        {
            return Fail($"批量处理失败：{ex.Message}");
        }
    }

    private static ToolResult Fail(string error) => new() { Name = "batch_file_process", Success = false, Error = error };
}

/// <summary>
/// 文件版本管理工具 - 创建和恢复文件快照
/// </summary>
public class FileVersionTool : ITool
{
    private readonly string _versionDir;

    public FileVersionTool(string? versionDir = null)
    {
        _versionDir = versionDir ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "DesktopMascot", "versions");
        Directory.CreateDirectory(_versionDir);
    }

    public string Name => "file_version";
    public string Description => "管理文件版本：创建快照、查看历史、恢复版本。";
    public string ParametersSchema => """
    {
        "type": "object",
        "properties": {
            "action": { "type": "string", "enum": ["snapshot", "list", "restore", "diff"], "description": "操作类型" },
            "file_path": { "type": "string", "description": "文件路径" },
            "version_id": { "type": "string", "description": "版本ID（restore模式）" },
            "description": { "type": "string", "description": "版本描述（snapshot模式）" }
        },
        "required": ["action", "file_path"]
    }
    """;

    public async Task<ToolResult> ExecuteAsync(string arguments, CancellationToken ct = default)
    {
        try
        {
            var doc = JsonDocument.Parse(arguments);
            var root = doc.RootElement;

            var action = root.TryGetProperty("action", out var aEl) ? aEl.GetString() ?? "" : "";
            var filePath = root.TryGetProperty("file_path", out var fEl) ? fEl.GetString() ?? "" : "";

            if (string.IsNullOrEmpty(filePath))
                return Fail("缺少 file_path 参数");

            return action switch
            {
                "snapshot" => await CreateSnapshotAsync(filePath, root, ct),
                "list" => await ListVersionsAsync(filePath),
                "restore" => await RestoreVersionAsync(filePath, root, ct),
                "diff" => await DiffVersionAsync(filePath, root, ct),
                _ => Fail($"不支持的操作：{action}")
            };
        }
        catch (Exception ex)
        {
            return Fail($"操作失败：{ex.Message}");
        }
    }

    private async Task<ToolResult> CreateSnapshotAsync(string filePath, JsonElement root, CancellationToken ct)
    {
        if (!File.Exists(filePath)) return Fail($"文件不存在：{filePath}");

        var description = root.TryGetProperty("description", out var dEl) ? dEl.GetString() ?? "" : "";
        var versionId = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
        var versionDir = Path.Combine(_versionDir, Path.GetFileNameWithoutExtension(filePath));
        Directory.CreateDirectory(versionDir);

        var destPath = Path.Combine(versionDir, $"{versionId}{Path.GetExtension(filePath)}");
        File.Copy(filePath, destPath, true);

        var metaPath = Path.Combine(versionDir, $"{versionId}.meta.json");
        var meta = new { VersionId = versionId, FilePath = filePath, Description = description, CreatedAt = DateTime.UtcNow };
        await File.WriteAllTextAsync(metaPath, JsonSerializer.Serialize(meta, new JsonSerializerOptions { WriteIndented = true }), ct);

        return new ToolResult
        {
            Name = Name,
            Success = true,
            Content = $"已创建快照：{versionId}\n文件：{filePath}\n描述：{description}"
        };
    }

    private async Task<ToolResult> ListVersionsAsync(string filePath)
    {
        var versionDir = Path.Combine(_versionDir, Path.GetFileNameWithoutExtension(filePath));
        if (!Directory.Exists(versionDir))
            return new ToolResult { Name = Name, Success = true, Content = "暂无版本记录" };

        var versions = Directory.GetFiles(versionDir, "*.meta.json")
            .OrderByDescending(f => f)
            .ToList();

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"文件版本历史：{filePath}");
        sb.AppendLine();

        foreach (var metaFile in versions)
        {
            var json = await File.ReadAllTextAsync(metaFile);
            var meta = JsonSerializer.Deserialize<JsonElement>(json);
            var versionId = meta.TryGetProperty("VersionId", out var vId) ? vId.GetString() : "";
            var desc = meta.TryGetProperty("Description", out var descEl) ? descEl.GetString() : "";
            var created = meta.TryGetProperty("CreatedAt", out var cEl) ? cEl.GetString() : "";

            sb.AppendLine($"  {versionId} - {desc} ({created})");
        }

        return new ToolResult { Name = Name, Success = true, Content = sb.ToString() };
    }

    private async Task<ToolResult> RestoreVersionAsync(string filePath, JsonElement root, CancellationToken ct)
    {
        var versionId = root.TryGetProperty("version_id", out var vEl) ? vEl.GetString() ?? "" : "";
        if (string.IsNullOrEmpty(versionId)) return Fail("缺少 version_id 参数");

        var versionDir = Path.Combine(_versionDir, Path.GetFileNameWithoutExtension(filePath));
        var versionFile = Path.Combine(versionDir, $"{versionId}{Path.GetExtension(filePath)}");

        if (!File.Exists(versionFile)) return Fail($"版本不存在：{versionId}");

        File.Copy(versionFile, filePath, true);

        return new ToolResult
        {
            Name = Name,
            Success = true,
            Content = $"已恢复到版本：{versionId}"
        };
    }

    private async Task<ToolResult> DiffVersionAsync(string filePath, JsonElement root, CancellationToken ct)
    {
        var versionId = root.TryGetProperty("version_id", out var vEl) ? vEl.GetString() ?? "" : "";
        if (string.IsNullOrEmpty(versionId)) return Fail("缺少 version_id 参数");

        var versionDir = Path.Combine(_versionDir, Path.GetFileNameWithoutExtension(filePath));
        var versionFile = Path.Combine(versionDir, $"{versionId}{Path.GetExtension(filePath)}");

        if (!File.Exists(versionFile)) return Fail($"版本不存在：{versionId}");
        if (!File.Exists(filePath)) return Fail($"当前文件不存在：{filePath}");

        var oldLines = await File.ReadAllLinesAsync(versionFile, ct);
        var newLines = await File.ReadAllLinesAsync(filePath, ct);

        var diff = CompareLines(oldLines, newLines);

        return new ToolResult
        {
            Name = Name,
            Success = true,
            Content = $"与版本 {versionId} 的对比：\n{diff}"
        };
    }

    private static string CompareLines(string[] lines1, string[] lines2)
    {
        var sb = new System.Text.StringBuilder();
        var maxLines = Math.Max(lines1.Length, lines2.Length);
        var diffCount = 0;

        for (int i = 0; i < maxLines; i++)
        {
            var line1 = i < lines1.Length ? lines1[i] : null;
            var line2 = i < lines2.Length ? lines2[i] : null;

            if (line1 != line2)
            {
                diffCount++;
                sb.AppendLine($"行 {i + 1}:");
                if (line1 != null) sb.AppendLine($"  - {line1}");
                if (line2 != null) sb.AppendLine($"  + {line2}");
            }
        }

        if (diffCount == 0) return "内容完全相同";
        sb.Insert(0, $"共 {diffCount} 处差异\n");
        return sb.ToString();
    }

    private static ToolResult Fail(string error) => new() { Name = "file_version", Success = false, Error = error };
}
