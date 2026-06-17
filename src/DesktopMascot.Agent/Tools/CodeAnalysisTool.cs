using DesktopMascot.Core.Tools;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using DesktopMascot.Agent.Models;

namespace DesktopMascot.Agent.Tools;

/// <summary>
/// 代码分析工具 - 分析代码质量、复杂度、依赖
/// </summary>
public class CodeAnalysisTool : ITool
{
    public string Name => "code_analysis";
    public string Description => "分析代码文件：质量检查、复杂度分析、依赖提取、代码统计。";
    public string ParametersSchema => """
    {
        "type": "object",
        "properties": {
            "action": { "type": "string", "enum": ["quality", "complexity", "dependencies", "stats", "structure"], "description": "分析类型" },
            "file_path": { "type": "string", "description": "文件路径" },
            "directory": { "type": "string", "description": "目录路径（structure模式）" },
            "language": { "type": "string", "description": "编程语言提示" }
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
            var directory = root.TryGetProperty("directory", out var dEl) ? dEl.GetString() ?? "" : "";

            return action switch
            {
                "quality" => await AnalyzeQualityAsync(filePath, ct),
                "complexity" => await AnalyzeComplexityAsync(filePath, ct),
                "dependencies" => await AnalyzeDependenciesAsync(filePath, ct),
                "stats" => await AnalyzeStatsAsync(filePath, ct),
                "structure" => await AnalyzeStructureAsync(directory, ct),
                _ => Fail($"不支持的分析类型：{action}")
            };
        }
        catch (Exception ex)
        {
            return Fail($"代码分析失败：{ex.Message}");
        }
    }

    private async Task<ToolResult> AnalyzeQualityAsync(string filePath, CancellationToken ct)
    {
        if (!File.Exists(filePath)) return Fail($"文件不存在：{filePath}");

        var content = await File.ReadAllTextAsync(filePath, ct);
        var lines = content.Split('\n');
        var issues = new List<string>();

        // 检查空行过多
        var emptyLines = lines.Count(string.IsNullOrWhiteSpace);
        if (emptyLines > lines.Length * 0.3)
            issues.Add($"空行过多：{emptyLines}/{lines.Length} 行");

        // 检查行长度
        var longLines = lines.Where(l => l.Length > 120).ToList();
        if (longLines.Count > 0)
            issues.Add($"长行：{longLines.Count} 行超过 120 字符");

        // 检查重复代码
        var duplicateCheck = CheckDuplicateLines(lines);
        if (duplicateCheck > 0)
            issues.Add($"重复代码：{duplicateCheck} 行重复");

        // 检查 TODO/FIXME
        var todoCount = lines.Count(l => l.Contains("TODO") || l.Contains("FIXME"));
        if (todoCount > 0)
            issues.Add($"待办事项：{todoCount} 个 TODO/FIXME");

        var qualityScore = Math.Max(0, 100 - issues.Count * 10);

        var sb = new StringBuilder();
        sb.AppendLine($"文件：{filePath}");
        sb.AppendLine($"质量评分：{qualityScore}/100");
        sb.AppendLine($"代码行数：{lines.Length}");
        sb.AppendLine($"空行：{emptyLines}");
        sb.AppendLine($"长行：{longLines.Count}");

        if (issues.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("问题：");
            foreach (var issue in issues)
                sb.AppendLine($"  - {issue}");
        }

        return new ToolResult { Name = Name, Success = true, Content = sb.ToString() };
    }

    private async Task<ToolResult> AnalyzeComplexityAsync(string filePath, CancellationToken ct)
    {
        if (!File.Exists(filePath)) return Fail($"文件不存在：{filePath}");

        var content = await File.ReadAllTextAsync(filePath, ct);
        var lines = content.Split('\n');

        var metrics = new Dictionary<string, int>
        {
            ["总行数"] = lines.Length,
            ["代码行数"] = lines.Count(l => !string.IsNullOrWhiteSpace(l) && !l.TrimStart().StartsWith("//")),
            ["注释行数"] = lines.Count(l => l.TrimStart().StartsWith("//") || l.TrimStart().StartsWith("/*")),
            ["空行数"] = lines.Count(string.IsNullOrWhiteSpace),
            ["平均行长度"] = lines.Length > 0 ? (int)lines.Average(l => l.Length) : 0,
            ["最大嵌套深度"] = CalculateMaxNesting(lines),
            ["方法数量"] = CountMethods(lines)
        };

        var sb = new StringBuilder();
        sb.AppendLine($"文件：{filePath}");
        sb.AppendLine();
        foreach (var metric in metrics)
            sb.AppendLine($"{metric.Key}：{metric.Value}");

        return new ToolResult { Name = Name, Success = true, Content = sb.ToString() };
    }

    private async Task<ToolResult> AnalyzeDependenciesAsync(string filePath, CancellationToken ct)
    {
        if (!File.Exists(filePath)) return Fail($"文件不存在：{filePath}");

        var content = await File.ReadAllTextAsync(filePath, ct);
        var dependencies = new List<string>();

        // C# using 语句
        var usingMatches = Regex.Matches(content, @"using\s+([\w.]+);");
        foreach (Match m in usingMatches)
        {
            var ns = m.Groups[1].Value;
            if (!ns.StartsWith("System"))
                dependencies.Add(ns);
        }

        // JavaScript/TypeScript import
        var importMatches = Regex.Matches(content, @"import\s+.*?from\s+['""](.+?)['""]");
        foreach (Match m in importMatches)
        {
            dependencies.Add(m.Groups[1].Value);
        }

        var sb = new StringBuilder();
        sb.AppendLine($"文件：{filePath}");
        sb.AppendLine($"依赖数量：{dependencies.Count}");
        sb.AppendLine();
        foreach (var dep in dependencies.Distinct())
            sb.AppendLine($"  - {dep}");

        return new ToolResult { Name = Name, Success = true, Content = sb.ToString() };
    }

    private async Task<ToolResult> AnalyzeStatsAsync(string filePath, CancellationToken ct)
    {
        if (!File.Exists(filePath)) return Fail($"文件不存在：{filePath}");

        var content = await File.ReadAllTextAsync(filePath, ct);
        var lines = content.Split('\n');
        var ext = Path.GetExtension(filePath).ToLower();

        var stats = new Dictionary<string, object>
        {
            ["文件名"] = Path.GetFileName(filePath),
            ["扩展名"] = ext,
            ["总行数"] = lines.Length,
            ["代码行数"] = lines.Count(l => !string.IsNullOrWhiteSpace(l) && !l.TrimStart().StartsWith("//")),
            ["注释行数"] = lines.Count(l => l.TrimStart().StartsWith("//")),
            ["空行数"] = lines.Count(string.IsNullOrWhiteSpace),
            ["文件大小"] = $"{new FileInfo(filePath).Length / 1024.0:F1} KB"
        };

        var sb = new StringBuilder();
        sb.AppendLine($"文件统计：{filePath}");
        sb.AppendLine();
        foreach (var stat in stats)
            sb.AppendLine($"{stat.Key}：{stat.Value}");

        return new ToolResult { Name = Name, Success = true, Content = sb.ToString() };
    }

    private async Task<ToolResult> AnalyzeStructureAsync(string directory, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
            return Fail($"目录不存在：{directory}");

        var files = Directory.GetFiles(directory, "*.*", SearchOption.AllDirectories);
        var structure = new Dictionary<string, int>();

        foreach (var file in files)
        {
            ct.ThrowIfCancellationRequested();
            var ext = Path.GetExtension(file).ToLower();
            if (string.IsNullOrEmpty(ext)) ext = "(无扩展名)";
            structure[ext] = structure.GetValueOrDefault(ext) + 1;
        }

        var sb = new StringBuilder();
        sb.AppendLine($"目录结构分析：{directory}");
        sb.AppendLine($"总文件数：{files.Length}");
        sb.AppendLine();
        sb.AppendLine("文件类型分布：");
        foreach (var item in structure.OrderByDescending(x => x.Value))
            sb.AppendLine($"  {item.Key}：{item.Value} 个文件");

        return new ToolResult { Name = Name, Success = true, Content = sb.ToString() };
    }

    private static int CheckDuplicateLines(string[] lines)
    {
        var lineSet = new HashSet<string>();
        var duplicates = 0;

        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (trimmed.Length < 10) continue; // 忽略短行

            if (!lineSet.Add(trimmed))
                duplicates++;
        }

        return duplicates;
    }

    private static int CalculateMaxNesting(string[] lines)
    {
        int maxDepth = 0;
        int currentDepth = 0;

        foreach (var line in lines)
        {
            foreach (char c in line)
            {
                if (c == '{') currentDepth++;
                if (c == '}') currentDepth--;
                maxDepth = Math.Max(maxDepth, currentDepth);
            }
        }

        return maxDepth;
    }

    private static int CountMethods(string[] lines)
    {
        int count = 0;
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (trimmed.Contains("void ") || trimmed.Contains("Task ") ||
                trimmed.Contains("string ") || trimmed.Contains("int ") ||
                trimmed.Contains("bool ") || trimmed.Contains("async "))
            {
                if (trimmed.Contains("(") && trimmed.Contains(")"))
                    count++;
            }
        }
        return count;
    }

    private static ToolResult Fail(string error) => new() { Name = "code_analysis", Success = false, Error = error };
}
