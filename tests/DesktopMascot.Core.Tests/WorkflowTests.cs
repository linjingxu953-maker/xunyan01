using DesktopMascot.Core.Tools;
using DesktopMascot.Core.Workflow;

namespace DesktopMascot.Core.Tests;

public class WorkflowEngineTests
{
    private readonly ToolRegistry _toolRegistry;
    private readonly WorkflowEngine _engine;

    public WorkflowEngineTests()
    {
        _toolRegistry = new ToolRegistry();
        _toolRegistry.Register(new GetCurrentTimeTool());
        _toolRegistry.Register(new CalculatorTool());
        _toolRegistry.Register(new GetRandomQuoteTool());
        _engine = new WorkflowEngine(_toolRegistry);
    }

    [Fact]
    public void CreateWorkflow_ShouldCreateInstance()
    {
        var definition = new WorkflowBuilder("测试工作流")
            .AddStep("步骤1", "get_current_time")
            .Build();

        var instance = _engine.CreateWorkflow(definition);

        Assert.NotNull(instance);
        Assert.Equal("测试工作流", instance.Name);
        Assert.Equal(WorkflowStatus.Pending, instance.Status);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldCompleteWorkflow()
    {
        var definition = new WorkflowBuilder("测试工作流")
            .AddStep("获取时间", "get_current_time")
            .Build();

        var instance = _engine.CreateWorkflow(definition);
        var result = await _engine.ExecuteAsync(instance);

        Assert.Equal(WorkflowStatus.Completed, result.Status);
        Assert.NotNull(result.CompletedAt);
    }

    [Fact]
    public async Task ExecuteAsync_WithMultipleSteps_ShouldCompleteAll()
    {
        var definition = new WorkflowBuilder("多步骤工作流")
            .AddStep("获取时间", "get_current_time")
            .AddStep("计算", "calculator", """{"expression": "2 + 2"}""")
            .Build();

        var instance = _engine.CreateWorkflow(definition);
        var result = await _engine.ExecuteAsync(instance);

        Assert.Equal(WorkflowStatus.Completed, result.Status);
        Assert.All(result.Steps, s => Assert.Equal(StepStatus.Completed, s.Status));
    }

    [Fact]
    public async Task ExecuteAsync_FailedStep_ShouldFailWorkflow()
    {
        var definition = new WorkflowBuilder("失败工作流")
            .AddStep("正常步骤", "get_current_time")
            .AddStep("失败步骤", "non_existing_tool")
            .Build();

        var instance = _engine.CreateWorkflow(definition);
        var result = await _engine.ExecuteAsync(instance);

        Assert.Equal(WorkflowStatus.Failed, result.Status);
        Assert.Contains("不存在", result.ErrorMessage);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldRaiseEvents()
    {
        var definition = new WorkflowBuilder("事件工作流")
            .AddStep("获取时间", "get_current_time")
            .Build();

        var events = new List<WorkflowEvent>();
        _engine.WorkflowEventOccurred += e => events.Add(e);

        var instance = _engine.CreateWorkflow(definition);
        await _engine.ExecuteAsync(instance);

        Assert.Contains(events, e => e.EventType == "started");
        Assert.Contains(events, e => e.EventType == "completed");
    }

    [Fact]
    public async Task CancelAsync_ShouldCancelWorkflow()
    {
        var definition = new WorkflowBuilder("取消工作流")
            .AddStep("步骤1", "get_current_time")
            .Build();

        var instance = _engine.CreateWorkflow(definition);
        _ = _engine.ExecuteAsync(instance);

        await _engine.CancelAsync(instance.Id);
        var result = _engine.GetWorkflow(instance.Id);

        Assert.Equal(WorkflowStatus.Cancelled, result?.Status);
    }

    [Fact]
    public void GetWorkflow_ShouldReturnWorkflow()
    {
        var definition = new WorkflowBuilder("查询工作流")
            .AddStep("步骤1", "get_current_time")
            .Build();

        var instance = _engine.CreateWorkflow(definition);
        var result = _engine.GetWorkflow(instance.Id);

        Assert.NotNull(result);
        Assert.Equal(instance.Id, result!.Id);
    }

    [Fact]
    public void GetWorkflow_NonExisting_ShouldReturnNull()
    {
        var result = _engine.GetWorkflow("non_existing");

        Assert.Null(result);
    }
}

public class WorkflowBuilderTests
{
    [Fact]
    public void Build_ShouldCreateDefinition()
    {
        var definition = new WorkflowBuilder("测试")
            .WithDescription("描述")
            .AddStep("步骤1", "tool1")
            .Build();

        Assert.Equal("测试", definition.Name);
        Assert.Equal("描述", definition.Description);
        Assert.Single(definition.Steps);
    }

    [Fact]
    public void AddStep_ShouldIncrementOrder()
    {
        var definition = new WorkflowBuilder("测试")
            .AddStep("步骤1", "tool1")
            .AddStep("步骤2", "tool2")
            .AddStep("步骤3", "tool3")
            .Build();

        Assert.Equal(3, definition.Steps.Count);
        Assert.Equal(0, definition.Steps[0].Order);
        Assert.Equal(1, definition.Steps[1].Order);
        Assert.Equal(2, definition.Steps[2].Order);
    }

    [Fact]
    public void WithVariable_ShouldAddVariable()
    {
        var definition = new WorkflowBuilder("测试")
            .WithVariable("key1", "value1")
            .Build();

        Assert.Contains("key1", definition.Variables.Keys);
        Assert.Equal("value1", definition.Variables["key1"]);
    }
}

public class WorkflowTemplatesTests
{
    [Fact]
    public void SummarizePage_ShouldCreateWorkflow()
    {
        var definition = WorkflowTemplates.SummarizePage("测试页面");

        Assert.Equal("网页总结", definition.Name);
        Assert.Contains("测试页面", definition.Description);
        Assert.NotEmpty(definition.Steps);
    }

    [Fact]
    public void AnalyzeError_ShouldCreateWorkflow()
    {
        var definition = WorkflowTemplates.AnalyzeError("测试错误");

        Assert.Equal("报错分析", definition.Name);
        Assert.Contains("测试错误", definition.Description);
    }

    [Fact]
    public void MultiStepTask_ShouldCreateWorkflow()
    {
        var definition = WorkflowTemplates.MultiStepTask("测试任务");

        Assert.Equal("测试任务", definition.Name);
        Assert.Equal(3, definition.Steps.Count);
    }
}
