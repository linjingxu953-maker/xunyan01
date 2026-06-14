using DesktopMascot.Core.Workflow;

namespace DesktopMascot.Core.Workflow;

/// <summary>
/// 工作流模板
/// </summary>
public class WorkflowTemplate
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public List<WorkflowStep> Steps { get; set; } = new();
    public string[] RequiredTools { get; set; } = Array.Empty<string>();
    public string[] Tags { get; set; } = Array.Empty<string>();
}

/// <summary>
/// 工作流模板管理器
/// </summary>
public static class WorkflowTemplates
{
    /// <summary>获取所有内置模板</summary>
    public static List<WorkflowTemplate> GetBuiltInTemplates()
    {
        return new List<WorkflowTemplate>
        {
            CreateSummarizePageTemplate(),
            CreateAnalyzeErrorTemplate(),
            CreateInspectProjectTemplate(),
            CreateCodeReviewTemplate(),
            CreateFileOrganizationTemplate(),
            CreateDataAnalysisTemplate(),
            CreateDocumentationTemplate(),
            CreateBugFixTemplate()
        };
    }

    /// <summary>网页总结模板</summary>
    private static WorkflowTemplate CreateSummarizePageTemplate()
    {
        return new WorkflowTemplate
        {
            Id = "summarize_page",
            Name = "总结网页",
            Description = "读取当前浏览器页面并生成摘要",
            Category = "效率",
            RequiredTools = new[] { "browser_context", "screen_capture" },
            Tags = new[] { "总结", "网页", "摘要", "summarize" },
            Steps = new List<WorkflowStep>
            {
                new() { Name = "获取浏览器上下文", ToolName = "browser_context", Order = 1 },
                new() { Name = "截取屏幕", ToolName = "screen_capture", Order = 2 },
                new() { Name = "分析并总结", ToolName = "analyze_content", Order = 3 }
            }
        };
    }

    /// <summary>报错分析模板</summary>
    private static WorkflowTemplate CreateAnalyzeErrorTemplate()
    {
        return new WorkflowTemplate
        {
            Id = "analyze_error",
            Name = "分析报错",
            Description = "分析当前屏幕或剪贴板中的错误信息",
            Category = "开发",
            RequiredTools = new[] { "screen_capture", "clipboard" },
            Tags = new[] { "报错", "错误", "调试", "error" },
            Steps = new List<WorkflowStep>
            {
                new() { Name = "截取错误屏幕", ToolName = "screen_capture", Order = 1 },
                new() { Name = "读取错误信息", ToolName = "clipboard", Order = 2 },
                new() { Name = "分析错误原因", ToolName = "analyze_error", Order = 3 },
                new() { Name = "生成修复建议", ToolName = "generate_fix", Order = 4 }
            }
        };
    }

    /// <summary>项目诊断模板</summary>
    private static WorkflowTemplate CreateInspectProjectTemplate()
    {
        return new WorkflowTemplate
        {
            Id = "inspect_project",
            Name = "项目诊断",
            Description = "分析项目结构，给出诊断和改进建议",
            Category = "开发",
            RequiredTools = new[] { "list_directory", "read_file" },
            Tags = new[] { "项目", "诊断", "分析", "project" },
            Steps = new List<WorkflowStep>
            {
                new() { Name = "扫描项目结构", ToolName = "list_directory", Order = 1, ArgumentsTemplate = """{"recursive": true}""" },
                new() { Name = "读取配置文件", ToolName = "read_file", Order = 2 },
                new() { Name = "分析项目健康度", ToolName = "analyze_project", Order = 3 },
                new() { Name = "生成诊断报告", ToolName = "generate_report", Order = 4 }
            }
        };
    }

    /// <summary>代码审查模板</summary>
    private static WorkflowTemplate CreateCodeReviewTemplate()
    {
        return new WorkflowTemplate
        {
            Id = "code_review",
            Name = "代码审查",
            Description = "审查代码质量，检查潜在问题",
            Category = "开发",
            RequiredTools = new[] { "read_file", "list_directory" },
            Tags = new[] { "代码", "审查", "质量" },
            Steps = new List<WorkflowStep>
            {
                new() { Name = "读取代码文件", ToolName = "read_file", Order = 1 },
                new() { Name = "分析代码质量", ToolName = "analyze_code", Order = 2 },
                new() { Name = "生成审查报告", ToolName = "generate_report", Order = 3 }
            }
        };
    }

    /// <summary>文件整理模板</summary>
    private static WorkflowTemplate CreateFileOrganizationTemplate()
    {
        return new WorkflowTemplate
        {
            Id = "file_organization",
            Name = "文件整理",
            Description = "整理文件目录，移动、重命名、归档",
            Category = "效率",
            RequiredTools = new[] { "list_directory", "write_file" },
            Tags = new[] { "文件", "整理", "归档" },
            Steps = new List<WorkflowStep>
            {
                new() { Name = "扫描目录", ToolName = "list_directory", Order = 1 },
                new() { Name = "分析文件结构", ToolName = "analyze_files", Order = 2 },
                new() { Name = "生成整理方案", ToolName = "generate_plan", Order = 3, RequiresApproval = true },
                new() { Name = "执行整理", ToolName = "execute_plan", Order = 4, RequiresApproval = true }
            }
        };
    }

