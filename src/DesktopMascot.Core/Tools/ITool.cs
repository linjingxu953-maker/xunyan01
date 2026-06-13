namespace DesktopMascot.Core.Tools;

/// <summary>
/// 工具接口
/// </summary>
public interface ITool
{
    /// <summary>工具定义</summary>
    ToolDefinition Definition { get; }
    
    /// <summary>执行工具</summary>
    Task<ToolCallResponse> ExecuteAsync(ToolCallRequest request, CancellationToken ct = default);
    
    /// <summary>验证参数</summary>
    Task<bool> ValidateArgumentsAsync(string arguments, CancellationToken ct = default);
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
