using DesktopMascot.Core.Tools;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DesktopMascot.Agent.Models;

namespace DesktopMascot.Agent.Tools;

/// <summary>
/// 文件整理工具 — 按类型/日期/大小自动分类归档 + 重复文件查找
/// </summary>
public class FileOrganizerTool : ITool
{
    public string Name => "file_organizer";
    public string Description => "文件整理：按类型/日期/大小自动分类归档，查找重复文件，清理空目录。";
    public string ParametersSchema => """
    {
        "type": "object",
        "properties": {
            "action": { "type": "string", "enum": ["organize_by_type", "organize_by_date", "organize_by_size", "find_duplicates", "cleanup_empty", "file_stats", "large_files"], "description": "操作类型" },
            "directory": { "type": "string", "description": "目标目录" },
            "output_dir": { "type": "string", "description": "归档目标目录" },
            "min_size_mb": { "type": "integer", "description": "最小文件大小 MB（large_files）" },
            "dry_run": { "type": "boolean", "description": "预览模式（不实际移动）" }
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
                "organize_by_type" => OrganizeByType(root, ct),
                "organize_by_date" => OrganizeByDate(root, ct),
                "organize_by_size" => OrganizeBySize(root, ct),
                "find_duplicates" => await FindDuplicatesAsync(root, ct),
                "cleanup_empty" => CleanupEmpty(root),
                "file_stats" => FileStats(root),
                "large_files" => LargeFiles(root),
                _ => Fail($"不支持的操作：{action}")
            };
        }
        catch (Exception ex)
        {
            return Fail($"文件整理失败：{ex.Message}");
        }
    }

    private ToolResult OrganizeByType(JsonElement root, CancellationToken ct)
    {
        var directory = GetRequiredString(root, "directory");
        var outputDir = root.TryGetProperty("output_dir", out var oEl) ? oEl.GetString() : null;
        var dryRun = root.TryGetProperty("dry_run", out var drEl) && drEl.GetBoolean();

        if (string.IsNullOrEmpty(directory)) return Fail("缺少 directory 参数");
        if (!Directory.Exists(directory)) return Fail($"目录不存在：{directory}");

        outputDir ??= Path.Combine(directory, "_organized");

        var typeCategories = new Dictionary<string, string>
        {
            [".jpg"] = "Images", [".jpeg"] = "Images", [".png"] = "Images",
            [".gif"] = "Images", [".bmp"] = "Images", [".webp"] = "Images", [".svg"] = "Images",
            [".mp4"] = "Videos", [".avi"] = "Videos", [".mkv"] = "Videos", [".mov"] = "Videos", [".wmv"] = "Videos",
            [".mp3"] = "Audio", [".wav"] = "Audio", [".flac"] = "Audio", [".aac"] = "Audio", [".ogg"] = "Audio",
            [".pdf"] = "Documents", [".doc"] = "Documents", [".docx"] = "Documents",
            [".xls"] = "Documents", [".xlsx"] = "Documents", [".pptx"] = "Documents",
            [".txt"] = "Documents", [".md"] = "Documents",
            [".zip"] = "Archives", [".rar"] = "Archives", [".7z"] = "Archives", [".tar"] = "Archives", [".gz"] = "Archives",
            [".cs"] = "Code", [".js"] = "Code", [".ts"] = "Code", [".py"] = "Code",
            [".java"] = "Code", [".go"] = "Code", [".rs"] = "Code", [".html"] = "Code", [".css"] = "Code",
            [".json"] = "Code", [".xml"] = "Code", [".yaml"] = "Code", [".yml"] = "Code",
            [".exe"] = "Executables", [".msi"] = "Executables", [".dll"] = "Executables",
        };

        var files = Directory.GetFiles(directory, "*.*", SearchOption.TopDirectoryOnly);
        var moved = 0;
        var sb = new StringBuilder();
        sb.AppendLine(dryRun ? "预览：按类型分类" : "按类型分类整理");
        sb.AppendLine($"源目录：{directory}");
        sb.AppendLine($"文件数：{files.Length}");
        sb.AppendLine();

        foreach (var file in files)
        {
            ct.ThrowIfCancellationRequested();
            var ext = Path.GetExtension(file).ToLower();
            var category = typeCategories.TryGetValue(ext, out var cat) ? cat : "Other";

            var targetDir = Path.Combine(outputDir, category);
            var targetPath = Path.Combine(targetDir, Path.GetFileName(file));

            if (!dryRun)
            {
                Directory.CreateDirectory(targetDir);
                if (!File.Exists(targetPath))
                    File.Move(file, targetPath);
            }

            sb.AppendLine($"  {Path.GetFileName(file)} → {category}/");
            moved++;
        }

        sb.AppendLine();
        sb.AppendLine($"整理了 {moved} 个文件");
        if (!dryRun) sb.AppendLine($"目标：{outputDir}");

        return new ToolResult { Name = Name, Success = true, Content = sb.ToString() };
    }

    private ToolResult OrganizeByDate(JsonElement root, CancellationToken ct)
    {
        var directory = GetRequiredString(root, "directory");
        var outputDir = root.TryGetProperty("output_dir", out var oEl) ? oEl.GetString() : null;
        var dryRun = root.TryGetProperty("dry_run", out var drEl) && drEl.GetBoolean();

        if (string.IsNullOrEmpty(directory)) return Fail("缺少 directory 参数");
        if (!Directory.Exists(directory)) return Fail($"目录不存在：{directory}");

        outputDir ??= Path.Combine(directory, "_by_date");

        var files = Directory.GetFiles(directory, "*.*", SearchOption.TopDirectoryOnly);
        var moved = 0;
        var sb = new StringBuilder();
        sb.AppendLine(dryRun ? "预览：按日期分类" : "按日期分类整理");
        sb.AppendLine($"文件数：{files.Length}");
        sb.AppendLine();

        foreach (var file in files)
        {
            ct.ThrowIfCancellationRequested();
            var date = File.GetLastWriteTime(file);
            var dateDir = Path.Combine(outputDir, date.ToString("yyyy-MM-dd"));
            var targetPath = Path.Combine(dateDir, Path.GetFileName(file));

            if (!dryRun)
            {
                Directory.CreateDirectory(dateDir);
                if (!File.Exists(targetPath))
                    File.Move(file, targetPath);
            }

            sb.AppendLine($"  {Path.GetFileName(file)} → {date:yyyy-MM-dd}/");
            moved++;
        }

        sb.AppendLine($"\n整理了 {moved} 个文件");
        return new ToolResult { Name = Name, Success = true, Content = sb.ToString() };
    }

    private ToolResult OrganizeBySize(JsonElement root, CancellationToken ct)
    {
        var directory = GetRequiredString(root, "directory");
        var outputDir = root.TryGetProperty("output_dir", out var oEl) ? oEl.GetString() : null;
        var dryRun = root.TryGetProperty("dry_run", out var drEl) && drEl.GetBoolean();

        if (string.IsNullOrEmpty(directory)) return Fail("缺少 directory 参数");
        if (!Directory.Exists(directory)) return Fail($"目录不存在：{directory}");

        outputDir ??= Path.Combine(directory, "_by_size");

        var files = Directory.GetFiles(directory, "*.*", SearchOption.TopDirectoryOnly);
        var moved = 0;
        var sb = new StringBuilder();
        sb.AppendLine(dryRun ? "预览：按大小分类" : "按大小分类整理");
        sb.AppendLine($"文件数：{files.Length}");
        sb.AppendLine();

        foreach (var file in files)
        {
            ct.ThrowIfCancellationRequested();
            var size = new FileInfo(file).Length;
            var category = size switch
            {
                < 1024 => "Tiny (< 1KB)",
                < 1024 * 1024 => "Small (< 1MB)",
                < 10 * 1024 * 1024 => "Medium (< 10MB)",
                < 100 * 1024 * 1024 => "Large (< 100MB)",
                _ => "Huge (> 100MB)"
            };

            var targetDir = Path.Combine(outputDir, category);
            var targetPath = Path.Combine(targetDir, Path.GetFileName(file));

            if (!dryRun)
            {
                Directory.CreateDirectory(targetDir);
                if (!File.Exists(targetPath))
                    File.Move(file, targetPath);
            }

            sb.AppendLine($"  {Path.GetFileName(file)} ({size / 1024.0:F1} KB) → {category}/");
            moved++;
        }

        sb.AppendLine($"\n整理了 {moved} 个文件");
        return new ToolResult { Name = Name, Success = true, Content = sb.ToString() };
    }

    private async Task<ToolResult> FindDuplicatesAsync(JsonElement root, CancellationToken ct)
    {
        var directory = GetRequiredString(root, "directory");
        if (string.IsNullOrEmpty(directory)) return Fail("缺少 directory 参数");
        if (!Directory.Exists(directory)) return Fail($"目录不存在：{directory}");

        var files = Directory.GetFiles(directory, "*.*", SearchOption.AllDirectories);
        var hashGroups = new Dictionary<string, List<string>>();

        foreach (var file in files)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var hash = await ComputeFileHashAsync(file, ct);
                if (!hashGroups.ContainsKey(hash))
                    hashGroups[hash] = new List<string>();
                hashGroups[hash].Add(file);
            }
            catch { }
        }

        var duplicates = hashGroups.Where(g => g.Value.Count > 1).ToList();
        var duplicateCount = duplicates.Sum(g => g.Value.Count - 1);

        var sb = new StringBuilder();
        sb.AppendLine("重复文件查找");
        sb.AppendLine($"扫描文件：{files.Length}");
        sb.AppendLine($"重复组：{duplicates.Count}");
        sb.AppendLine($"可释放空间：约 {duplicateCount} 个文件");
        sb.AppendLine();

        foreach (var group in duplicates.Take(20))
        {
            var size = new FileInfo(group.Value[0]).Length;
            sb.AppendLine($"  相同文件（{size / 1024.0:F1} KB，{group.Value.Count} 份）：");
            foreach (var path in group.Value.Take(5))
                sb.AppendLine($"    {path}");
            if (group.Value.Count > 5)
                sb.AppendLine($"    ... 还有 {group.Value.Count - 5} 个");
            sb.AppendLine();
        }

        if (duplicates.Count > 20)
            sb.AppendLine($"... 还有 {duplicates.Count - 20} 组重复文件");

        return new ToolResult { Name = Name, Success = true, Content = sb.ToString() };
    }

    private ToolResult CleanupEmpty(JsonElement root)
    {
        var directory = GetRequiredString(root, "directory");
        if (string.IsNullOrEmpty(directory)) return Fail("缺少 directory 参数");
        if (!Directory.Exists(directory)) return Fail($"目录不存在：{directory}");

        var dirs = Directory.GetDirectories(directory, "*.*", SearchOption.AllDirectories)
            .OrderByDescending(d => d.Length)
            .ToList();

        var removed = 0;
        foreach (var dir in dirs)
        {
            try
            {
                if (Directory.GetFileSystemEntries(dir).Length == 0)
                {
                    Directory.Delete(dir);
                    removed++;
                }
            }
            catch { }
        }

        return new ToolResult
        {
            Name = Name,
            Success = true,
            Content = $"清理完成\n删除了 {removed} 个空目录"
        };
    }

    private ToolResult FileStats(JsonElement root)
    {
        var directory = GetRequiredString(root, "directory");
        if (string.IsNullOrEmpty(directory)) return Fail("缺少 directory 参数");
        if (!Directory.Exists(directory)) return Fail($"目录不存在：{directory}");

        var files = Directory.GetFiles(directory, "*.*", SearchOption.AllDirectories);
        var totalSize = files.Sum(f => new FileInfo(f).Length);
        var extGroups = files.GroupBy(f => Path.GetExtension(f).ToLower())
            .OrderByDescending(g => g.Count())
            .ToList();

        var sb = new StringBuilder();
        sb.AppendLine("文件统计");
        sb.AppendLine($"目录：{directory}");
        sb.AppendLine($"文件总数：{files.Length}");
        sb.AppendLine($"总大小：{totalSize / 1024.0 / 1024.0:F1} MB");
        sb.AppendLine();
        sb.AppendLine("类型分布：");
        foreach (var g in extGroups.Take(15))
        {
            var size = g.Sum(f => new FileInfo(f).Length);
            sb.AppendLine($"  {g.Key,-10} {g.Count(),5} 个  {size / 1024.0 / 1024.0:F1} MB");
        }

        return new ToolResult { Name = Name, Success = true, Content = sb.ToString() };
    }

    private ToolResult LargeFiles(JsonElement root)
    {
        var directory = GetRequiredString(root, "directory");
        var minSizeMb = root.TryGetProperty("min_size_mb", out var msEl) ? msEl.GetInt32() : 100;

        if (string.IsNullOrEmpty(directory)) return Fail("缺少 directory 参数");
        if (!Directory.Exists(directory)) return Fail($"目录不存在：{directory}");

        var minSize = (long)minSizeMb * 1024 * 1024;
        var files = Directory.GetFiles(directory, "*.*", SearchOption.AllDirectories)
            .Select(f => new FileInfo(f))
            .Where(f => f.Length >= minSize)
            .OrderByDescending(f => f.Length)
            .ToList();

        var sb = new StringBuilder();
        sb.AppendLine($"大文件（> {minSizeMb} MB）");
        sb.AppendLine($"找到：{files.Count} 个");
        sb.AppendLine();

        foreach (var f in files.Take(20))
        {
            sb.AppendLine($"  {f.Length / 1024.0 / 1024.0:F1} MB  {f.FullName}");
        }

        return new ToolResult { Name = Name, Success = true, Content = sb.ToString() };
    }

    private static async Task<string> ComputeFileHashAsync(string filePath, CancellationToken ct)
    {
        using var sha256 = SHA256.Create();
        await using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920);
        var hashBytes = await sha256.ComputeHashAsync(stream, ct);
        return Convert.ToHexString(hashBytes);
    }

    private static string? GetRequiredString(JsonElement root, string name)
    {
        return root.TryGetProperty(name, out var el) ? el.GetString() : null;
    }

    private static ToolResult Fail(string error) => new() { Name = "file_organizer", Success = false, Error = error };
}
