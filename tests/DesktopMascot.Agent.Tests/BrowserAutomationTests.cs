using System.Text.Json;
using DesktopMascot.Agent.Tools;

namespace DesktopMascot.Agent.Tests;

public class BrowserAutomationTests
{
    [Fact]
    public async Task BrowserAutomationTool_OpenMissingUrl_ShouldFail()
    {
        var tool = new BrowserAutomationTool();
        var args = JsonSerializer.Serialize(new { action = "open" });
        var result = await tool.ExecuteAsync(args);

        Assert.False(result.Success);
        Assert.Contains("缺少 url", result.Error);
    }

    [Fact]
    public async Task BrowserAutomationTool_ClickMissingSelector_ShouldFail()
    {
        var tool = new BrowserAutomationTool();
        var args = JsonSerializer.Serialize(new { action = "click" });
        var result = await tool.ExecuteAsync(args);

        Assert.False(result.Success);
        Assert.Contains("缺少 selector", result.Error);
    }

    [Fact]
    public async Task BrowserAutomationTool_TypeMissingText_ShouldFail()
    {
        var tool = new BrowserAutomationTool();
        var args = JsonSerializer.Serialize(new { action = "type" });
        var result = await tool.ExecuteAsync(args);

        Assert.False(result.Success);
        Assert.Contains("缺少 text", result.Error);
    }

    [Fact]
    public async Task BrowserAutomationTool_UnsupportedAction_ShouldFail()
    {
        var tool = new BrowserAutomationTool();
        var args = JsonSerializer.Serialize(new { action = "unsupported" });
        var result = await tool.ExecuteAsync(args);

        Assert.False(result.Success);
        Assert.Contains("不支持", result.Error);
    }

    [Fact]
    public void BrowserAutomationTool_Metadata_ShouldBeCorrect()
    {
        var tool = new BrowserAutomationTool();
        Assert.Equal("browser_automation", tool.Name);
        Assert.Contains("open", tool.ParametersSchema);
        Assert.Contains("click", tool.ParametersSchema);
        Assert.Contains("screenshot", tool.ParametersSchema);
    }

    [Fact]
    public async Task BrowserAutomationTool_Scroll_ShouldWork()
    {
        var tool = new BrowserAutomationTool();
        var args = JsonSerializer.Serialize(new { action = "scroll", scroll_amount = -120 });
        var result = await tool.ExecuteAsync(args);

        Assert.True(result.Success);
        Assert.Contains("滚动", result.Content);
    }
}
