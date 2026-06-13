using System.Text.Json;

namespace DesktopMascot.Core.Plugins;

/// <summary>
/// 内置插件：随机名言
/// </summary>
public class QuotesPlugin : PluginBase, IToolProvider
{
    private readonly List<string> _quotes = new()
    {
        "代码是写给人看的，顺便让机器执行。 - Harold Abelson",
        "先让它工作，再让它正确，最后让它快。 - Kent Beck",
        "过早优化是万恶之源。 - Donald Knuth",
        "简单是可靠的先决条件。 - Edsger Dijkstra",
        "任何足够先进的技术都与魔法无异。 - Arthur C. Clarke"
    };

    public override PluginMetadata Metadata => new()
    {
        Id = "builtin.quotes",
        Name = "随机名言插件",
        Version = "1.0.0",
        Author = "DesktopMascot",
        Description = "提供随机编程名言的内置插件"
    };

    public IEnumerable<object> GetTools()
    {
        return new object[] { new GetRandomQuoteTool(_quotes) };
    }
}

/// <summary>
/// 获取随机名言工具
/// </summary>
internal class GetRandomQuoteTool : IPluginTool
{
    private readonly List<string> _quotes;

    public GetRandomQuoteTool(List<string> quotes)
    {
        _quotes = quotes;
    }

    public string Name => "get_random_quote";
    public string Description => "获取一条随机编程名言";
    public string ParametersSchema => "{}";

    public Task<PluginToolResult> ExecuteAsync(string arguments, CancellationToken ct = default)
    {
        var random = new Random();
        var quote = _quotes[random.Next(_quotes.Count)];
        
        return Task.FromResult(new PluginToolResult
        {
            Name = Name,
            Success = true,
            Content = quote
        });
    }
}

/// <summary>
/// 内置插件：天气查询（模拟）
/// </summary>
public class WeatherPlugin : PluginBase, IToolProvider
{
    public override PluginMetadata Metadata => new()
    {
        Id = "builtin.weather",
        Name = "天气查询插件",
        Version = "1.0.0",
        Author = "DesktopMascot",
        Description = "提供天气查询功能的内置插件（模拟数据）"
    };

    public IEnumerable<object> GetTools()
    {
        return new object[] { new GetWeatherTool() };
    }
}

/// <summary>
/// 获取天气工具
/// </summary>
internal class GetWeatherTool : IPluginTool
{
    public string Name => "get_weather";
    public string Description => "查询指定城市的天气（模拟数据）";
    public string ParametersSchema => """
    {
        "type": "object",
        "properties": {
            "city": {
                "type": "string",
                "description": "城市名称"
            }
        },
        "required": ["city"]
    }
    """;

    public Task<PluginToolResult> ExecuteAsync(string arguments, CancellationToken ct = default)
    {
        try
        {
            var doc = JsonDocument.Parse(arguments);
            var city = doc.RootElement.GetProperty("city").GetString() ?? "北京";
            
            // 模拟天气数据
            var weather = new
            {
                City = city,
                Temperature = Random.Shared.Next(15, 35),
                Condition = Random.Shared.Next(0, 3) switch
                {
                    0 => "晴",
                    1 => "多云",
                    _ => "小雨"
                },
                Humidity = Random.Shared.Next(40, 80)
            };
            
            return Task.FromResult(new PluginToolResult
            {
                Name = Name,
                Success = true,
                Content = $"城市: {weather.City}\n温度: {weather.Temperature}°C\n天气: {weather.Condition}\n湿度: {weather.Humidity}%"
            });
        }
        catch (Exception ex)
        {
            return Task.FromResult(new PluginToolResult
            {
                Name = Name,
                Success = false,
                Error = $"查询失败: {ex.Message}"
            });
        }
    }
}
