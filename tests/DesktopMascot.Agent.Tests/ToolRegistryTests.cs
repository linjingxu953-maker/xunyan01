using DesktopMascot.Agent.Models;
using DesktopMascot.Agent.Tools;

namespace DesktopMascot.Agent.Tests;

public class ToolRegistryTests
{
    [Fact]
    public void Register_ShouldAddTool()
    {
        var registry = new ToolRegistry();
        var tool = new GetCurrentTimeTool();

        registry.Register(tool);

        Assert.Equal(1, registry.Count);
    }

    [Fact]
    public void GetTool_ShouldReturnTool()
    {
        var registry = new ToolRegistry();
        var tool = new GetCurrentTimeTool();
        registry.Register(tool);

        var result = registry.GetTool("get_current_time");

        Assert.NotNull(result);
        Assert.Equal("get_current_time", result!.Name);
    }

    [Fact]
    public void GetTool_NonExisting_ShouldReturnNull()
    {
        var registry = new ToolRegistry();

        var result = registry.GetTool("non_existing");

        Assert.Null(result);
    }

    [Fact]
    public void GetToolDefinitions_ShouldReturnAllTools()
    {
        var registry = new ToolRegistry();
        registry.Register(new GetCurrentTimeTool());
        registry.Register(new CalculatorTool());

        var definitions = registry.GetToolDefinitions().ToList();

        Assert.Equal(2, definitions.Count);
        Assert.Contains(definitions, d => d.Name == "get_current_time");
        Assert.Contains(definitions, d => d.Name == "calculator");
    }

    [Fact]
    public async Task ExecuteToolAsync_ExistingTool_ShouldSucceed()
    {
        var registry = new ToolRegistry();
        registry.Register(new GetCurrentTimeTool());

        var result = await registry.ExecuteToolAsync(new ToolCall
        {
            Name = "get_current_time",
            Arguments = "{}"
        });

        Assert.True(result.Success);
        Assert.NotEmpty(result.Content);
    }

    [Fact]
    public async Task ExecuteToolAsync_NonExistingTool_ShouldFail()
    {
        var registry = new ToolRegistry();

        var result = await registry.ExecuteToolAsync(new ToolCall
        {
            Name = "non_existing",
            Arguments = "{}"
        });

        Assert.False(result.Success);
        Assert.Contains("不存在", result.Error);
    }
}
