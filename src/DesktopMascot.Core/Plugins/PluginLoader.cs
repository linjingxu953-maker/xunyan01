using System.Reflection;

namespace DesktopMascot.Core.Plugins;

/// <summary>
/// 插件加载器
/// </summary>
public class PluginLoader
{
    private readonly string _pluginsDirectory;

    public PluginLoader(string? pluginsDirectory = null)
    {
        _pluginsDirectory = pluginsDirectory ?? Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "plugins");
        Directory.CreateDirectory(_pluginsDirectory);
    }

    /// <summary>
    /// 扫描并加载所有插件
    /// </summary>
    public async Task<List<IPlugin>> LoadAllPluginsAsync(CancellationToken ct = default)
    {
        var plugins = new List<IPlugin>();
        
        if (!Directory.Exists(_pluginsDirectory))
            return plugins;

        var dllFiles = Directory.GetFiles(_pluginsDirectory, "*.dll", SearchOption.AllDirectories);
        
        foreach (var dllPath in dllFiles)
        {
            try
            {
                var plugin = await LoadPluginFromAssemblyAsync(dllPath, ct);
                if (plugin != null)
                {
                    plugins.Add(plugin);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"加载插件失败 {dllPath}: {ex.Message}");
            }
        }

        return plugins;
    }

    /// <summary>
    /// 从程序集加载插件
    /// </summary>
    public async Task<IPlugin?> LoadPluginFromAssemblyAsync(string assemblyPath, CancellationToken ct = default)
    {
        var assembly = Assembly.LoadFrom(assemblyPath);
        
        // 查找实现 IPlugin 的类型
        var pluginType = assembly.GetTypes()
            .FirstOrDefault(t => 
                typeof(IPlugin).IsAssignableFrom(t) && 
                !t.IsAbstract && 
                !t.IsInterface);
        
        if (pluginType == null)
            return null;

        var plugin = (IPlugin)Activator.CreateInstance(pluginType)!;
        
        // 获取插件目录
        var pluginDir = Path.GetDirectoryName(assemblyPath) ?? "";
        var context = new PluginContext
        {
            PluginDirectory = pluginDir,
            DataDirectory = Path.Combine(pluginDir, "data")
        };
        
        await plugin.InitializeAsync(context, ct);
        return plugin;
    }

    /// <summary>
    /// 从 ZIP 包安装插件
    /// </summary>
    public async Task<string> InstallPluginAsync(string zipPath, CancellationToken ct = default)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"plugin_{Guid.NewGuid():N}");
        
        try
        {
            // 解压到临时目录
            System.IO.Compression.ZipFile.ExtractToDirectory(zipPath, tempDir);
            
            // 查找插件信息文件
            var infoPath = Path.Combine(tempDir, "plugin.json");
            if (!File.Exists(infoPath))
                throw new InvalidOperationException("无效的插件包：缺少 plugin.json");
            
            var json = await File.ReadAllTextAsync(infoPath, ct);
            var metadata = System.Text.Json.JsonSerializer.Deserialize<PluginMetadata>(json);
            
            if (metadata == null || string.IsNullOrEmpty(metadata.Id))
                throw new InvalidOperationException("无效的插件元数据");
            
            // 移动到插件目录
            var targetDir = Path.Combine(_pluginsDirectory, metadata.Id);
            if (Directory.Exists(targetDir))
                Directory.Delete(targetDir, true);
            
            Directory.Move(tempDir, targetDir);
            return targetDir;
        }
        catch
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
            throw;
        }
    }

    /// <summary>
    /// 卸载插件
    /// </summary>
    public async Task UninstallPluginAsync(string pluginId, CancellationToken ct = default)
    {
        var pluginDir = Path.Combine(_pluginsDirectory, pluginId);
        
        if (Directory.Exists(pluginDir))
        {
            Directory.Delete(pluginDir, true);
        }
        
        await Task.CompletedTask;
    }
}
