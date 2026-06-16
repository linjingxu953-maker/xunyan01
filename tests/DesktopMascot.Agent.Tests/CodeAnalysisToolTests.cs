using System.Text.Json;
using DesktopMascot.Agent.Tools;

namespace DesktopMascot.Agent.Tests;

public class CodeAnalysisToolTests
{
    [Fact]
    public async Task AnalyzeQuality_ShouldReturnScore()
    {
        var tempFile = Path.GetTempPath();
        var testFile = Path.Combine(tempFile, $"test_{Guid.NewGuid():N}.cs");

        try
        {
            await File.WriteAllTextAsync(testFile, "public class Test\n{\n    public void Method() { }\n}");

            var tool = new CodeAnalysisTool();
            var args = JsonSerializer.Serialize(new { action = "quality", file_path = testFile });
            var result = await tool.ExecuteAsync(args);

            Assert.True(result.Success);
            Assert.Contains("质量评分", result.Content);
        }
        finally
        {
            if (File.Exists(testFile)) File.Delete(testFile);
        }
    }

    [Fact]
    public async Task AnalyzeComplexity_ShouldReturnMetrics()
    {
        var tempFile = Path.GetTempPath();
        var testFile = Path.Combine(tempFile, $"test_{Guid.NewGuid():N}.cs");

        try
        {
            await File.WriteAllTextAsync(testFile, "public class Test\n{\n    public void Method() { }\n}");

            var tool = new CodeAnalysisTool();
            var args = JsonSerializer.Serialize(new { action = "complexity", file_path = testFile });
            var result = await tool.ExecuteAsync(args);

            Assert.True(result.Success);
            Assert.Contains("总行数", result.Content);
        }
        finally
        {
            if (File.Exists(testFile)) File.Delete(testFile);
        }
    }

    [Fact]
    public async Task AnalyzeDependencies_ShouldFindImports()
    {
        var tempFile = Path.GetTempPath();
        var testFile = Path.Combine(tempFile, $"test_{Guid.NewGuid():N}.cs");

        try
        {
            await File.WriteAllTextAsync(testFile, "using System;\nusing MyNamespace;\nusing AnotherLib;");

            var tool = new CodeAnalysisTool();
            var args = JsonSerializer.Serialize(new { action = "dependencies", file_path = testFile });
            var result = await tool.ExecuteAsync(args);

            Assert.True(result.Success);
            Assert.Contains("MyNamespace", result.Content);
            Assert.Contains("AnotherLib", result.Content);
        }
        finally
        {
            if (File.Exists(testFile)) File.Delete(testFile);
        }
    }

    [Fact]
    public async Task AnalyzeStats_ShouldReturnStats()
    {
        var tempFile = Path.GetTempPath();
        var testFile = Path.Combine(tempFile, $"test_{Guid.NewGuid():N}.cs");

        try
        {
            await File.WriteAllTextAsync(testFile, "public class Test\n{\n    public void Method() { }\n}");

            var tool = new CodeAnalysisTool();
            var args = JsonSerializer.Serialize(new { action = "stats", file_path = testFile });
            var result = await tool.ExecuteAsync(args);

            Assert.True(result.Success);
            Assert.Contains("总行数", result.Content);
        }
        finally
        {
            if (File.Exists(testFile)) File.Delete(testFile);
        }
    }

    [Fact]
    public async Task AnalyzeStructure_ShouldReturnDistribution()
    {
        var tempDir = Path.GetTempPath();
        var testDir = Path.Combine(tempDir, $"test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(testDir);

        try
        {
            await File.WriteAllTextAsync(Path.Combine(testDir, "test1.cs"), "code");
            await File.WriteAllTextAsync(Path.Combine(testDir, "test2.txt"), "text");

            var tool = new CodeAnalysisTool();
            var args = JsonSerializer.Serialize(new { action = "structure", directory = testDir });
            var result = await tool.ExecuteAsync(args);

            Assert.True(result.Success);
            Assert.Contains(".cs", result.Content);
            Assert.Contains(".txt", result.Content);
        }
        finally
        {
            if (Directory.Exists(testDir)) Directory.Delete(testDir, true);
        }
    }

    [Fact]
    public async Task AnalyzeQuality_MissingFile_ShouldFail()
    {
        var tool = new CodeAnalysisTool();
        var args = JsonSerializer.Serialize(new { action = "quality", file_path = "nonexistent.cs" });
        var result = await tool.ExecuteAsync(args);

        Assert.False(result.Success);
        Assert.Contains("不存在", result.Error);
    }

    [Fact]
    public void CodeAnalysisTool_Metadata_ShouldBeCorrect()
    {
        var tool = new CodeAnalysisTool();
        Assert.Equal("code_analysis", tool.Name);
        Assert.Contains("quality", tool.ParametersSchema);
        Assert.Contains("complexity", tool.ParametersSchema);
    }
}
