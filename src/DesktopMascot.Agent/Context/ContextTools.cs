using System.Text.Json;
using DesktopMascot.Agent.Models;
using DesktopMascot.Core.Tools;

namespace DesktopMascot.Agent.Context;

/// <summary>
/// 获取当前窗口信息工具
/// </summary>
public class GetActiveWindowTool : ITool
{
    private readonly IContextProvider _contextProvider;

    public GetActiveWindowTool(IContextProvider contextProvider)
    {
        _contextProvider = contextProvider;
    }

    public string Name => "get_active_window";
    public string Description => "获取当前活动窗口的标题和应用名";
    public string ParametersSchema => "{}";

    public async Task<ToolResult> ExecuteAsync(string arguments, CancellationToken ct = default)
    {
        var context = await _contextProvider.GetActiveWindowContextAsync(ct);
        
        return new ToolResult
        {
            Name = Name,
            Success = true,
            Content = $"窗口标题: {context.ActiveWindowTitle}\n应用: {context.ActiveApplication}"
        };
    }
}

/// <summary>
/// 读取文件内容工具
/// </summary>
public class ReadFileTool : ITool
{
    private readonly IContextProvider _contextProvider;

    public ReadFileTool(IContextProvider contextProvider)
    {
        _contextProvider = contextProvider;
    }

    public string Name => "read_file";
    public string Description => "读取指定路径的文件内容";
    public string ParametersSchema => """
    {
        "type": "object",
        "properties": {
            "path": {
                "type": "string",
                "description": "文件路径"
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
            var path = doc.RootElement.GetProperty("path").GetString() ?? "";
            
            if (string.IsNullOrEmpty(path))
            {
                return new ToolResult
                {
                    Name = Name,
                    Success = false,
                    Error = "文件路径不能为空"
                };
            }
            
            var content = await _contextProvider.ReadFileAsync(path, ct);
            
            if (content == null)
            {
                return new ToolResult
                {
                    Name = Name,
                    Success = false,
                    Error = $"文件不存在或无法读取: {path}"
                };
            }
            
            return new ToolResult
            {
                Name = Name,
                Success = true,
                Content = content
            };
        }
        catch (Exception ex)
        {
            return new ToolResult
            {
                Name = Name,
                Success = false,
                Error = $"读取文件失败: {ex.Message}"
            };
        }
    }
}
