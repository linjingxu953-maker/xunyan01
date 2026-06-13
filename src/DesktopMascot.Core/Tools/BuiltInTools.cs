using System.Text.Json;

namespace DesktopMascot.Core.Tools;

/// <summary>
/// 工具基类
/// </summary>
public abstract class ToolBase : ITool
{
    public abstract ToolDefinition Definition { get; }

    public abstract Task<ToolCallResponse> ExecuteAsync(ToolCallRequest request, CancellationToken ct = default);

    public virtual Task<bool> ValidateArgumentsAsync(string arguments, CancellationToken ct = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(Definition.ParametersSchema) || Definition.ParametersSchema == "{}")
                return Task.FromResult(true);

            JsonDocument.Parse(arguments);
            return Task.FromResult(true);
        }
        catch
        {
            return Task.FromResult(false);
        }
    }

    protected T ParseArguments<T>(string arguments) where T : class
    {
        return JsonSerializer.Deserialize<T>(arguments) ?? throw new InvalidOperationException("参数解析失败");
    }

    protected ToolCallResponse Success(string result)
    {
        return new ToolCallResponse
        {
            ToolName = Definition.Name,
            Success = true,
            Result = result
        };
    }

    protected ToolCallResponse Fail(string error)
    {
        return new ToolCallResponse
        {
            ToolName = Definition.Name,
            Success = false,
            Error = error
        };
    }
}

/// <summary>
/// 获取当前时间工具
/// </summary>
public class GetCurrentTimeTool : ToolBase
{
    public override ToolDefinition Definition => new()
    {
        Name = "get_current_time",
        Description = "获取当前日期和时间",
        Category = ToolCategories.System,
        ParametersSchema = "{}",
        Tags = new() { "time", "system" }
    };

    public override Task<ToolCallResponse> ExecuteAsync(ToolCallRequest request, CancellationToken ct = default)
    {
        return Task.FromResult(Success(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")));
    }
}

/// <summary>
/// 计算器工具
/// </summary>
public class CalculatorTool : ToolBase
{
    public override ToolDefinition Definition => new()
    {
        Name = "calculator",
        Description = "执行数学计算",
        Category = ToolCategories.Data,
        ParametersSchema = """
        {
            "type": "object",
            "properties": {
                "expression": {
                    "type": "string",
                    "description": "数学表达式"
                }
            },
            "required": ["expression"]
        }
        """,
        Tags = new() { "math", "calculator" }
    };

    public override Task<ToolCallResponse> ExecuteAsync(ToolCallRequest request, CancellationToken ct = default)
    {
        try
        {
            var doc = JsonDocument.Parse(request.Arguments);
            var expression = doc.RootElement.GetProperty("expression").GetString() ?? "";

            var dt = new System.Data.DataTable();
            var result = dt.Compute(expression, "");

            return Task.FromResult(Success(result.ToString() ?? "0"));
        }
        catch (Exception ex)
        {
            return Task.FromResult(Fail($"计算错误: {ex.Message}"));
        }
    }
}

/// <summary>
/// 获取随机名言工具
/// </summary>
public class GetRandomQuoteTool : ToolBase
{
    private static readonly List<string> Quotes = new()
    {
        "代码是写给人看的，顺便让机器执行。 - Harold Abelson",
        "先让它工作，再让它正确，最后让它快。 - Kent Beck",
        "过早优化是万恶之源。 - Donald Knuth",
        "简单是可靠的先决条件。 - Edsger Dijkstra",
        "任何足够先进的技术都与魔法无异。 - Arthur C. Clarke"
    };

    public override ToolDefinition Definition => new()
    {
        Name = "get_random_quote",
        Description = "获取一条随机编程名言",
        Category = ToolCategories.General,
        ParametersSchema = "{}",
        Tags = new() { "quote", "inspiration" }
    };

    public override Task<ToolCallResponse> ExecuteAsync(ToolCallRequest request, CancellationToken ct = default)
    {
        var random = new Random();
        var quote = Quotes[random.Next(Quotes.Count)];
        return Task.FromResult(Success(quote));
    }
}

/// <summary>
/// 天气查询工具（模拟）
/// </summary>
public class GetWeatherTool : ToolBase
{
    public override ToolDefinition Definition => new()
    {
        Name = "get_weather",
        Description = "查询指定城市的天气（模拟数据）",
        Category = ToolCategories.Network,
        ParametersSchema = """
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
        """,
        Tags = new() { "weather", "api" }
    };

    public override Task<ToolCallResponse> ExecuteAsync(ToolCallRequest request, CancellationToken ct = default)
    {
        try
        {
            var doc = JsonDocument.Parse(request.Arguments);
            var city = doc.RootElement.GetProperty("city").GetString() ?? "北京";

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

            var result = $"城市: {weather.City}\n温度: {weather.Temperature}°C\n天气: {weather.Condition}\n湿度: {weather.Humidity}%";
            return Task.FromResult(Success(result));
        }
        catch (Exception ex)
        {
            return Task.FromResult(Fail($"查询失败: {ex.Message}"));
        }
    }
}
