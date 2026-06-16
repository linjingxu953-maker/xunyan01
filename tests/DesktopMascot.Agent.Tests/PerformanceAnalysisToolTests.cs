using System.Text.Json;
using DesktopMascot.Agent.Tools;

namespace DesktopMascot.Agent.Tests;

public class PerformanceAnalysisToolTests
{
    [Fact]
    public async Task RunBenchmark_ShouldReturnResults()
    {
        var tool = new PerformanceAnalysisTool();
        var args = JsonSerializer.Serialize(new { action = "benchmark", iterations = 5 });
        var result = await tool.ExecuteAsync(args);

        Assert.True(result.Success);
        Assert.Contains("基准测试结果", result.Content);
        Assert.Contains("平均耗时", result.Content);
    }

    [Fact]
    public async Task AnalyzeMemory_ShouldReturnStats()
    {
        var tool = new PerformanceAnalysisTool();
        var args = JsonSerializer.Serialize(new { action = "memory" });
        var result = await tool.ExecuteAsync(args);

        Assert.True(result.Success);
        Assert.Contains("内存分析", result.Content);
        Assert.Contains("工作集", result.Content);
    }

    [Fact]
    public async Task AnalyzeCpu_ShouldReturnStats()
    {
        var tool = new PerformanceAnalysisTool();
        var args = JsonSerializer.Serialize(new { action = "cpu" });
        var result = await tool.ExecuteAsync(args);

        Assert.True(result.Success);
        Assert.Contains("CPU 分析", result.Content);
        Assert.Contains("CPU 时间", result.Content);
    }

    [Fact]
    public async Task DetectBottlenecks_ShouldReturnAnalysis()
    {
        var tool = new PerformanceAnalysisTool();
        var args = JsonSerializer.Serialize(new { action = "bottleneck" });
        var result = await tool.ExecuteAsync(args);

        Assert.True(result.Success);
        Assert.Contains("瓶颈检测", result.Content);
    }

    [Fact]
    public async Task SuggestOptimizations_ShouldReturnSuggestions()
    {
        var tempFile = Path.GetTempPath();
        var testFile = Path.Combine(tempFile, $"test_{Guid.NewGuid():N}.cs");

        try
        {
            await File.WriteAllTextAsync(testFile, "var x = string + \"test\";");

            var tool = new PerformanceAnalysisTool();
            var args = JsonSerializer.Serialize(new { action = "optimize", file_path = testFile });
            var result = await tool.ExecuteAsync(args);

            Assert.True(result.Success);
            Assert.Contains("优化建议", result.Content);
        }
        finally
        {
            if (File.Exists(testFile)) File.Delete(testFile);
        }
    }

    [Fact]
    public void PerformanceAnalysisTool_Metadata_ShouldBeCorrect()
    {
        var tool = new PerformanceAnalysisTool();
        Assert.Equal("performance_analysis", tool.Name);
        Assert.Contains("benchmark", tool.ParametersSchema);
        Assert.Contains("memory", tool.ParametersSchema);
    }
}
