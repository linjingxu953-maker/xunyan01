using System.Text.Json;
using DesktopMascot.Agent.Tools;

namespace DesktopMascot.Agent.Tests;

public class NetworkRequestToolTests
{
    [Fact]
    public async Task NetworkRequestTool_MissingUrl_ShouldFail()
    {
        var tool = new NetworkRequestTool();
        var args = JsonSerializer.Serialize(new { method = "GET" });
        var result = await tool.ExecuteAsync(args);

        Assert.False(result.Success);
        Assert.Contains("缺少 url", result.Error);
    }

    [Fact]
    public async Task NetworkRequestTool_GetRequest_ShouldWork()
    {
        var tool = new NetworkRequestTool();
        var args = JsonSerializer.Serialize(new { method = "GET", url = "https://httpbin.org/get" });
        var result = await tool.ExecuteAsync(args);

        // httpbin.org 可能不可用，所以只检查格式
        Assert.True(result.Success || result.Content.Contains("状态码"));
    }

    [Fact]
    public void NetworkRequestTool_Metadata_ShouldBeCorrect()
    {
        var tool = new NetworkRequestTool();
        Assert.Equal("network_request", tool.Name);
        Assert.Contains("GET", tool.ParametersSchema);
        Assert.Contains("POST", tool.ParametersSchema);
    }
}
