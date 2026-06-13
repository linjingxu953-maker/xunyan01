using DesktopMascot.Core.Plugins;

namespace DesktopMascot.Core.Tests;

public class PluginRegistryTests : IDisposable
{
    private readonly PluginRegistry _registry;
    private readonly PluginLoader _loader;
    private readonly string _testDir;

    public PluginRegistryTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"plugins_test_{Guid.NewGuid():N}");
        _loader = new PluginLoader(_testDir);
        _registry = new PluginRegistry(_loader);
    }

    void IDisposable.Dispose()
    {
        if (Directory.Exists(_testDir))
            Directory.Delete(_testDir, true);
    }

    [Fact]
    public void RegisterPlugin_ShouldAddToRegistry()
    {
        var plugin = new QuotesPlugin();

        _registry.RegisterPlugin(plugin);

        Assert.Single(_registry.Plugins);
        Assert.Contains("builtin.quotes", _registry.Plugins.Keys);
    }

    [Fact]
    public void RegisterPlugin_Duplicate_ShouldIgnore()
    {
        var plugin1 = new QuotesPlugin();
        var plugin2 = new QuotesPlugin();

        _registry.RegisterPlugin(plugin1);
        _registry.RegisterPlugin(plugin2);

        Assert.Single(_registry.Plugins);
    }

    [Fact]
    public void GetPlugin_ShouldReturnPlugin()
    {
        var plugin = new QuotesPlugin();
        _registry.RegisterPlugin(plugin);

        var result = _registry.GetPlugin("builtin.quotes");

        Assert.NotNull(result);
        Assert.Equal("builtin.quotes", result!.Metadata.Id);
    }

    [Fact]
    public void GetPlugin_NonExisting_ShouldReturnNull()
    {
        var result = _registry.GetPlugin("non.existing");

        Assert.Null(result);
    }

    [Fact]
    public async Task EnablePlugin_ShouldChangeState()
    {
        var plugin = new QuotesPlugin();
        _registry.RegisterPlugin(plugin);

        await _registry.EnablePluginAsync("builtin.quotes");

        Assert.Equal(PluginState.Enabled, plugin.State);
    }

    [Fact]
    public async Task DisablePlugin_ShouldChangeState()
    {
        var plugin = new QuotesPlugin();
        _registry.RegisterPlugin(plugin);
        await _registry.EnablePluginAsync("builtin.quotes");

        await _registry.DisablePluginAsync("builtin.quotes");

        Assert.Equal(PluginState.Disabled, plugin.State);
    }

    [Fact]
    public async Task UnloadPlugin_ShouldRemoveFromRegistry()
    {
        var plugin = new QuotesPlugin();
        _registry.RegisterPlugin(plugin);

        await _registry.UnloadPluginAsync("builtin.quotes");

        Assert.Empty(_registry.Plugins);
    }

    [Fact]
    public async Task GetToolProviders_ShouldReturnEnabledPlugins()
    {
        var quotesPlugin = new QuotesPlugin();
        var weatherPlugin = new WeatherPlugin();
        _registry.RegisterPlugin(quotesPlugin);
        _registry.RegisterPlugin(weatherPlugin);
        await quotesPlugin.EnableAsync();

        var providers = _registry.GetToolProviders().ToList();

        Assert.Single(providers);
        Assert.IsType<QuotesPlugin>(providers[0]);
    }

    [Fact]
    public async Task GetAllTools_ShouldReturnAllTools()
    {
        var quotesPlugin = new QuotesPlugin();
        _registry.RegisterPlugin(quotesPlugin);
        await quotesPlugin.EnableAsync();

        var tools = _registry.GetAllTools().ToList();

        Assert.Single(tools);
    }
}

public class BuiltInPluginsTests
{
    [Fact]
    public void QuotesPlugin_Metadata_ShouldBeCorrect()
    {
        var plugin = new QuotesPlugin();

        Assert.Equal("builtin.quotes", plugin.Metadata.Id);
        Assert.Equal("随机名言插件", plugin.Metadata.Name);
    }

    [Fact]
    public void QuotesPlugin_ShouldProvideTools()
    {
        var plugin = new QuotesPlugin();

        var tools = plugin.GetTools().ToList();

        Assert.Single(tools);
    }

    [Fact]
    public async Task QuotesPlugin_Tool_ShouldReturnQuote()
    {
        var plugin = new QuotesPlugin();
        var tool = plugin.GetTools().First() as IPluginTool;

        var result = await tool!.ExecuteAsync("{}");

        Assert.True(result.Success);
        Assert.NotEmpty(result.Content);
    }

    [Fact]
    public void WeatherPlugin_Metadata_ShouldBeCorrect()
    {
        var plugin = new WeatherPlugin();

        Assert.Equal("builtin.weather", plugin.Metadata.Id);
        Assert.Equal("天气查询插件", plugin.Metadata.Name);
    }

    [Fact]
    public async Task WeatherPlugin_Tool_ShouldReturnWeather()
    {
        var plugin = new WeatherPlugin();
        var tool = plugin.GetTools().First() as IPluginTool;

        var result = await tool!.ExecuteAsync("""{"city": "北京"}""");

        Assert.True(result.Success);
        Assert.Contains("北京", result.Content);
    }
}

public class PluginBaseTests
{
    [Fact]
    public async Task Initialize_ShouldSetState()
    {
        var plugin = new QuotesPlugin();
        var context = new PluginContext { PluginDirectory = "/test" };

        await plugin.InitializeAsync(context);

        Assert.Equal(PluginState.Loaded, plugin.State);
    }

    [Fact]
    public async Task Enable_ShouldSetState()
    {
        var plugin = new QuotesPlugin();
        await plugin.InitializeAsync(new PluginContext());

        await plugin.EnableAsync();

        Assert.Equal(PluginState.Enabled, plugin.State);
    }

    [Fact]
    public async Task Disable_ShouldSetState()
    {
        var plugin = new QuotesPlugin();
        await plugin.InitializeAsync(new PluginContext());
        await plugin.EnableAsync();

        await plugin.DisableAsync();

        Assert.Equal(PluginState.Disabled, plugin.State);
    }
}
