namespace DesktopMascot.Core.Plugins;

/// <summary>
/// 插件注册表
/// </summary>
public class PluginRegistry
{
    private readonly Dictionary<string, IPlugin> _plugins = new();
    private readonly PluginLoader _loader;

    public event Action<IPlugin>? PluginLoaded;
    public event Action<IPlugin>? PluginUnloaded;
    public event Action<IPlugin>? PluginStateChanged;

    public PluginRegistry(PluginLoader loader)
    {
        _loader = loader;
    }

    /// <summary>已注册插件</summary>
    public IReadOnlyDictionary<string, IPlugin> Plugins => _plugins;

    /// <summary>
    /// 加载并注册所有插件
    /// </summary>
    public async Task LoadAllPluginsAsync(CancellationToken ct = default)
    {
        var plugins = await _loader.LoadAllPluginsAsync(ct);
        
        foreach (var plugin in plugins)
        {
            RegisterPlugin(plugin);
        }
    }

    /// <summary>
    /// 注册插件
    /// </summary>
    public void RegisterPlugin(IPlugin plugin)
    {
        var id = plugin.Metadata.Id;
        
        if (_plugins.ContainsKey(id))
        {
            Console.WriteLine($"插件已存在: {id}");
            return;
        }
        
        _plugins[id] = plugin;
        PluginLoaded?.Invoke(plugin);
    }

    /// <summary>
    /// 获取插件
    /// </summary>
    public IPlugin? GetPlugin(string pluginId)
    {
        return _plugins.TryGetValue(pluginId, out var plugin) ? plugin : null;
    }

    /// <summary>
    /// 启用插件
    /// </summary>
    public async Task EnablePluginAsync(string pluginId, CancellationToken ct = default)
    {
        var plugin = GetPlugin(pluginId);
        if (plugin == null)
            throw new InvalidOperationException($"插件不存在: {pluginId}");
        
        await plugin.EnableAsync(ct);
        PluginStateChanged?.Invoke(plugin);
    }

    /// <summary>
    /// 禁用插件
    /// </summary>
    public async Task DisablePluginAsync(string pluginId, CancellationToken ct = default)
    {
        var plugin = GetPlugin(pluginId);
        if (plugin == null)
            throw new InvalidOperationException($"插件不存在: {pluginId}");
        
        await plugin.DisableAsync(ct);
        PluginStateChanged?.Invoke(plugin);
    }

    /// <summary>
    /// 卸载插件
    /// </summary>
    public async Task UnloadPluginAsync(string pluginId, CancellationToken ct = default)
    {
        var plugin = GetPlugin(pluginId);
        if (plugin == null)
            return;
        
        await plugin.UnloadAsync(ct);
        plugin.Dispose();
        
        _plugins.Remove(pluginId);
        PluginUnloaded?.Invoke(plugin);
        
        await _loader.UninstallPluginAsync(pluginId, ct);
    }

    /// <summary>
    /// 获取所有启用的工具提供者
    /// </summary>
    public IEnumerable<IToolProvider> GetToolProviders()
    {
        return _plugins.Values
            .Where(p => p.State == PluginState.Enabled)
            .OfType<IToolProvider>();
    }

    /// <summary>
    /// 获取所有插件的工具
    /// </summary>
    public IEnumerable<object> GetAllTools()
    {
        return GetToolProviders().SelectMany(p => p.GetTools());
    }
}
