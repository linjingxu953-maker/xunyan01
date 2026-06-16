using DesktopMascot.Core.Workflow;

namespace DesktopMascot.Agent.Tests;

public class TaskPlannerTests
{
    [Fact]
    public void PlanTask_Summarize_ShouldGenerateCorrectSteps()
    {
        var planner = new TaskPlanner();
        var plan = planner.PlanTask("帮我总结这个页面");

        Assert.Equal(TaskIntent.Summarize, plan.Intent);
        Assert.Equal(3, plan.Steps.Count);
        Assert.Contains(plan.Steps, s => s.ToolName == "browser_context");
        Assert.Contains(plan.Steps, s => s.ToolName == "screen_understand");
    }

    [Fact]
    public void PlanTask_Analyze_ShouldGenerateCorrectSteps()
    {
        var planner = new TaskPlanner();
        var plan = planner.PlanTask("帮我分析这个报错");

        Assert.Equal(TaskIntent.Analyze, plan.Intent);
        Assert.Equal(4, plan.Steps.Count);
        Assert.Contains(plan.Steps, s => s.ToolName == "screen_capture");
    }

    [Fact]
    public void PlanTask_Generate_ShouldGenerateCorrectSteps()
    {
        var planner = new TaskPlanner();
        var plan = planner.PlanTask("帮我生成一个Python脚本");

        Assert.Equal(TaskIntent.Generate, plan.Intent);
        Assert.Equal(4, plan.Steps.Count);
        Assert.Contains(plan.Steps, s => s.RequiresApproval); // write_file 需要确认
    }

    [Fact]
    public void PlanTask_Search_ShouldGenerateCorrectSteps()
    {
        var planner = new TaskPlanner();
        var plan = planner.PlanTask("帮我搜索一下这个概念");

        Assert.Equal(TaskIntent.Search, plan.Intent);
        Assert.Equal(3, plan.Steps.Count);
    }

    [Fact]
    public void PlanTask_Fix_ShouldGenerateCorrectSteps()
    {
        var planner = new TaskPlanner();
        var plan = planner.PlanTask("帮我修复这个bug");

        Assert.Equal(TaskIntent.Fix, plan.Intent);
        Assert.Equal(4, plan.Steps.Count);
        Assert.Contains(plan.Steps, s => s.RequiresApproval); // write_file 需要确认
    }

    [Fact]
    public void PlanTask_Learn_ShouldGenerateCorrectSteps()
    {
        var planner = new TaskPlanner();
        var plan = planner.PlanTask("帮我解释一下量子计算");

        Assert.Equal(TaskIntent.Learn, plan.Intent);
        Assert.Equal(4, plan.Steps.Count);
    }

    [Fact]
    public void PlanTask_General_ShouldGenerateBasicSteps()
    {
        var planner = new TaskPlanner();
        var plan = planner.PlanTask("随便聊聊");

        Assert.Equal(TaskIntent.General, plan.Intent);
        Assert.Equal(2, plan.Steps.Count);
    }

    [Fact]
    public void PlanTask_ShouldHaveEstimatedDuration()
    {
        var planner = new TaskPlanner();
        var plan = planner.PlanTask("帮我做点什么");

        Assert.True(plan.EstimatedDuration > TimeSpan.Zero);
    }

    [Fact]
    public void PlanTask_Generate_ShouldRequireApproval()
    {
        var planner = new TaskPlanner();
        var plan = planner.PlanTask("帮我创建一个文件");

        Assert.True(plan.RequiresApproval);
    }

    [Fact]
    public void PlanTask_Search_ShouldNotRequireApproval()
    {
        var planner = new TaskPlanner();
        var plan = planner.PlanTask("帮我搜索信息");

        Assert.False(plan.RequiresApproval);
    }
}
