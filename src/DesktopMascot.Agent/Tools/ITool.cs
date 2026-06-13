using DesktopMascot.Agent.Models;

namespace DesktopMascot.Agent.Tools;

/// <summary>
/// 工具接口
/// </summary>
public interface ITool
{
    /// <summary>
    /// 工具名称
    /// </summary>
    string Name { get; }

    /// <summary>
    /// 工具描述
    /// </summary>
    string Description { get; }

    /// <summary>
    /// 参数 JSON Schema
    /// </summary>
    string ParametersSchema { get; }

    /// <summary>
    /// 执行工具
    /// </summary>
    Task<ToolResult> ExecuteAsync(string arguments, CancellationToken ct = default);
}
