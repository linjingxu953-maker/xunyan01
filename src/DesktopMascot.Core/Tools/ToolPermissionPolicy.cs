using DesktopMascot.Core.Enums;
using DesktopMascot.Core.Security;

namespace DesktopMascot.Core.Tools;

/// <summary>
/// Central permission policy for tools whose metadata is incomplete.
/// </summary>
public static class ToolPermissionPolicy
{
    private static readonly HashSet<string> L1Tools = new(StringComparer.OrdinalIgnoreCase)
    {
        "get_active_window",
        "clipboard"
    };

    private static readonly HashSet<string> L2Tools = new(StringComparer.OrdinalIgnoreCase)
    {
        "screen_capture",
        "browser_context",
        "screen_understand",
        "ocr",
        "course_assist",
        "exam_mode"
    };

    private static readonly HashSet<string> L3Tools = new(StringComparer.OrdinalIgnoreCase)
    {
        "read_file",
        "list_directory",
        "search_file",
        "file_compare",
        "code_analysis",
        "security_scan",
        "performance_analysis",
        "pdf_tool",
        "note_generator",
        "network_request"
    };

    private static readonly HashSet<string> L4Tools = new(StringComparer.OrdinalIgnoreCase)
    {
        "write_file",
        "edit_file",
        "file_organizer",
        "compression",
        "batch_file_processor",
        "file_version",
        "database",
        "cloud_sync",
        "file_encryption",
        "image_processing",
        "video_processing",
        "short_video_maker",
        "task_template",
        "paper_writing"
    };

    private static readonly HashSet<string> L5Tools = new(StringComparer.OrdinalIgnoreCase)
    {
        "run_command",
        "computer_use",
        "browser_automation"
    };

    public static PermissionLevel ResolveRequiredPermission(ITool tool)
    {
        var declared = tool.Definition.RequiredPermission;
        if (declared > PermissionLevel.L0_Chat)
            return declared;

        var name = tool.Name;
        if (L5Tools.Contains(name))
            return PermissionLevel.L5_CommandExec;
        if (L4Tools.Contains(name))
            return PermissionLevel.L4_FileWrite;
        if (L3Tools.Contains(name))
            return PermissionLevel.L3_FileRead;
        if (L2Tools.Contains(name))
            return PermissionLevel.L2_ScreenBrowser;
        if (L1Tools.Contains(name))
            return PermissionLevel.L1_WindowTitle;

        return tool.RequiresConfirmation
            ? PermissionLevel.L4_FileWrite
            : PermissionLevel.L0_Chat;
    }

    public static PromptPermissionType ResolvePromptPermissionType(ITool tool, PermissionLevel level)
    {
        var name = tool.Name;

        if (name.Contains("screen", StringComparison.OrdinalIgnoreCase)
            || string.Equals(name, "ocr", StringComparison.OrdinalIgnoreCase))
        {
            return PromptPermissionType.ScreenCapture;
        }

        if (string.Equals(name, "browser_context", StringComparison.OrdinalIgnoreCase)
            || string.Equals(name, "browser_automation", StringComparison.OrdinalIgnoreCase))
        {
            return PromptPermissionType.BrowserRead;
        }

        if (string.Equals(name, "clipboard", StringComparison.OrdinalIgnoreCase))
        {
            return PromptPermissionType.SelectedTextRead;
        }

        if (string.Equals(name, "network_request", StringComparison.OrdinalIgnoreCase))
        {
            return PromptPermissionType.ApiCall;
        }

        if (string.Equals(name, "run_command", StringComparison.OrdinalIgnoreCase)
            || string.Equals(name, "computer_use", StringComparison.OrdinalIgnoreCase))
        {
            return PromptPermissionType.CommandExecute;
        }

        return level switch
        {
            PermissionLevel.L1_WindowTitle => PromptPermissionType.WindowRead,
            PermissionLevel.L2_ScreenBrowser => PromptPermissionType.ScreenCapture,
            PermissionLevel.L3_FileRead => PromptPermissionType.FileRead,
            PermissionLevel.L4_FileWrite => PromptPermissionType.FileWrite,
            PermissionLevel.L5_CommandExec => PromptPermissionType.CommandExecute,
            PermissionLevel.L6_Forbidden => PromptPermissionType.CommandExecute,
            _ => PromptPermissionType.FileRead
        };
    }

    public static PromptRiskLevel ResolveRiskLevel(PermissionLevel level)
    {
        return level switch
        {
            PermissionLevel.L1_WindowTitle => PromptRiskLevel.Low,
            PermissionLevel.L2_ScreenBrowser => PromptRiskLevel.Low,
            PermissionLevel.L3_FileRead => PromptRiskLevel.Low,
            PermissionLevel.L4_FileWrite => PromptRiskLevel.Medium,
            PermissionLevel.L5_CommandExec => PromptRiskLevel.High,
            PermissionLevel.L6_Forbidden => PromptRiskLevel.High,
            _ => PromptRiskLevel.Low
        };
    }
}
