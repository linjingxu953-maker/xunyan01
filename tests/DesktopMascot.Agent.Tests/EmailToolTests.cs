using System.Text.Json;
using DesktopMascot.Agent.Tools;

namespace DesktopMascot.Agent.Tests;

public class EmailToolTests
{
    [Fact]
    public async Task Configure_ShouldSetServer()
    {
        var tool = new EmailTool();
        var args = JsonSerializer.Serialize(new
        {
            action = "configure",
            smtp_host = "smtp.gmail.com",
            smtp_port = 587,
            username = "test@gmail.com",
            password = "password"
        });

        var result = await tool.ExecuteAsync(args);

        Assert.True(result.Success);
        Assert.Contains("已配置邮件服务器", result.Content);
    }

    [Fact]
    public async Task Configure_MissingHost_ShouldFail()
    {
        var tool = new EmailTool();
        var args = JsonSerializer.Serialize(new { action = "configure" });
        var result = await tool.ExecuteAsync(args);

        Assert.False(result.Success);
        Assert.Contains("缺少 smtp_host", result.Error);
    }

    [Fact]
    public async Task Send_BeforeConfigure_ShouldFail()
    {
        var tool = new EmailTool();
        var args = JsonSerializer.Serialize(new
        {
            action = "send",
            to = "test@example.com",
            subject = "Test",
            body = "Hello"
        });

        var result = await tool.ExecuteAsync(args);

        Assert.False(result.Success);
        Assert.Contains("请先配置", result.Error);
    }

    [Fact]
    public async Task Send_MissingTo_ShouldFail()
    {
        var tool = new EmailTool();
        await tool.ExecuteAsync(JsonSerializer.Serialize(new
        {
            action = "configure",
            smtp_host = "smtp.gmail.com"
        }));

        var args = JsonSerializer.Serialize(new { action = "send", subject = "Test" });
        var result = await tool.ExecuteAsync(args);

        Assert.False(result.Success);
        Assert.Contains("缺少 to", result.Error);
    }

    [Fact]
    public async Task Send_MissingSubject_ShouldFail()
    {
        var tool = new EmailTool();
        await tool.ExecuteAsync(JsonSerializer.Serialize(new
        {
            action = "configure",
            smtp_host = "smtp.gmail.com"
        }));

        var args = JsonSerializer.Serialize(new { action = "send", to = "test@example.com" });
        var result = await tool.ExecuteAsync(args);

        Assert.False(result.Success);
        Assert.Contains("缺少 subject", result.Error);
    }

    [Fact]
    public void EmailTool_Metadata_ShouldBeCorrect()
    {
        var tool = new EmailTool();
        Assert.Equal("email", tool.Name);
        Assert.Contains("send", tool.ParametersSchema);
        Assert.Contains("configure", tool.ParametersSchema);
    }
}