    /// <summary>数据分析模板</summary>
    private static WorkflowTemplate CreateDataAnalysisTemplate()
    {
        return new WorkflowTemplate
        {
            Id = "data_analysis",
            Name = "数据分析",
            Description = "分析数据文件，生成统计报告",
            Category = "分析",
            RequiredTools = new[] { "read_file", "write_file" },
            Tags = new[] { "数据", "分析", "统计" },
            Steps = new List<WorkflowStep>
            {
                new() { Name = "读取数据", ToolName = "read_file", Order = 1 },
                new() { Name = "数据清洗", ToolName = "clean_data", Order = 2 },
                new() { Name = "统计分析", ToolName = "analyze_data", Order = 3 },
                new() { Name = "生成报告", ToolName = "generate_report", Order = 4 }
            }
        };
    }

    /// <summary>文档生成模板</summary>
    private static WorkflowTemplate CreateDocumentationTemplate()
    {
        return new WorkflowTemplate
        {
            Id = "documentation",
            Name = "文档生成",
            Description = "为代码生成文档和说明",
            Category = "文档",
            RequiredTools = new[] { "read_file", "list_directory", "write_file" },
            Tags = new[] { "文档", "说明", "注释" },
            Steps = new List<WorkflowStep>
            {
                new() { Name = "扫描项目结构", ToolName = "list_directory", Order = 1 },
                new() { Name = "读取源代码", ToolName = "read_file", Order = 2 },
                new() { Name = "分析代码结构", ToolName = "analyze_code", Order = 3 },
                new() { Name = "生成文档", ToolName = "generate_documentation", Order = 4 }
            }
        };
    }

    /// <summary>Bug修复模板</summary>
    private static WorkflowTemplate CreateBugFixTemplate()
    {
        return new WorkflowTemplate
        {
            Id = "bug_fix",
            Name = "Bug修复",
            Description = "分析和修复代码Bug",
            Category = "开发",
            RequiredTools = new[] { "read_file", "write_file" },
            Tags = new[] { "Bug", "修复", "调试" },
            Steps = new List<WorkflowStep>
            {
                new() { Name = "读取错误信息", ToolName = "read_file", Order = 1 },
                new() { Name = "定位问题代码", ToolName = "find_code", Order = 2 },
                new() { Name = "分析原因", ToolName = "analyze_bug", Order = 3 },
                new() { Name = "生成修复方案", ToolName = "generate_fix", Order = 4, RequiresApproval = true },
                new() { Name = "应用修复", ToolName = "apply_fix", Order = 5, RequiresApproval = true }
            }
        };
    }

    /// <summary>创建网页总结工作流</summary>
    public static WorkflowDefinition SummarizePage(string pageTitle)
    {
        return new WorkflowBuilder("网页总结")
            .WithDescription($"总结网页: {pageTitle}")
            .AddStep("获取当前时间", "get_current_time")
            .AddStep("获取浏览器内容", "browser_context")
            .AddStep("分析并总结", "analyze_content")
            .Build();
    }

    /// <summary>创建报错分析工作流</summary>
    public static WorkflowDefinition AnalyzeError(string errorMessage)
    {
        return new WorkflowBuilder("报错分析")
            .WithDescription($"分析报错: {errorMessage}")
            .AddStep("截取错误屏幕", "screen_capture")
            .AddStep("读取错误信息", "clipboard")
            .AddStep("分析错误原因", "analyze_error")
            .Build();
    }

    /// <summary>创建多步骤任务工作流</summary>
    public static WorkflowDefinition MultiStepTask(string taskName)
    {
        return new WorkflowBuilder(taskName)
            .WithDescription($"执行多步骤任务: {taskName}")
            .AddStep("步骤1: 获取时间", "get_current_time")
            .AddStep("步骤2: 计算", "calculator", """{"expression": "1 + 1"}""")
            .AddStep("步骤3: 获取名言", "get_random_quote")
            .Build();
    }

    /// <summary>根据关键词查找模板</summary>
    public static WorkflowTemplate? FindTemplate(string query)
    {
        var templates = GetBuiltInTemplates();
        var lowerQuery = query.ToLowerInvariant();

        return templates.FirstOrDefault(t => 
            t.Name.Contains(lowerQuery) ||
            t.Description.Contains(lowerQuery) ||
            t.Tags.Any(tag => tag.Contains(lowerQuery)));
    }

    /// <summary>获取所有模板类别</summary>
    public static List<string> GetCategories()
    {
        return GetBuiltInTemplates()
            .Select(t => t.Category)
            .Distinct()
            .ToList();
    }
}
