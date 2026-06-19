using DesktopMascot.Core.Enums;

namespace DesktopMascot.UI.ViewModels;

public sealed class ToolLauncherItem
{
    public ToolLauncherItem(
        string id,
        string title,
        string category,
        string description,
        TaskType taskType,
        string toolName,
        string instruction,
        string riskText,
        params string[] keywords)
        : this(id, title, category, description, taskType, toolName, instruction, riskText, ToolLauncherLaunchMode.FillPrompt, keywords)
    {
    }

    public ToolLauncherItem(
        string id,
        string title,
        string category,
        string description,
        TaskType taskType,
        string toolName,
        string instruction,
        string riskText,
        ToolLauncherFormKind formKind,
        params string[] keywords)
        : this(id, title, category, description, taskType, toolName, instruction, riskText, ToolLauncherLaunchMode.FillPrompt, formKind, keywords)
    {
    }

    public ToolLauncherItem(
        string id,
        string title,
        string category,
        string description,
        TaskType taskType,
        string toolName,
        string instruction,
        string riskText,
        ToolLauncherLaunchMode launchMode,
        params string[] keywords)
        : this(id, title, category, description, taskType, toolName, instruction, riskText, launchMode, ToolLauncherFormKind.None, keywords)
    {
    }

    public ToolLauncherItem(
        string id,
        string title,
        string category,
        string description,
        TaskType taskType,
        string toolName,
        string instruction,
        string riskText,
        ToolLauncherLaunchMode launchMode,
        ToolLauncherFormKind formKind,
        params string[] keywords)
    {
        Id = id;
        Title = title;
        Category = category;
        Description = description;
        TaskType = taskType;
        ToolName = toolName;
        Instruction = instruction;
        RiskText = riskText;
        LaunchMode = launchMode;
        FormKind = formKind == ToolLauncherFormKind.None
            ? InferFormKind(id, category, taskType, launchMode)
            : formKind;
        Keywords = keywords;
    }

    public string Id { get; }
    public string Title { get; }
    public string Category { get; }
    public string Description { get; }
    public TaskType TaskType { get; }
    public string ToolName { get; }
    public string Instruction { get; }
    public string RiskText { get; }
    public ToolLauncherLaunchMode LaunchMode { get; }
    public ToolLauncherFormKind FormKind { get; }
    public IReadOnlyList<string> Keywords { get; }

    public string CategoryBadgeText => Category;

    public string LaunchPrompt =>
        $"请使用工具入口 {ToolName}（{Title}）处理任务。{Instruction} 请说明目标、输入材料和期望输出。";

    public bool Matches(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return true;

        var text = query.Trim();
        return Contains(Title, text)
            || Contains(Category, text)
            || Contains(Description, text)
            || Contains(ToolName, text)
            || Keywords.Any(keyword => Contains(keyword, text));
    }

    private static bool Contains(string source, string query) =>
        source.Contains(query, StringComparison.OrdinalIgnoreCase);

    private static ToolLauncherFormKind InferFormKind(
        string id,
        string category,
        TaskType taskType,
        ToolLauncherLaunchMode launchMode)
    {
        if (launchMode != ToolLauncherLaunchMode.FillPrompt)
            return ToolLauncherFormKind.None;

        if (CommandFormIds.Contains(id) || taskType == TaskType.RunCommand)
            return ToolLauncherFormKind.Command;

        if (PathFormIds.Contains(id) || category == "文件")
            return ToolLauncherFormKind.Path;

        if (ContentFormIds.Contains(id) || category is "内容" or "个人")
            return ToolLauncherFormKind.Content;

        return ToolLauncherFormKind.None;
    }

    private static readonly HashSet<string> CommandFormIds = new(StringComparer.OrdinalIgnoreCase)
    {
        "run_command",
        "database",
        "network_request"
    };

    private static readonly HashSet<string> PathFormIds = new(StringComparer.OrdinalIgnoreCase)
    {
        "code_analysis",
        "performance",
        "pdf",
        "image_processing",
        "video_processing",
        "short_video",
        "cloud_sync"
    };

    private static readonly HashSet<string> ContentFormIds = new(StringComparer.OrdinalIgnoreCase)
    {
        "translate",
        "note_generator",
        "paper_writing",
        "course_assist",
        "exam_mode",
        "text_to_speech",
        "calendar",
        "email",
        "notification",
        "task_template",
        "concurrency_control",
        "character_switch",
        "character_market",
        "clipboard",
        "browser_context",
        "screen_capture"
    };
}

public enum ToolLauncherLaunchMode
{
    FillPrompt,
    ScreenSelection,
    ComputerUsePanel
}

public enum ToolLauncherFormKind
{
    None,
    Path,
    Command,
    Content
}
