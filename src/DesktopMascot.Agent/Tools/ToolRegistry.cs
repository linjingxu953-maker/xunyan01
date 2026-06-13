using DesktopMascot.Agent.Context;
using DesktopMascot.Agent.Models;

namespace DesktopMascot.Agent.Tools;

/// <summary>
/// 工具注册表
/// </summary>
public class ToolRegistry
{
    private readonly Dictionary<string, ITool> _tools = new();
    private IContextProvider? _contextProvider;

    /// <summary>
    /// 注册工具
    /// </summary>
    public void Register(ITool tool)
    {
        _tools[tool.Name] = tool;
    }

    /// <summary>
    /// 设置上下文提供者
    /// </summary>
    public void SetContextProvider(IContextProvider contextProvider)
    {
        _contextProvider = contextProvider;
    }

    /// <summary>
    /// 获取上下文提供者
    /// </summary>
    public IContextProvider? GetContextProvider()
    {
        return _contextProvider;
    }

    /// <summary>
    /// 获取工具
    /// </summary>
    public ITool? GetTool(string name)
    {
        return _tools.TryGetValue(name, out var tool) ? tool : null;
    }

    /// <summary>
    /// 获取所有工具定义
    /// </summary>
    public IEnumerable<ToolDefinition> GetToolDefinitions()
    {
        return _tools.Values.Select(t => new ToolDefinition
        {
            Name = t.Name,
            Description = t.Description,
            Parameters = t.ParametersSchema
        });
    }

    /// <summary>
    /// 执行工具调用
    /// </summary>
    public async Task<ToolResult> ExecuteToolAsync(ToolCall call, CancellationToken ct = default)
    {
        var tool = GetTool(call.Name);
        if (tool == null)
        {
            return new ToolResult
            {
                ToolCallId = call.Id,
                Name = call.Name,
                Success = false,
                Error = $"工具不存在: {call.Name}"
            };
        }

        return await tool.ExecuteAsync(call.Arguments, ct);
    }

    /// <summary>
    /// 获取已注册工具数量
    /// </summary>
    public int Count => _tools.Count;
}
