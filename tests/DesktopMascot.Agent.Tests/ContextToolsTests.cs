using DesktopMascot.Agent.Context;

namespace DesktopMascot.Agent.Tests;

public class ContextToolsTests
{
    [Fact]
    public async Task GetActiveWindowTool_ShouldReturnWindowInfo()
    {
        var provider = new MockContextProvider
        {
            MockWindowTitle = "Test Window",
            MockAppName = "TestApp"
        };
        var tool = new GetActiveWindowTool(provider);

        var result = await tool.ExecuteAsync("{}");

        Assert.True(result.Success);
        Assert.Contains("Test Window", result.Content);
        Assert.Contains("TestApp", result.Content);
    }

    [Fact]
    public async Task ReadFileTool_ShouldReadFile()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(tempFile, "测试文件内容");
            
            var provider = new WindowsContextProvider();
            var tool = new ReadFileTool(provider);
            var args = $"{{\"path\": \"{tempFile.Replace("\\", "\\\\")}\"}}";

            var result = await tool.ExecuteAsync(args);

            Assert.True(result.Success);
            Assert.Contains("测试文件内容", result.Content);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task ReadFileTool_EmptyPath_ShouldFail()
    {
        var provider = new WindowsContextProvider();
        var tool = new ReadFileTool(provider);

        var result = await tool.ExecuteAsync("""{"path": ""}""");

        Assert.False(result.Success);
        Assert.Contains("不能为空", result.Error);
    }

    [Fact]
    public void GetActiveWindowTool_Metadata_ShouldBeCorrect()
    {
        var provider = new MockContextProvider();
        var tool = new GetActiveWindowTool(provider);

        Assert.Equal("get_active_window", tool.Name);
        Assert.NotEmpty(tool.Description);
    }

    [Fact]
    public void ReadFileTool_Metadata_ShouldBeCorrect()
    {
        var provider = new WindowsContextProvider();
        var tool = new ReadFileTool(provider);

        Assert.Equal("read_file", tool.Name);
        Assert.NotEmpty(tool.Description);
        Assert.Contains("path", tool.ParametersSchema);
    }
}
