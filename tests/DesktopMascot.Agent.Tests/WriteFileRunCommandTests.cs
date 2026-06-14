using DesktopMascot.Agent.Tools;
using DesktopMascot.Agent.Context;
using System.Text.Json;

namespace DesktopMascot.Agent.Tests;

public class WriteFileRunCommandTests
{
    [Fact]
    public async Task WriteFileTool_ShouldCreateFile()
    {
        var provider = new MockContextProvider();
        var tool = new WriteFileTool(provider);
        var tempFile = Path.Combine(Path.GetTempPath(), $"test_write_{Guid.NewGuid():N}.txt");

        try
        {
            var args = JsonSerializer.Serialize(new { path = tempFile, content = "Hello World" });
            var result = await tool.ExecuteAsync(args);

            Assert.True(result.Success);
            Assert.Contains("创建", result.Content);
            Assert.True(File.Exists(tempFile));
            Assert.Equal("Hello World", await File.ReadAllTextAsync(tempFile));
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task WriteFileTool_OverwriteExisting_ShouldWork()
    {
        var provider = new MockContextProvider();
        var tool = new WriteFileTool(provider);
        var tempFile = Path.Combine(Path.GetTempPath(), $"test_overwrite_{Guid.NewGuid():N}.txt");

        try
        {
            await File.WriteAllTextAsync(tempFile, "old content");
            var args = JsonSerializer.Serialize(new { path = tempFile, content = "new content" });
            var result = await tool.ExecuteAsync(args);

            Assert.True(result.Success);
            Assert.Contains("覆盖", result.Content);
            Assert.Equal("new content", await File.ReadAllTextAsync(tempFile));
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task WriteFileTool_EmptyPath_ShouldFail()
    {
        var provider = new MockContextProvider();
        var tool = new WriteFileTool(provider);

        var args = JsonSerializer.Serialize(new { path = "", content = "test" });
        var result = await tool.ExecuteAsync(args);

        Assert.False(result.Success);
        Assert.Contains("不能为空", result.Error);
    }

    [Fact]
    public async Task WriteFileTool_Metadata_ShouldBeCorrect()
    {
        var provider = new MockContextProvider();
        var tool = new WriteFileTool(provider);

        Assert.Equal("write_file", tool.Name);
        Assert.True(tool.RequiresConfirmation);
        Assert.NotEmpty(tool.ConfirmationMessage);
    }

    [Fact]
    public async Task RunCommandTool_ShouldExecuteCommand()
    {
        var tool = new RunCommandTool();
        var args = JsonSerializer.Serialize(new { command = "echo hello" });
        var result = await tool.ExecuteAsync(args);

        Assert.True(result.Success);
        Assert.Contains("hello", result.Content);
    }

    [Fact]
    public async Task RunCommandTool_DangerousCommand_ShouldReject()
    {
        var tool = new RunCommandTool();
        var args = JsonSerializer.Serialize(new { command = "rm -rf /" });
        var result = await tool.ExecuteAsync(args);

        Assert.False(result.Success);
        Assert.Contains("危险命令", result.Error);
    }

    [Fact]
    public async Task RunCommandTool_EmptyCommand_ShouldFail()
    {
        var tool = new RunCommandTool();
        var args = JsonSerializer.Serialize(new { command = "" });
        var result = await tool.ExecuteAsync(args);

        Assert.False(result.Success);
        Assert.Contains("不能为空", result.Error);
    }

    [Fact]
    public async Task RunCommandTool_Metadata_ShouldBeCorrect()
    {
        var tool = new RunCommandTool();

        Assert.Equal("run_command", tool.Name);
        Assert.True(tool.RequiresConfirmation);
        Assert.NotEmpty(tool.ConfirmationMessage);
    }

    [Fact]
    public void ToolRegistry_RequiresConfirmation_ShouldWork()
    {
        var registry = new ToolRegistry();
        registry.Register(new WriteFileTool(new MockContextProvider()));
        registry.Register(new RunCommandTool());
        registry.Register(new GetCurrentTimeTool());

        Assert.True(registry.RequiresConfirmation("write_file"));
        Assert.True(registry.RequiresConfirmation("run_command"));
        Assert.False(registry.RequiresConfirmation("get_current_time"));
    }
}
