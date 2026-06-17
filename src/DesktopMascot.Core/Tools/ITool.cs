namespace DesktopMascot.Core.Tools;

using System.Text.Json;

/// <summary>
/// 统一工具接口 — Agent 和 Core 层共用。
/// Agent 工具直接实现 Name/Description/ParametersSchema + ExecuteAsync(string)；
/// Core 工具通过 ToolBase 基类实现。
/// </summary>
public interface ITool
{
    /// <summary>工具名称</summary>
    string Name { get; }

    /// <summary>工具描述（给 LLM 看）</summary>
    string Description { get; }

    /// <summary>参数 JSON Schema</summary>
    string ParametersSchema { get; }

    /// <summary>是否需要用户确认</summary>
    bool RequiresConfirmation => false;

    /// <summary>确认提示信息</summary>
    string ConfirmationMessage => "";

    /// <summary>完整的工具元数据定义（默认从 Name/Description/ParametersSchema 构建）</summary>
    ToolDefinition Definition => new ToolDefinition
    {
        Name = this.Name,
        Description = this.Description,
        ParametersSchema = this.ParametersSchema
    };

    /// <summary>执行工具（传入 JSON 参数字符串）</summary>
    Task<ToolResult> ExecuteAsync(string arguments, CancellationToken ct = default);

    /// <summary>验证参数是否合法（默认通过）</summary>
    Task<bool> ValidateArgumentsAsync(string arguments, CancellationToken ct = default) => Task.FromResult(true);
}

/// <summary>
/// 工具提供者接口
/// </summary>
public interface IToolProvider
{
    /// <summary>提供者名称</summary>
    string Name { get; }

    /// <summary>获取所有工具</summary>
    IEnumerable<ITool> GetTools();

    /// <summary>初始化</summary>
    Task InitializeAsync(CancellationToken ct = default);
}

/// <summary>
/// 工具注册表接口
/// </summary>
public interface IToolRegistry
{
    /// <summary>注册工具</summary>
    void Register(ITool tool);

    /// <summary>注册工具提供者</summary>
    Task RegisterProviderAsync(IToolProvider provider, CancellationToken ct = default);

    /// <summary>获取工具</summary>
    ITool? GetTool(string name);

    /// <summary>获取所有工具定义</summary>
    IEnumerable<ToolDefinition> GetAllDefinitions();

    /// <summary>按类别获取工具</summary>
    IEnumerable<ToolDefinition> GetByCategory(string category);

    /// <summary>按标签获取工具</summary>
    IEnumerable<ToolDefinition> GetByTag(string tag);

    /// <summary>搜索工具</summary>
    IEnumerable<ToolDefinition> Search(string query);

    /// <summary>执行工具</summary>
    Task<ToolCallResponse> ExecuteAsync(ToolCallRequest request, CancellationToken ct = default);

    /// <summary>获取调用日志</summary>
    Task<List<ToolCallLog>> GetCallLogsAsync(int limit = 100, CancellationToken ct = default);
}
