using DesktopMascot.Agent.Context;

namespace DesktopMascot.Agent.Tests;

public class ContextProviderTests
{
    [Fact]
    public async Task GetActiveWindowContext_ShouldReturnSnapshot()
    {
        var provider = new WindowsContextProvider();

        var snapshot = await provider.GetActiveWindowContextAsync();

        Assert.NotNull(snapshot);
        Assert.NotEmpty(snapshot.ActiveWindowTitle);
        Assert.NotNull(snapshot.ActiveApplication);
    }

    [Fact]
    public async Task ReadFile_ExistingFile_ShouldReturnContent()
    {
        var provider = new WindowsContextProvider();
        var tempFile = Path.GetTempFileName();
        
        try
        {
            await File.WriteAllTextAsync(tempFile, "测试内容");

            var content = await provider.ReadFileAsync(tempFile);

            Assert.Equal("测试内容", content);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task ReadFile_NonExistingFile_ShouldReturnNull()
    {
        var provider = new WindowsContextProvider();

        var content = await provider.ReadFileAsync("/non/existing/file.txt");

        Assert.Null(content);
    }

    [Fact]
    public async Task GetFullContext_ShouldReturnCompleteSnapshot()
    {
        var provider = new WindowsContextProvider();

        var snapshot = await provider.GetFullContextAsync();

        Assert.NotNull(snapshot);
        Assert.True(snapshot.CapturedAt <= DateTime.UtcNow);
    }
}

public class MockContextProviderTests
{
    [Fact]
    public async Task GetActiveWindowContext_ShouldReturnMockData()
    {
        var provider = new MockContextProvider
        {
            MockWindowTitle = "My App",
            MockAppName = "MyApp.exe"
        };

        var snapshot = await provider.GetActiveWindowContextAsync();

        Assert.Equal("My App", snapshot.ActiveWindowTitle);
        Assert.Equal("MyApp.exe", snapshot.ActiveApplication);
    }

    [Fact]
    public async Task GetSelectedText_ShouldReturnMockText()
    {
        var provider = new MockContextProvider
        {
            MockSelectedText = "Selected content"
        };

        var text = await provider.GetSelectedTextAsync();

        Assert.Equal("Selected content", text);
    }

    [Fact]
    public async Task ReadFile_ShouldReturnMockContent()
    {
        var provider = new MockContextProvider
        {
            MockFileContent = "File content"
        };

        var content = await provider.ReadFileAsync("any/path");

        Assert.Equal("File content", content);
    }
}
