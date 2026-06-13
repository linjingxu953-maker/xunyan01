namespace DesktopMascot.Core.Plugins;

/// <summary>
/// 插件基类
/// </summary>
public abstract class PluginBase : IPlugin
{
    private PluginState _state = PluginState.Unloaded;

    public abstract PluginMetadata Metadata { get; }
    
    public PluginState State => _state;

    protected PluginContext? Context { get; private set; }

    public virtual Task InitializeAsync(PluginContext context, CancellationToken ct = default)
    {
        Context = context;
        _state = PluginState.Loaded;
        return Task.CompletedTask;
    }

    public virtual Task EnableAsync(CancellationToken ct = default)
    {
        _state = PluginState.Enabled;
        return Task.CompletedTask;
    }

    public virtual Task DisableAsync(CancellationToken ct = default)
    {
        _state = PluginState.Disabled;
        return Task.CompletedTask;
    }

    public virtual Task UnloadAsync(CancellationToken ct = default)
    {
        _state = PluginState.Unloaded;
        return Task.CompletedTask;
    }

    public virtual void Dispose()
    {
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// 获取插件数据目录
    /// </summary>
    protected string GetDataDirectory()
    {
        if (Context?.DataDirectory == null)
            throw new InvalidOperationException("插件未初始化");
        
        Directory.CreateDirectory(Context.DataDirectory);
        return Context.DataDirectory;
    }

    /// <summary>
    /// 加载插件配置
    /// </summary>
    protected async Task<T?> LoadConfigAsync<T>(string fileName, CancellationToken ct = default) where T : class
    {
        var configPath = Path.Combine(GetDataDirectory(), fileName);
        
        if (!File.Exists(configPath))
            return null;
        
        var json = await File.ReadAllTextAsync(configPath, ct);
        return System.Text.Json.JsonSerializer.Deserialize<T>(json);
    }

    /// <summary>
    /// 保存插件配置
    /// </summary>
    protected async Task SaveConfigAsync<T>(string fileName, T config, CancellationToken ct = default)
    {
        var configPath = Path.Combine(GetDataDirectory(), fileName);
        var json = System.Text.Json.JsonSerializer.Serialize(config, new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true
        });
        await File.WriteAllTextAsync(configPath, json, ct);
    }
}
