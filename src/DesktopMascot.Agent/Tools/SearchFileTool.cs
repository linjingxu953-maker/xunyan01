using System.Text;
using System.Text.Json;
using DesktopMascot.Agent.Context;
using DesktopMascot.Agent.Models;

namespace DesktopMascot.Agent.Tools;

/// <summary>
/// 文件搜索工具 - 在目录中搜索文件和内容
/// </summary>
public class SearchFileTool : ITool
{
    private readonly IContextProvider _contextProvider;

    public SearchFileTool(IContextProvider contextProvider)
    {
        _contextProvider = contextProvider;
    }

    public string Name => "search_file";
    public string Description => "在目录中搜索文件。支持按文件名、内容、扩展名搜索。";

    public string ParametersSchema => """
    {
        "type": "object",
        "properties": {
            "path": {
                "type": "string",
                "description": "搜索目录路径"
            },
            "query": {
                "type": "string",
                "description": "搜索关键词（文件名或文件内容）"
            },
            "mode": {
                "type": "string",
                "enum": ["filename", "content", "extension"],
                "description": "搜索模式"
            },
            "recursive": {
                "type": "boolean",
                "description": "是否递归搜索子目录（默认 true）"
            },
            "max_results": {
                "type": "integer",
                "description": "最大结果数（默认 20）"
            }
        },
        "required": ["path", "query"]
    }
    """;

    public async Task<ToolResult> ExecuteAsync(string arguments, CancellationToken ct = default)
    {
        try
        {
            var doc = JsonDocument.Parse(arguments);
            var root = doc.RootElement;

            if (!root.TryGetProperty("path", out var pathElement))
                return Fail("缺少 path 参数");

            if (!root.TryGetProperty("query", out var queryElement))
                return Fail("缺少 query 参数");

            var searchPath = pathElement.GetString() ?? "";
            var query = queryElement.GetString() ?? "";
            var mode = root.TryGetProperty("mode", out var modeElement) ? modeElement.GetString() ?? "filename" : "filename";
            var recursive = !root.TryGetProperty("recursive", out var recElement) || recElement.GetBoolean();
            var maxResults = root.TryGetProperty("max_results", out var maxElement) ? maxElement.GetInt32() : 20;

            if (string.IsNullOrWhiteSpace(searchPath))
                return Fail("搜索路径不能为空");

            if (!Directory.Exists(searchPath))
                return Fail($"目录不存在：{searchPath}");

            if (string.IsNullOrWhiteSpace(query))
                return Fail("搜索关键词不能为空");

            var results = new List<SearchResult>();
            var searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;

            switch (mode)
            {
                case "filename":
                    SearchByFilename(searchPath, query, searchOption, results, maxResults, ct);
                    break;
                case "content":
                    await SearchByContentAsync(searchPath, query, searchOption, results, maxResults, ct);
                    break;
                case "extension":
                    SearchByExtension(searchPath, query, searchOption, results, maxResults);
                    break;
                default:
                    return Fail($"不支持的搜索模式：{mode}");
            }

            if (results.Count == 0)
                return new ToolResult
                {
                    Name = Name,
                    Success = true,
                    Content = $"未找到匹配 \"{query}\" 的{(mode == "filename" ? "文件" : mode == "content" ? "内容" : "扩展名")}"
                };

            var sb = new StringBuilder();
            sb.AppendLine($"搜索结果（{results.Count} 条）：");
            sb.AppendLine();

            foreach (var r in results)
            {
                sb.AppendLine($"文件：{r.FilePath}");
                if (!string.IsNullOrEmpty(r.MatchedLine))
                    sb.AppendLine($"  匹配行 {r.LineNumber}：{r.MatchedLine.Trim()}");
                sb.AppendLine();
            }

            return new ToolResult
            {
                Name = Name,
                Success = true,
                Content = sb.ToString()
            };
        }
        catch (Exception ex)
        {
            return Fail($"搜索失败：{ex.Message}");
        }
    }

    private static void SearchByFilename(string path, string query, SearchOption option, List<SearchResult> results, int maxResults, CancellationToken ct)
    {
        if (results.Count >= maxResults) return;

        try
        {
            foreach (var file in Directory.EnumerateFiles(path, "*.*", option))
            {
                ct.ThrowIfCancellationRequested();
                if (results.Count >= maxResults) break;

                var fileName = Path.GetFileName(file);
                if (fileName.Contains(query, StringComparison.OrdinalIgnoreCase))
                {
                    results.Add(new SearchResult { FilePath = file });
                }
            }
        }
        catch { }
    }

    private static async Task SearchByContentAsync(string path, string query, SearchOption option, List<SearchResult> results, int maxResults, CancellationToken ct)
    {
        if (results.Count >= maxResults) return;

        try
        {
            var textExtensions = new HashSet<string> { ".txt", ".cs", ".js", ".ts", ".py", ".java", ".cpp", ".h", ".json", ".xml", ".md", ".html", ".css", ".yaml", ".yml", ".csproj", ".sln" };

            foreach (var file in Directory.EnumerateFiles(path, "*.*", option))
            {
                ct.ThrowIfCancellationRequested();
                if (results.Count >= maxResults) break;

                var ext = Path.GetExtension(file).ToLower();
                if (!textExtensions.Contains(ext)) continue;

                try
                {
                    var lines = await File.ReadAllLinesAsync(file, ct);
                    for (int i = 0; i < lines.Length; i++)
                    {
                        if (lines[i].Contains(query, StringComparison.OrdinalIgnoreCase))
                        {
                            results.Add(new SearchResult
                            {
                                FilePath = file,
                                LineNumber = i + 1,
                                MatchedLine = lines[i]
                            });
                            break;
                        }
                    }
                }
                catch { }
            }
        }
        catch { }
    }

    private static void SearchByExtension(string path, string extension, SearchOption option, List<SearchResult> results, int maxResults)
    {
        var pattern = extension.StartsWith(".") ? $"*{extension}" : $"*.{extension}";

        try
        {
            foreach (var file in Directory.EnumerateFiles(path, pattern, option))
            {
                if (results.Count >= maxResults) break;
                results.Add(new SearchResult { FilePath = file });
            }
        }
        catch { }
    }

    private class SearchResult
    {
        public string FilePath { get; set; } = string.Empty;
        public int LineNumber { get; set; }
        public string? MatchedLine { get; set; }
    }

    private static ToolResult Fail(string error) => new() { Name = "search_file", Success = false, Error = error };
}
