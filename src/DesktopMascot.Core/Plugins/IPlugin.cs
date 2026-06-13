namespace DesktopMascot.Core.Plugins;

/// <summary>
/// 插件状态
/// </summary>
public enum PluginState
{
    /// <summary>未加载</summary>
    Unloaded,
    /// <summary>已加载</summary>
    Loaded,
    /// <summary>已启用</summary>
    Enabled,
    /// <summary>已禁用</summary>
    Disabled,
    /// <summary>错误</summary>
    Error
}

/// <summary>
/// 插件元数据
/// </summary>
public class PluginMetadata
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Version { get; set; } = "1.0.0";
    public string Author { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string[] Dependencies { get; set; } = Array.Empty<string>();
    public Dictionary<string, string> Settings { get; set; } = new();
}

/// <summary>
/// 插件上下文
/// </summary>
public class PluginContext
{
    public string PluginDirectory { get; set; } = string.Empty;
    public string DataDirectory { get; set; } = string.Empty;
    public IServiceProvider? Services { get; set; }
}

/// <summary>
/// 插件接口
/// </summary>
public interface IPlugin : IDisposable
{
    /// <summary>插件元数据</summary>
    PluginMetadata Metadata { get; }
    
    /// <summary>当前状态</summary>
    PluginState State { get; }
    
    /// <summary>初始化插件</summary>
    Task InitializeAsync(PluginContext context, CancellationToken ct = default);
    
    /// <summary>启用插件</summary>
    Task EnableAsync(CancellationToken ct = default);
    
    /// <summary>禁用插件</summary>
    Task DisableAsync(CancellationToken ct = default);
    
    /// <summary>卸载插件</summary>
    Task UnloadAsync(CancellationToken ct = default);
}

/// <summary>
/// 可提供工具的插件接口
/// </summary>
public interface IToolProvider
{
    /// <summary>获取插件提供的工具</summary>
    IEnumerable<object> GetTools();
}
