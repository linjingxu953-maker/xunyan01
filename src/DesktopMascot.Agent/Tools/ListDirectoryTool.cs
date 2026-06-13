using System.Text;
using System.Text.Json;
using DesktopMascot.Agent.Context;
using DesktopMascot.Agent.Models;

namespace DesktopMascot.Agent.Tools;

/// <summary>
/// 目录列表工具 - 遍历指定目录的文件结构
/// </summary>
public class ListDirectoryTool : ITool
{
    private readonly IContextProvider _contextProvider;

    public ListDirectoryTool(IContextProvider contextProvider)
    {
        _contextProvider = contextProvider;
    }

    public string Name => "list_directory";
    public string Description => "列出指定目录的文件和子目录结构。支持递归遍历和文件类型过滤。";
    public string ParametersSchema => """
    {
        "type": "object",
        "properties": {
            "path": {
                "type": "string",
                "description": "要遍历的目录路径"
            },
            "recursive": {
                "type": "boolean",
                "description": "是否递归遍历子目录（默认 false）"
            },
            "max_depth": {
                "type": "integer",
                "description": "最大递归深度（默认 2）"
            },
            "filter": {
                "type": "string",
                "description": "文件类型过滤（如 *.cs, *.json, *.*）"
            }
        },
        "required": ["path"]
    }
    """;

    public async Task<ToolResult> ExecuteAsync(string arguments, CancellationToken ct = default)
    {
        try
        {
            var doc = JsonDocument.Parse(arguments);
            var root = doc.RootElement;

            if (!root.TryGetProperty("path", out var pathElement))
            {
                return new ToolResult
                {
                    Name = Name,
                    Success = false,
                    Error = "缺少 path 参数"
                };
            }

            var path = pathElement.GetString() ?? "";
            if (string.IsNullOrWhiteSpace(path))
            {
                return new ToolResult
                {
                    Name = Name,
                    Success = false,
                    Error = "路径不能为空"
                };
            }

            if (!Directory.Exists(path))
            {
                return new ToolResult
                {
                    Name = Name,
                    Success = false,
                    Error = $"目录不存在: {path}"
                };
            }

            var recursive = root.TryGetProperty("recursive", out var rec) && rec.GetBoolean();
            var maxDepth = root.TryGetProperty("max_depth", out var depth) ? depth.GetInt32() : 2;
            var filter = root.TryGetProperty("filter", out var filterElement) ? filterElement.GetString() : null;

            var result = new System.Text.StringBuilder();
            result.AppendLine($"目录: {path}");
            result.AppendLine();

            await ListDirectoryAsync(result, path, 0, maxDepth, recursive, filter, ct);

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
                Error = $"遍历目录失败: {ex.Message}"
            };
        }
    }

    private static async Task ListDirectoryAsync(
        StringBuilder sb, string path, int currentDepth, int maxDepth,
        bool recursive, string? filter, CancellationToken ct)
    {
        if (currentDepth > maxDepth)
            return;

        ct.ThrowIfCancellationRequested();

        var indent = new string(' ', currentDepth * 2);

        try
        {
            var files = Directory.GetFiles(path);
            if (!string.IsNullOrEmpty(filter))
            {
                files = Directory.GetFiles(path, filter);
            }

            foreach (var file in files)
            {
                var fileName = Path.GetFileName(file);
                var fileInfo = new FileInfo(file);
                var size = FormatFileSize(fileInfo.Length);
                sb.AppendLine($"{indent}  {fileName} ({size})");
            }

            if (recursive)
            {
                var directories = Directory.GetDirectories(path);
                foreach (var dir in directories)
                {
                    var dirName = Path.GetFileName(dir);
                    sb.AppendLine($"{indent}  [{dirName}/]");
                    await ListDirectoryAsync(sb, dir, currentDepth + 1, maxDepth, true, filter, ct);
                }
            }
        }
        catch (UnauthorizedAccessException)
        {
            sb.AppendLine($"{indent}  [权限不足]");
        }
    }

    private static string FormatFileSize(long bytes)
    {
        return bytes switch
        {
            < 1024 => $"{bytes}B",
            < 1024 * 1024 => $"{bytes / 1024.0:F1}KB",
            < 1024 * 1024 * 1024 => $"{bytes / (1024.0 * 1024):F1}MB",
            _ => $"{bytes / (1024.0 * 1024 * 1024):F1}GB"
        };
    }
}
