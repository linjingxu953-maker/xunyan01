using System.Net;
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
        // 用 mock handler 模拟成功响应，避免真实网络请求卡死测试
        var handler = new MockHttpHandler(HttpStatusCode.OK, """{"status": "ok"}""");
        var tool = new NetworkRequestTool(handler);
        var args = JsonSerializer.Serialize(new { method = "GET", url = "https://mock.local/test" });

        var result = await tool.ExecuteAsync(args);

        Assert.True(result.Success);
        Assert.Contains("200", result.Content);
        Assert.Contains("ok", result.Content);
        Assert.Contains("https://mock.local/test", result.Content);
    }

    [Fact]
    public async Task NetworkRequestTool_PostRequest_ShouldIncludeBody()
    {
        var handler = new MockHttpHandler(HttpStatusCode.Created, """{"id": 42}""");
        var tool = new NetworkRequestTool(handler);
        var args = JsonSerializer.Serialize(new
        {
            method = "POST",
            url = "https://mock.local/api",
            body = """{"name":"test"}"""
        });

        var result = await tool.ExecuteAsync(args);

        Assert.True(result.Success);
        Assert.Contains("201", result.Content);
        Assert.Contains("42", result.Content);
    }

    [Fact]
    public void NetworkRequestTool_Metadata_ShouldBeCorrect()
    {
        var tool = new NetworkRequestTool();
        Assert.Equal("network_request", tool.Name);
        Assert.Contains("GET", tool.ParametersSchema);
        Assert.Contains("POST", tool.ParametersSchema);
    }

    /// <summary>可控制的 HttpMessageHandler，返回预设响应</summary>
    private sealed class MockHttpHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _statusCode;
        private readonly string _content;

        public MockHttpHandler(HttpStatusCode statusCode, string content)
        {
            _statusCode = statusCode;
            _content = content;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(_statusCode)
            {
                Content = new StringContent(_content)
            });
        }
    }
}
