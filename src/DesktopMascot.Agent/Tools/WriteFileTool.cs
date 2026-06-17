using DesktopMascot.Core.Tools;
using System.Text.Json;
using DesktopMascot.Agent.Context;
using DesktopMascot.Agent.Models;

namespace DesktopMascot.Agent.Tools;

/// <summary>
/// 文件写入工具 - 生成或覆盖文件（需权限确认）
/// </summary>
public class WriteFileTool : ITool
{
    private readonly IContextProvider _contextProvider;

    public WriteFileTool(IContextProvider contextProvider)
    {
        _contextProvider = contextProvider;
    }

    public string Name => "write_file";
    public string Description => "写入文件到指定路径。如果文件已存在会覆盖。需要用户确认权限。";
    public bool RequiresConfirmation => true;
    public string ConfirmationMessage => "AI 想要写入文件，是否允许？";
    public string ParametersSchema => """
    {
        "type": "object",
        "properties": {
            "path": {
                "type": "string",
                "description": "文件路径"
            },
            "content": {
                "type": "string",
                "description": "文件内容"
            },
            "encoding": {
                "type": "string",
                "description": "编码格式（默认 utf-8）"
            }
        },
        "required": ["path", "content"]
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

            if (!root.TryGetProperty("content", out var contentElement))
            {
                return new ToolResult
                {
                    Name = Name,
                    Success = false,
                    Error = "缺少 content 参数"
                };
            }

            var filePath = pathElement.GetString() ?? "";
            var content = contentElement.GetString() ?? "";

            if (string.IsNullOrWhiteSpace(filePath))
            {
                return new ToolResult
                {
                    Name = Name,
                    Success = false,
                    Error = "文件路径不能为空"
                };
            }

            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var encoding = System.Text.Encoding.UTF8;
            if (root.TryGetProperty("encoding", out var encElement))
            {
                var encName = encElement.GetString()?.ToLower();
                if (encName == "ascii") encoding = System.Text.Encoding.ASCII;
                else if (encName == "unicode") encoding = System.Text.Encoding.Unicode;
            }

            var fileExists = File.Exists(filePath);
            await File.WriteAllTextAsync(filePath, content, encoding, ct);

            var fileInfo = new FileInfo(filePath);
            var size = FormatFileSize(fileInfo.Length);

            return new ToolResult
            {
                Name = Name,
                Success = true,
                Content = $"文件已{(fileExists ? "覆盖" : "创建")}：{filePath}\n大小：{size}"
            };
        }
        catch (Exception ex)
        {
            return new ToolResult
            {
                Name = Name,
                Success = false,
                Error = $"写入文件失败：{ex.Message}"
            };
        }
    }

    private static string FormatFileSize(long bytes)
    {
        return bytes switch
        {
            < 1024 => $"{bytes}B",
            < 1024 * 1024 => $"{bytes / 1024.0:F1}KB",
            _ => $"{bytes / (1024.0 * 1024):F1}MB"
        };
    }
}
