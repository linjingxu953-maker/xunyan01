using DesktopMascot.Core.Tools;

namespace DesktopMascot.Core.Tests;

/// <summary>
/// 测试用禁用工具
/// </summary>
internal class DisabledTool : ToolBase
{
    public override ToolDefinition Definition => new()
    {
        Name = "disabled_tool",
        Description = "已禁用的工具",
        IsEnabled = false
    };

    public override Task<ToolCallResponse> ExecuteAsync(ToolCallRequest request, CancellationToken ct = default)
    {
        return Task.FromResult(Success("不应该执行到这里"));
    }
}

public class ToolRegistryTests
{
    private readonly ToolRegistry _registry;

    public ToolRegistryTests()
    {
        _registry = new ToolRegistry();
    }

    [Fact]
    public void Register_ShouldAddTool()
    {
        var tool = new GetCurrentTimeTool();

        _registry.Register(tool);

        Assert.Single(_registry.GetAllDefinitions());
    }

    [Fact]
    public void Register_Duplicate_ShouldThrow()
    {
        var tool1 = new GetCurrentTimeTool();
        var tool2 = new GetCurrentTimeTool();
        _registry.Register(tool1);

        Assert.Throws<InvalidOperationException>(() => _registry.Register(tool2));
    }

    [Fact]
    public void GetTool_ShouldReturnTool()
    {
        var tool = new GetCurrentTimeTool();
        _registry.Register(tool);

        var result = _registry.GetTool("get_current_time");

        Assert.NotNull(result);
    }

    [Fact]
    public void GetTool_NonExisting_ShouldReturnNull()
    {
        var result = _registry.GetTool("non_existing");

        Assert.Null(result);
    }

    [Fact]
    public void GetByCategory_ShouldFilterTools()
    {
        _registry.Register(new GetCurrentTimeTool()); // system
        _registry.Register(new CalculatorTool()); // data
        _registry.Register(new GetWeatherTool()); // network

        var systemTools = _registry.GetByCategory(ToolCategories.System).ToList();

        Assert.Single(systemTools);
        Assert.Equal("get_current_time", systemTools[0].Name);
    }

    [Fact]
    public void GetByTag_ShouldFilterTools()
    {
        _registry.Register(new GetCurrentTimeTool()); // time, system
        _registry.Register(new CalculatorTool()); // math, calculator
        _registry.Register(new GetWeatherTool()); // weather, api

        var timeTools = _registry.GetByTag("time").ToList();

        Assert.Single(timeTools);
    }

    [Fact]
    public void Search_ShouldFindTools()
    {
        _registry.Register(new GetCurrentTimeTool());
        _registry.Register(new CalculatorTool());
        _registry.Register(new GetWeatherTool());

        var results = _registry.Search("calc").ToList();

        Assert.Single(results);
        Assert.Equal("calculator", results[0].Name);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldExecuteTool()
    {
        _registry.Register(new GetCurrentTimeTool());

        var response = await _registry.ExecuteAsync(new ToolCallRequest
        {
            ToolName = "get_current_time",
            Arguments = "{}"
        });

        Assert.True(response.Success);
        Assert.NotEmpty(response.Result);
    }

    [Fact]
    public async Task ExecuteAsync_NonExisting_ShouldFail()
    {
        var response = await _registry.ExecuteAsync(new ToolCallRequest
        {
            ToolName = "non_existing",
            Arguments = "{}"
        });

        Assert.False(response.Success);
        Assert.Contains("不存在", response.Error);
    }

    [Fact]
    public async Task ExecuteAsync_Disabled_ShouldFail()
    {
        var tool = new DisabledTool();
        _registry.Register(tool);

        var response = await _registry.ExecuteAsync(new ToolCallRequest
        {
            ToolName = "disabled_tool",
            Arguments = "{}"
        });

        Assert.False(response.Success);
        Assert.Contains("禁用", response.Error);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldLogCall()
    {
        _registry.Register(new GetCurrentTimeTool());

        await _registry.ExecuteAsync(new ToolCallRequest
        {
            ToolName = "get_current_time",
            Arguments = "{}"
        });

        var logs = await _registry.GetCallLogsAsync();

        Assert.Single(logs);
        Assert.True(logs[0].Success);
    }
}

public class BuiltInToolsTests_M15
{
    [Fact]
    public void GetCurrentTimeTool_ShouldHaveCorrectDefinition()
    {
        var tool = new GetCurrentTimeTool();

        Assert.Equal("get_current_time", tool.Definition.Name);
        Assert.Equal(ToolCategories.System, tool.Definition.Category);
    }

    [Fact]
    public async Task GetCurrentTimeTool_ShouldReturnTime()
    {
        var tool = new GetCurrentTimeTool();
        var request = new ToolCallRequest { Arguments = "{}" };

        var response = await tool.ExecuteAsync(request);

        Assert.True(response.Success);
        Assert.NotEmpty(response.Result);
    }

    [Fact]
    public async Task CalculatorTool_ShouldCalculate()
    {
        var tool = new CalculatorTool();
        var request = new ToolCallRequest
        {
            Arguments = """{"expression": "2 + 3 * 4"}"""
        };

        var response = await tool.ExecuteAsync(request);

        Assert.True(response.Success);
        Assert.Equal("14", response.Result);
    }

    [Fact]
    public async Task CalculatorTool_InvalidExpression_ShouldFail()
    {
        var tool = new CalculatorTool();
        var request = new ToolCallRequest
        {
            Arguments = """{"expression": "invalid"}"""
        };

        var response = await tool.ExecuteAsync(request);

        Assert.False(response.Success);
        Assert.Contains("错误", response.Error);
    }

    [Fact]
    public async Task GetWeatherTool_ShouldReturnWeather()
    {
        var tool = new GetWeatherTool();
        var request = new ToolCallRequest
        {
            Arguments = """{"city": "北京"}"""
        };

        var response = await tool.ExecuteAsync(request);

        Assert.True(response.Success);
        Assert.Contains("北京", response.Result);
    }

    [Fact]
    public void ToolBase_ValidateArguments_Valid_ShouldReturnTrue()
    {
        var tool = new CalculatorTool();

        var valid = tool.ValidateArgumentsAsync("""{"expression": "1+1"}""").Result;

        Assert.True(valid);
    }

    [Fact]
    public void ToolBase_ValidateArguments_Invalid_ShouldReturnFalse()
    {
        var tool = new CalculatorTool();

        var valid = tool.ValidateArgumentsAsync("not json").Result;

        Assert.False(valid);
    }
}
