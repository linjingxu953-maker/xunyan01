using DesktopMascot.Core.Enums;

namespace DesktopMascot.Core.Workflow;

/// <summary>
/// 任务规划器 - 将复杂任务拆解为可执行的子任务链
/// </summary>
public class TaskPlanner
{
    /// <summary>
    /// 规划任务步骤
    /// </summary>
    public TaskPlan PlanTask(string userRequest, Dictionary<string, object>? context = null)
    {
        var intent = ClassifyTask(userRequest);
        var steps = GenerateSteps(intent, userRequest, context);

        return new TaskPlan
        {
            UserRequest = userRequest,
            Intent = intent,
            Steps = steps,
            EstimatedDuration = EstimateDuration(steps),
            RequiresApproval = steps.Any(s => s.RequiresApproval)
        };
    }

    private TaskIntent ClassifyTask(string request)
    {
        var lower = request.ToLowerInvariant();

        if (lower.Contains("总结") || lower.Contains("summarize"))
            return TaskIntent.Summarize;
        if (lower.Contains("分析") || lower.Contains("analyze"))
            return TaskIntent.Analyze;
        if (lower.Contains("生成") || lower.Contains("创建") || lower.Contains("create"))
            return TaskIntent.Generate;
        if (lower.Contains("搜索") || lower.Contains("查找") || lower.Contains("search"))
            return TaskIntent.Search;
        if (lower.Contains("修复") || lower.Contains("fix") || lower.Contains("解决"))
            return TaskIntent.Fix;
        if (lower.Contains("学习") || lower.Contains("解释") || lower.Contains("explain"))
            return TaskIntent.Learn;

        return TaskIntent.General;
    }

    private List<TaskStep> GenerateSteps(TaskIntent intent, string request, Dictionary<string, object>? context)
    {
        return intent switch
        {
            TaskIntent.Summarize => GenerateSummarizeSteps(request, context),
            TaskIntent.Analyze => GenerateAnalyzeSteps(request, context),
            TaskIntent.Generate => GenerateGenerateSteps(request, context),
            TaskIntent.Search => GenerateSearchSteps(request, context),
            TaskIntent.Fix => GenerateFixSteps(request, context),
            TaskIntent.Learn => GenerateLearnSteps(request, context),
            _ => GenerateGeneralSteps(request, context)
        };
    }

    private List<TaskStep> GenerateSummarizeSteps(string request, Dictionary<string, object>? context)
    {
        return new List<TaskStep>
        {
            new() { Step = 1, Name = "收集信息", ToolName = "browser_context", Description = "获取当前页面内容" },
            new() { Step = 2, Name = "分析内容", ToolName = "screen_understand", Description = "分析屏幕内容" },
            new() { Step = 3, Name = "生成摘要", ToolName = "none", Description = "基于收集的信息生成摘要" }
        };
    }

    private List<TaskStep> GenerateAnalyzeSteps(string request, Dictionary<string, object>? context)
    {
        return new List<TaskStep>
        {
            new() { Step = 1, Name = "收集上下文", ToolName = "screen_capture", Description = "截取当前屏幕" },
            new() { Step = 2, Name = "识别内容", ToolName = "screen_understand", Description = "识别屏幕内容" },
            new() { Step = 3, Name = "分析问题", ToolName = "none", Description = "分析问题原因" },
            new() { Step = 4, Name = "给出建议", ToolName = "none", Description = "提供解决方案" }
        };
    }

    private List<TaskStep> GenerateGenerateSteps(string request, Dictionary<string, object>? context)
    {
        return new List<TaskStep>
        {
            new() { Step = 1, Name = "理解需求", ToolName = "none", Description = "理解用户需求" },
            new() { Step = 2, Name = "规划结构", ToolName = "none", Description = "规划生成内容的结构" },
            new() { Step = 3, Name = "生成内容", ToolName = "none", Description = "生成所需内容" },
            new() { Step = 4, Name = "保存文件", ToolName = "write_file", Description = "保存到文件", RequiresApproval = true }
        };
    }

    private List<TaskStep> GenerateSearchSteps(string request, Dictionary<string, object>? context)
    {
        return new List<TaskStep>
        {
            new() { Step = 1, Name = "理解搜索意图", ToolName = "none", Description = "理解用户要搜索什么" },
            new() { Step = 2, Name = "执行搜索", ToolName = "browser_context", Description = "在浏览器中搜索" },
            new() { Step = 3, Name = "整理结果", ToolName = "none", Description = "整理搜索结果" }
        };
    }

    private List<TaskStep> GenerateFixSteps(string request, Dictionary<string, object>? context)
    {
        return new List<TaskStep>
        {
            new() { Step = 1, Name = "识别问题", ToolName = "screen_understand", Description = "识别问题内容" },
            new() { Step = 2, Name = "分析原因", ToolName = "none", Description = "分析问题原因" },
            new() { Step = 3, Name = "生成修复方案", ToolName = "none", Description = "生成修复方案" },
            new() { Step = 4, Name = "应用修复", ToolName = "write_file", Description = "应用修复方案", RequiresApproval = true }
        };
    }

    private List<TaskStep> GenerateLearnSteps(string request, Dictionary<string, object>? context)
    {
        return new List<TaskStep>
        {
            new() { Step = 1, Name = "理解问题", ToolName = "none", Description = "理解用户要学习什么" },
            new() { Step = 2, Name = "收集资料", ToolName = "browser_context", Description = "收集相关资料" },
            new() { Step = 3, Name = "解释概念", ToolName = "none", Description = "解释相关概念" },
            new() { Step = 4, Name = "给出示例", ToolName = "none", Description = "给出具体示例" }
        };
    }

    private List<TaskStep> GenerateGeneralSteps(string request, Dictionary<string, object>? context)
    {
        return new List<TaskStep>
        {
            new() { Step = 1, Name = "理解需求", ToolName = "none", Description = "理解用户需求" },
            new() { Step = 2, Name = "执行任务", ToolName = "none", Description = "执行任务" }
        };
    }

    private TimeSpan EstimateDuration(List<TaskStep> steps)
    {
        var baseTime = TimeSpan.FromSeconds(5);
        var stepTime = TimeSpan.FromSeconds(steps.Count * 3);
        return baseTime + stepTime;
    }
}

/// <summary>
/// 任务意图
/// </summary>
public enum TaskIntent
{
    General,
    Summarize,
    Analyze,
    Generate,
    Search,
    Fix,
    Learn
}

/// <summary>
/// 任务计划
/// </summary>
public class TaskPlan
{
    public string UserRequest { get; set; } = string.Empty;
    public TaskIntent Intent { get; set; }
    public List<TaskStep> Steps { get; set; } = new();
    public TimeSpan EstimatedDuration { get; set; }
    public bool RequiresApproval { get; set; }
}

/// <summary>
/// 任务步骤
/// </summary>
public class TaskStep
{
    public int Step { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ToolName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool RequiresApproval { get; set; }
    public Dictionary<string, string> Parameters { get; set; } = new();
}
