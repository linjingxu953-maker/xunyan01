namespace DesktopMascot.Core.Plugins;

/// <summary>
/// 插件工具接口
/// </summary>
public interface IPluginTool
{
    /// <summary>工具名称</summary>
    string Name { get; }

    /// <summary>工具描述</summary>
    string Description { get; }

    /// <summary>参数 JSON Schema</summary>
    string ParametersSchema { get; }

    /// <summary>执行工具</summary>
    Task<PluginToolResult> ExecuteAsync(string arguments, CancellationToken ct = default);
}

/// <summary>
/// 工具结果
/// </summary>
public class PluginToolResult
{
    public string Name { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string? Error { get; set; }
}
