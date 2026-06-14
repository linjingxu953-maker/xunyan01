using DesktopMascot.Agent.Context;
using DesktopMascot.Agent.Tools;
using System.Text.Json;

namespace DesktopMascot.Agent.Tests;

public class EditFileSearchFileTests
{
    [Fact]
    public async Task EditFileTool_Replace_ShouldWork()
    {
        var provider = new MockContextProvider();
        var tool = new EditFileTool(provider);
        var tempFile = Path.Combine(Path.GetTempPath(), $"test_edit_{Guid.NewGuid():N}.txt");

        try
        {
            await File.WriteAllTextAsync(tempFile, "Hello World\nFoo Bar\nHello Again");

            var args = JsonSerializer.Serialize(new { path = tempFile, mode = "replace", old_text = "Hello", new_text = "Hi" });
            var result = await tool.ExecuteAsync(args);

            Assert.True(result.Success);
            var content = await File.ReadAllTextAsync(tempFile);
            Assert.Contains("Hi World", content);
            Assert.Contains("Hi Again", content);
            Assert.DoesNotContain("Hello", content);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task EditFileTool_InsertAtLine_ShouldWork()
    {
        var provider = new MockContextProvider();
        var tool = new EditFileTool(provider);
        var tempFile = Path.Combine(Path.GetTempPath(), $"test_insert_{Guid.NewGuid():N}.txt");

        try
        {
            await File.WriteAllTextAsync(tempFile, "Line1\nLine2\nLine3");

            var args = JsonSerializer.Serialize(new { path = tempFile, mode = "insert", line_number = 2, new_text = "Inserted" });
            var result = await tool.ExecuteAsync(args);

            Assert.True(result.Success);
            var content = await File.ReadAllTextAsync(tempFile);
            Assert.Contains("Line1\nInserted\nLine2", content);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task EditFileTool_DeleteLine_ShouldWork()
    {
        var provider = new MockContextProvider();
        var tool = new EditFileTool(provider);
        var tempFile = Path.Combine(Path.GetTempPath(), $"test_delete_{Guid.NewGuid():N}.txt");

        try
        {
            await File.WriteAllTextAsync(tempFile, "Line1\nLine2\nLine3");

            var args = JsonSerializer.Serialize(new { path = tempFile, mode = "delete", line_number = 2 });
            var result = await tool.ExecuteAsync(args);

            Assert.True(result.Success);
            var content = await File.ReadAllTextAsync(tempFile);
            Assert.Contains("Line1\nLine3", content);
            Assert.DoesNotContain("Line2", content);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task EditFileTool_Append_ShouldWork()
    {
        var provider = new MockContextProvider();
        var tool = new EditFileTool(provider);
        var tempFile = Path.Combine(Path.GetTempPath(), $"test_append_{Guid.NewGuid():N}.txt");

        try
        {
            await File.WriteAllTextAsync(tempFile, "Start");

            var args = JsonSerializer.Serialize(new { path = tempFile, mode = "append", new_text = "\nEnd" });
            var result = await tool.ExecuteAsync(args);

            Assert.True(result.Success);
            var content = await File.ReadAllTextAsync(tempFile);
            Assert.Equal("Start\nEnd", content);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task EditFileTool_Prepend_ShouldWork()
    {
        var provider = new MockContextProvider();
        var tool = new EditFileTool(provider);
        var tempFile = Path.Combine(Path.GetTempPath(), $"test_prepend_{Guid.NewGuid():N}.txt");

        try
        {
            await File.WriteAllTextAsync(tempFile, "End");

            var args = JsonSerializer.Serialize(new { path = tempFile, mode = "prepend", new_text = "Start\n" });
            var result = await tool.ExecuteAsync(args);

            Assert.True(result.Success);
            var content = await File.ReadAllTextAsync(tempFile);
            Assert.Equal("Start\nEnd", content);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task EditFileTool_Metadata_ShouldBeCorrect()
    {
        var provider = new MockContextProvider();
        var tool = new EditFileTool(provider);

        Assert.Equal("edit_file", tool.Name);
        Assert.True(tool.RequiresConfirmation);
        Assert.Contains("replace", tool.ParametersSchema);
        Assert.Contains("insert", tool.ParametersSchema);
    }

    [Fact]
    public async Task SearchFileTool_ByFilename_ShouldWork()
    {
        var provider = new MockContextProvider();
        var tool = new SearchFileTool(provider);
        var tempDir = Path.Combine(Path.GetTempPath(), $"test_search_{Guid.NewGuid():N}");

        try
        {
            Directory.CreateDirectory(tempDir);
            File.WriteAllText(Path.Combine(tempDir, "test1.cs"), "code");
            File.WriteAllText(Path.Combine(tempDir, "test2.txt"), "text");
            File.WriteAllText(Path.Combine(tempDir, "other.js"), "js");

            var args = JsonSerializer.Serialize(new { path = tempDir, query = "test", mode = "filename" });
            var result = await tool.ExecuteAsync(args);

            Assert.True(result.Success);
            Assert.Contains("test1.cs", result.Content);
            Assert.Contains("test2.txt", result.Content);
            Assert.DoesNotContain("other.js", result.Content);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task SearchFileTool_ByContent_ShouldWork()
    {
        var provider = new MockContextProvider();
        var tool = new SearchFileTool(provider);
        var tempDir = Path.Combine(Path.GetTempPath(), $"test_search_content_{Guid.NewGuid():N}");

        try
        {
            Directory.CreateDirectory(tempDir);
            File.WriteAllText(Path.Combine(tempDir, "a.txt"), "Hello World");
            File.WriteAllText(Path.Combine(tempDir, "b.txt"), "Foo Bar");

            var args = JsonSerializer.Serialize(new { path = tempDir, query = "Hello", mode = "content" });
            var result = await tool.ExecuteAsync(args);

            Assert.True(result.Success);
            Assert.Contains("a.txt", result.Content);
            Assert.Contains("Hello World", result.Content);
            Assert.DoesNotContain("b.txt", result.Content);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task SearchFileTool_ByExtension_ShouldWork()
    {
        var provider = new MockContextProvider();
        var tool = new SearchFileTool(provider);
        var tempDir = Path.Combine(Path.GetTempPath(), $"test_search_ext_{Guid.NewGuid():N}");

        try
        {
            Directory.CreateDirectory(tempDir);
            File.WriteAllText(Path.Combine(tempDir, "a.cs"), "code");
            File.WriteAllText(Path.Combine(tempDir, "b.txt"), "text");
            File.WriteAllText(Path.Combine(tempDir, "c.cs"), "more code");

            var args = JsonSerializer.Serialize(new { path = tempDir, query = ".cs", mode = "extension" });
            var result = await tool.ExecuteAsync(args);

            Assert.True(result.Success);
            Assert.Contains("a.cs", result.Content);
            Assert.Contains("c.cs", result.Content);
            Assert.DoesNotContain("b.txt", result.Content);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task SearchFileTool_NoResults_ShouldReturnMessage()
    {
        var provider = new MockContextProvider();
        var tool = new SearchFileTool(provider);
        var tempDir = Path.Combine(Path.GetTempPath(), $"test_search_empty_{Guid.NewGuid():N}");

        try
        {
            Directory.CreateDirectory(tempDir);
            File.WriteAllText(Path.Combine(tempDir, "a.txt"), "hello");

            var args = JsonSerializer.Serialize(new { path = tempDir, query = "nonexistent", mode = "filename" });
            var result = await tool.ExecuteAsync(args);

            Assert.True(result.Success);
            Assert.Contains("未找到", result.Content);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void ToolRegistry_ShouldHaveAllNewTools()
    {
        var registry = new ToolRegistry();
        var provider = new MockContextProvider();
        registry.Register(new EditFileTool(provider));
        registry.Register(new SearchFileTool(provider));

        Assert.True(registry.RequiresConfirmation("edit_file"));
        Assert.False(registry.RequiresConfirmation("search_file"));
    }
}
