namespace DesktopMascot.Core.Plugins;

/// <summary>
/// 插件注册表
/// </summary>
public class PluginRegistry
{
    private readonly Dictionary<string, IPlugin> _plugins = new();
    private readonly PluginLoader _loader;
    private FileSystemWatcher? _watcher;

    public event Action<IPlugin>? PluginLoaded;
    public event Action<IPlugin>? PluginUnloaded;
    public event Action<IPlugin>? PluginStateChanged;
    public event Action<string>? PluginHotReloaded;

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

    /// <summary>
    /// 启用热加载监控
    /// </summary>
    public void EnableHotReload(string? pluginsDirectory = null)
    {
        var dir = pluginsDirectory ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "plugins");
        
        _watcher = new FileSystemWatcher(dir, "*.dll")
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.CreationTime,
            EnableRaisingEvents = true
        };

        _watcher.Created += OnPluginFileChanged;
        _watcher.Changed += OnPluginFileChanged;
    }

    /// <summary>
    /// 禁用热加载监控
    /// </summary>
    public void DisableHotReload()
    {
        _watcher?.Dispose();
        _watcher = null;
    }

    private async void OnPluginFileChanged(object sender, FileSystemEventArgs e)
    {
        try
        {
            await Task.Delay(1000); // 等待文件写入完成
            
            var pluginId = Path.GetFileNameWithoutExtension(e.Name);
            
            if (e.ChangeType == WatcherChangeTypes.Created || e.ChangeType == WatcherChangeTypes.Changed)
            {
                // 尝试重新加载插件
                if (_plugins.ContainsKey(pluginId))
                {
                    await UnloadPluginAsync(pluginId);
                }
                
                var plugin = await _loader.LoadPluginFromAssemblyAsync(e.FullPath);
                if (plugin != null)
                {
                    RegisterPlugin(plugin);
                    PluginHotReloaded?.Invoke(pluginId);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"热加载失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 检查插件依赖
    /// </summary>
    public List<string> CheckDependencies(string pluginId)
    {
        var missing = new List<string>();
        var plugin = GetPlugin(pluginId);
        
        if (plugin == null)
            return new List<string> { pluginId };

        foreach (var dep in plugin.Metadata.Dependencies)
        {
            if (!_plugins.ContainsKey(dep))
            {
                missing.Add(dep);
            }
        }

        return missing;
    }

    /// <summary>
    /// 获取插件配置
    /// </summary>
    public Dictionary<string, string> GetPluginSettings(string pluginId)
    {
        var plugin = GetPlugin(pluginId);
        return plugin?.Metadata.Settings ?? new Dictionary<string, string>();
    }

    /// <summary>
    /// 更新插件配置
    /// </summary>
    public void UpdatePluginSettings(string pluginId, Dictionary<string, string> settings)
    {
        var plugin = GetPlugin(pluginId);
        if (plugin != null)
        {
            foreach (var kvp in settings)
            {
                plugin.Metadata.Settings[kvp.Key] = kvp.Value;
            }
        }
    }
}
