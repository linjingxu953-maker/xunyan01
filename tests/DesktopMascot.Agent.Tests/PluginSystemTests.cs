using DesktopMascot.Core.Plugins;

namespace DesktopMascot.Agent.Tests;

public class PluginSystemEnhancedTests
{
    [Fact]
    public void PluginRegistry_ShouldTrackPlugins()
    {
        var loader = new PluginLoader();
        var registry = new PluginRegistry(loader);

        Assert.Empty(registry.Plugins);
    }

    [Fact]
    public void PluginRegistry_RegisterPlugin_ShouldAddToRegistry()
    {
        var loader = new PluginLoader();
        var registry = new PluginRegistry(loader);

        var plugin = new MockPlugin { Metadata = new PluginMetadata { Id = "test-plugin", Name = "Test" } };
        registry.RegisterPlugin(plugin);

        Assert.Single(registry.Plugins);
        Assert.Contains("test-plugin", registry.Plugins.Keys);
    }

    [Fact]
    public void PluginRegistry_DuplicatePlugin_ShouldNotAdd()
    {
        var loader = new PluginLoader();
        var registry = new PluginRegistry(loader);

        var plugin1 = new MockPlugin { Metadata = new PluginMetadata { Id = "test-plugin", Name = "Test1" } };
        var plugin2 = new MockPlugin { Metadata = new PluginMetadata { Id = "test-plugin", Name = "Test2" } };

        registry.RegisterPlugin(plugin1);
        registry.RegisterPlugin(plugin2);

        Assert.Single(registry.Plugins);
    }

    [Fact]
    public void PluginRegistry_CheckDependencies_ShouldReturnMissing()
    {
        var loader = new PluginLoader();
        var registry = new PluginRegistry(loader);

        var plugin = new MockPlugin
        {
            Metadata = new PluginMetadata
            {
                Id = "test-plugin",
                Dependencies = new[] { "dep1", "dep2" }
            }
        };
        registry.RegisterPlugin(plugin);

        var missing = registry.CheckDependencies("test-plugin");

        Assert.Equal(2, missing.Count);
        Assert.Contains("dep1", missing);
        Assert.Contains("dep2", missing);
    }

    [Fact]
    public void PluginRegistry_CheckDependencies_WithExistingDeps_ShouldReturnPartial()
    {
        var loader = new PluginLoader();
        var registry = new PluginRegistry(loader);

        var dep1 = new MockPlugin { Metadata = new PluginMetadata { Id = "dep1" } };
        var plugin = new MockPlugin
        {
            Metadata = new PluginMetadata
            {
                Id = "test-plugin",
                Dependencies = new[] { "dep1", "dep2" }
            }
        };

        registry.RegisterPlugin(dep1);
        registry.RegisterPlugin(plugin);

        var missing = registry.CheckDependencies("test-plugin");

        Assert.Single(missing);
        Assert.Contains("dep2", missing);
    }

    [Fact]
    public void PluginRegistry_GetPluginSettings_ShouldReturnSettings()
    {
        var loader = new PluginLoader();
        var registry = new PluginRegistry(loader);

        var plugin = new MockPlugin
        {
            Metadata = new PluginMetadata
            {
                Id = "test-plugin",
                Settings = new Dictionary<string, string> { ["key1"] = "value1", ["key2"] = "value2" }
            }
        };
        registry.RegisterPlugin(plugin);

        var settings = registry.GetPluginSettings("test-plugin");

        Assert.Equal(2, settings.Count);
        Assert.Equal("value1", settings["key1"]);
    }

    [Fact]
    public void PluginRegistry_UpdatePluginSettings_ShouldModifySettings()
    {
        var loader = new PluginLoader();
        var registry = new PluginRegistry(loader);

        var plugin = new MockPlugin
        {
            Metadata = new PluginMetadata
            {
                Id = "test-plugin",
                Settings = new Dictionary<string, string> { ["key1"] = "old" }
            }
        };
        registry.RegisterPlugin(plugin);

        registry.UpdatePluginSettings("test-plugin", new Dictionary<string, string> { ["key1"] = "new" });

        var settings = registry.GetPluginSettings("test-plugin");
        Assert.Equal("new", settings["key1"]);
    }
}

/// <summary>
/// 测试用 Mock 插件
/// </summary>
internal class MockPlugin : IPlugin
{
    public PluginMetadata Metadata { get; set; } = new();
    public PluginState State { get; set; } = PluginState.Unloaded;

    public Task InitializeAsync(PluginContext context, CancellationToken ct = default)
    {
        State = PluginState.Loaded;
        return Task.CompletedTask;
    }

    public Task EnableAsync(CancellationToken ct = default)
    {
        State = PluginState.Enabled;
        return Task.CompletedTask;
    }

    public Task DisableAsync(CancellationToken ct = default)
    {
        State = PluginState.Disabled;
        return Task.CompletedTask;
    }

    public Task UnloadAsync(CancellationToken ct = default)
    {
        State = PluginState.Unloaded;
        return Task.CompletedTask;
    }

    public void Dispose() { }
}
