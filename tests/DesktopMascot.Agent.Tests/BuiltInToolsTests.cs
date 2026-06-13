using DesktopMascot.Agent.Tools;

namespace DesktopMascot.Agent.Tests;

public class BuiltInToolsTests
{
    [Fact]
    public async Task GetCurrentTimeTool_ShouldReturnTime()
    {
        var tool = new GetCurrentTimeTool();

        var result = await tool.ExecuteAsync("{}");

        Assert.True(result.Success);
        Assert.NotEmpty(result.Content);
        // 验证返回的是有效时间格式
        Assert.True(DateTime.TryParse(result.Content, out _));
    }

    [Fact]
    public async Task CalculatorTool_ShouldCalculate()
    {
        var tool = new CalculatorTool();
        var args = """{"expression": "2 + 3 * 4"}""";

        var result = await tool.ExecuteAsync(args);

        Assert.True(result.Success);
        Assert.Equal("14", result.Content);
    }

    [Fact]
    public async Task CalculatorTool_InvalidExpression_ShouldFail()
    {
        var tool = new CalculatorTool();
        var args = """{"expression": "invalid"}""";

        var result = await tool.ExecuteAsync(args);

        Assert.False(result.Success);
        Assert.NotNull(result.Error);
        Assert.NotEmpty(result.Error);
    }

    [Fact]
    public void GetCurrentTimeTool_Metadata_ShouldBeCorrect()
    {
        var tool = new GetCurrentTimeTool();

        Assert.Equal("get_current_time", tool.Name);
        Assert.NotEmpty(tool.Description);
        Assert.Equal("{}", tool.ParametersSchema);
    }

    [Fact]
    public void CalculatorTool_Metadata_ShouldBeCorrect()
    {
        var tool = new CalculatorTool();

        Assert.Equal("calculator", tool.Name);
        Assert.NotEmpty(tool.Description);
        Assert.Contains("expression", tool.ParametersSchema);
    }
}
