using DesktopMascot.Core.Tools;

namespace DesktopMascot.Core.Tests;

public class ToolChainExecutorTests
{
    private readonly ToolRegistry _toolRegistry;
    private readonly ToolChainExecutor _executor;

    public ToolChainExecutorTests()
    {
        _toolRegistry = new ToolRegistry();
        _toolRegistry.Register(new GetCurrentTimeTool());
        _toolRegistry.Register(new CalculatorTool());
        _toolRegistry.Register(new GetRandomQuoteTool());
        _toolRegistry.Register(new GetWeatherTool());
        _executor = new ToolChainExecutor(_toolRegistry);
    }

    [Fact]
    public async Task ExecuteAsync_Sequential_ShouldCompleteAllSteps()
    {
        var chain = new ToolChainBuilder("顺序链")
            .AddStep("get_current_time")
            .AddStep("get_random_quote")
            .Build();

        var result = await _executor.ExecuteAsync(chain);

        Assert.True(result.Success);
        Assert.Equal(2, result.StepResults.Count);
        Assert.All(result.StepResults, s => Assert.True(s.Success));
    }

    [Fact]
    public async Task ExecuteAsync_Parallel_ShouldCompleteAllSteps()
    {
        var chain = new ToolChainBuilder("并行链")
            .AddParallelSteps("get_current_time", "get_random_quote")
            .Build();

        var result = await _executor.ExecuteAsync(chain);

        Assert.Equal(2, result.StepResults.Count);
        Assert.Contains(result.StepResults, s => s.ToolName == "get_current_time");
        Assert.Contains(result.StepResults, s => s.ToolName == "get_random_quote");
    }

    [Fact]
    public async Task ExecuteAsync_WithCondition_ShouldSkipSteps()
    {
        var chain = new ToolChainBuilder("条件链")
            .WithVariable("enable_calc", false)
            .AddStep("get_current_time")
            .AddStepWithCondition("calculator", "enable_calc == true", """{"expression": "1+1"}""")
            .Build();

        var result = await _executor.ExecuteAsync(chain);

        Assert.True(result.Success);
        Assert.Equal(2, result.StepResults.Count);
        Assert.Contains("跳过", result.StepResults[1].Output);
    }

    [Fact]
    public async Task ExecuteAsync_WithDataPassing_ShouldPassOutputs()
    {
        var chain = new ToolChainBuilder("数据传递链")
            .AddStep("get_current_time")
            .AddStep("get_random_quote")
            .Build();

        var result = await _executor.ExecuteAsync(chain);

        Assert.True(result.Success);
        Assert.Equal(2, result.StepResults.Count);
        Assert.True(result.StepResults[0].Success);
        Assert.True(result.StepResults[1].Success);
    }

    [Fact]
    public async Task ExecuteAsync_FailedStep_ShouldStopChain()
    {
        var chain = new ToolChainBuilder("失败链")
            .AddStep("get_current_time")
            .AddStep("non_existing_tool")
            .AddStep("get_random_quote")
            .Build();

        var result = await _executor.ExecuteAsync(chain);

        Assert.False(result.Success);
        Assert.Equal(2, result.StepResults.Count);
    }

    [Fact]
    public async Task ExecuteAsync_ContinueOnError_ShouldContinueChain()
    {
        var chain = new ToolChainBuilder("容错链")
            .AddStep("get_current_time")
            .AddStep("non_existing_tool", continueOnError: true)
            .AddStep("get_random_quote")
            .Build();

        var result = await _executor.ExecuteAsync(chain);

        Assert.Equal(3, result.StepResults.Count);
        Assert.False(result.StepResults[1].Success);
        Assert.True(result.StepResults[2].Success);
    }

    [Fact]
    public async Task ExecuteStepAsync_ShouldReturnResult()
    {
        var step = new ChainStep
        {
            ToolName = "calculator",
            ArgumentsTemplate = """{"expression": "5 + 5"}"""
        };

        var result = await _executor.ExecuteStepAsync(step, new Dictionary<string, object>());

        Assert.True(result.Success);
        Assert.Equal("10", result.Output);
    }

    [Fact]
    public async Task ExecuteStepAsync_NonExistingTool_ShouldFail()
    {
        var step = new ChainStep
        {
            ToolName = "non_existing"
        };

        var result = await _executor.ExecuteStepAsync(step, new Dictionary<string, object>());

        Assert.False(result.Success);
        Assert.Contains("不存在", result.Error);
    }

    [Fact]
    public void ToolChainBuilder_ShouldBuildCorrectly()
    {
        var chain = new ToolChainBuilder("测试链")
            .WithDescription("测试描述")
            .WithVariable("key", "value")
            .AddStep("tool1")
            .AddStep("tool2")
            .Build();

        Assert.Equal("测试链", chain.Name);
        Assert.Equal("测试描述", chain.Description);
        Assert.Equal(2, chain.Steps.Count);
        Assert.Contains("key", chain.Variables.Keys);
    }
}

public class ToolChainBuilderTests
{
    [Fact]
    public void AddStep_ShouldAddToChain()
    {
        var chain = new ToolChainBuilder("测试")
            .AddStep("tool1")
            .AddStep("tool2")
            .Build();

        Assert.Equal(2, chain.Steps.Count);
        Assert.Equal("tool1", chain.Steps[0].ToolName);
    }

    [Fact]
    public void AddParallelSteps_ShouldSetParallelMode()
    {
        var chain = new ToolChainBuilder("测试")
            .AddParallelSteps("tool1", "tool2", "tool3")
            .Build();

        Assert.Equal(ChainMode.Parallel, chain.Mode);
        Assert.Equal(3, chain.Steps.Count);
    }

    [Fact]
    public void WithVariable_ShouldAddVariable()
    {
        var chain = new ToolChainBuilder("测试")
            .WithVariable("key", "value")
            .Build();

        Assert.Equal("value", chain.Variables["key"]);
    }

    [Fact]
    public void AddStepWithCondition_ShouldSetCondition()
    {
        var chain = new ToolChainBuilder("测试")
            .AddStepWithCondition("tool1", "enable == true")
            .Build();

        Assert.NotNull(chain.Steps[0].Condition);
        Assert.Contains("enable", chain.Steps[0].Condition);
    }
}

public class ToolChainIntegrationTests
{
    [Fact]
    public async Task FullChain_ShouldWorkEndToEnd()
    {
        var registry = new ToolRegistry();
        registry.Register(new GetCurrentTimeTool());
        registry.Register(new CalculatorTool());
        registry.Register(new GetRandomQuoteTool());

        var executor = new ToolChainExecutor(registry);

        var chain = new ToolChainBuilder("完整链")
            .WithDescription("端到端测试")
            .AddStep("calculator", """{"expression": "100 / 4"}""")
            .AddStep("get_random_quote")
            .Build();

        var result = await executor.ExecuteAsync(chain);

        Assert.True(result.Success);
        Assert.Equal("25", result.StepResults[0].Output);
        Assert.NotEmpty(result.StepResults[1].Output);
        Assert.True(result.TotalDuration.TotalMilliseconds > 0);
    }
}
