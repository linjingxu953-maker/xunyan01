namespace DesktopMascot.Core.Workflow;

/// <summary>
/// 工作流构建器
/// </summary>
public class WorkflowBuilder
{
    private readonly WorkflowDefinition _definition = new();
    private int _stepOrder = 0;

    public WorkflowBuilder(string name)
    {
        _definition.Name = name;
    }

    public WorkflowBuilder WithDescription(string description)
    {
        _definition.Description = description;
        return this;
    }

    public WorkflowBuilder WithVariable(string key, object value)
    {
        _definition.Variables[key] = value;
        return this;
    }

    public WorkflowBuilder AddStep(string name, string toolName, string arguments = "{}", bool requiresApproval = false)
    {
        _definition.Steps.Add(new WorkflowStep
        {
            Id = Guid.NewGuid().ToString("N"),
            Name = name,
            ToolName = toolName,
            ArgumentsTemplate = arguments,
            RequiresApproval = requiresApproval,
            Order = _stepOrder++
        });
        return this;
    }

    public WorkflowBuilder AddStepWithDependency(string name, string toolName, string dependsOnStepId, string arguments = "{}")
    {
        _definition.Steps.Add(new WorkflowStep
        {
            Id = Guid.NewGuid().ToString("N"),
            Name = name,
            ToolName = toolName,
            ArgumentsTemplate = arguments,
            DependsOn = new List<string> { dependsOnStepId },
            Order = _stepOrder++
        });
        return this;
    }

    public WorkflowDefinition Build()
    {
        return _definition;
    }
}

/// <summary>
/// 工作流模板（简单版）
/// </summary>
public static class SimpleWorkflowTemplates
{
    /// <summary>
    /// 创建网页总结工作流
    /// </summary>
    public static WorkflowDefinition SummarizePage(string pageTitle)
    {
        return new WorkflowBuilder("网页总结")
            .WithDescription($"总结网页: {pageTitle}")
            .AddStep("获取当前时间", "get_current_time")
            .AddStep("获取天气", "get_weather", """{"city": "北京"}""")
            .Build();
    }

    /// <summary>
    /// 创建报错分析工作流
    /// </summary>
    public static WorkflowDefinition AnalyzeError(string errorMessage)
    {
        return new WorkflowBuilder("报错分析")
            .WithDescription($"分析报错: {errorMessage}")
            .AddStep("获取当前时间", "get_current_time")
            .AddStep("获取随机名言", "get_random_quote")
            .Build();
    }

    /// <summary>
    /// 创建多步骤任务工作流
    /// </summary>
    public static WorkflowDefinition MultiStepTask(string taskName)
    {
        return new WorkflowBuilder(taskName)
            .WithDescription($"执行多步骤任务: {taskName}")
            .AddStep("步骤1: 获取时间", "get_current_time")
            .AddStep("步骤2: 计算", "calculator", """{"expression": "1 + 1"}""")
            .AddStep("步骤3: 获取名言", "get_random_quote")
            .Build();
    }
}
