using DesktopMascot.Core.Tools;
using DesktopMascot.Agent.Context;
using DesktopMascot.Agent.Providers;

namespace DesktopMascot.Agent.Tools;

/// <summary>
/// 工具注册表初始化器 - 注册所有内置工具
/// </summary>
public static class ToolRegistryInitializer
{
    /// <summary>
    /// 注册所有内置工具
    /// </summary>
    public static void RegisterBuiltInTools(ToolRegistry registry, IContextProvider contextProvider, ILlmProvider? llmProvider = null, ITextToSpeechProvider? ttsProvider = null)
    {
        registry.SetContextProvider(contextProvider);

        // 基础工具
        registry.Register(new GetCurrentTimeTool());
        registry.Register(new CalculatorTool());

        // 上下文工具
        registry.Register(new GetActiveWindowTool(contextProvider));
        registry.Register(new ReadFileTool(contextProvider));

        // 屏幕工具
        registry.Register(new ScreenCaptureTool(contextProvider));
        registry.Register(new BrowserContextTool(contextProvider));
        registry.Register(new ClipboardTool(contextProvider));

        // 目录工具
        registry.Register(new ListDirectoryTool(contextProvider));

        // 文件写入和命令执行工具（需权限确认）
        registry.Register(new WriteFileTool(contextProvider));
        registry.Register(new EditFileTool(contextProvider));
        registry.Register(new RunCommandTool());

        // 文件搜索工具
        registry.Register(new SearchFileTool(contextProvider));

        // 计算机控制工具（需权限确认）
        registry.Register(new ComputerUseTool());

        // 屏幕理解工具（需要 LLM）
        if (llmProvider != null)
        {
            registry.Register(new ScreenUnderstandTool(contextProvider, llmProvider));
        }

        // 文件操作增强工具
        registry.Register(new FileCompareTool());
        registry.Register(new BatchFileProcessorTool());
        registry.Register(new FileVersionTool());

        // 浏览器自动化工具（需权限确认）
        registry.Register(new BrowserAutomationTool());

        // 代码分析和安全工具
        registry.Register(new CodeAnalysisTool());
        registry.Register(new SecurityScanTool());

        // 性能和并发工具
        registry.Register(new PerformanceAnalysisTool());
        registry.Register(new ConcurrencyControlTool());

        // 网络和数据库工具
        registry.Register(new NetworkRequestTool());
        registry.Register(new DatabaseTool());

        // 日历和邮件工具
        registry.Register(new CalendarTool());
        registry.Register(new EmailTool());

        // 通知工具
        registry.Register(new NotificationTool());

        // 云存储同步（可选功能）
        registry.Register(new CloudStorageSyncTool());

        // 文件加密工具（需权限确认）
        registry.Register(new FileEncryptionTool());

        // 图像处理工具
        registry.Register(new ImageProcessingTool());

        // 视频处理工具（需权限确认）
        registry.Register(new VideoProcessingTool());
        registry.Register(new ShortVideoMakerTool());

        // 语音工具（需要 ITextToSpeechProvider）
        if (ttsProvider != null)
        {
            registry.Register(new TextToSpeechTool(ttsProvider));
        }
    }

    /// <summary>
    /// 获取所有内置工具名称
    /// </summary>
    public static List<string> GetBuiltInToolNames()
    {
        return new List<string>
        {
            // 基础
            "get_current_time",
            "calculator",
            // 上下文
            "get_active_window",
            "read_file",
            // 屏幕
            "screen_capture",
            "browser_context",
            "clipboard",
            "screen_understand",
            // 文件
            "list_directory",
            "write_file",
            "edit_file",
            "run_command",
            "search_file",
            // 计算机控制
            "computer_use",
            // 文件操作增强
            "file_compare",
            "batch_file_processor",
            "file_version",
            // 浏览器
            "browser_automation",
            // 分析
            "code_analysis",
            "security_scan",
            // 性能并发
            "performance_analysis",
            "concurrency_control",
            // 网络数据库
            "network_request",
            "database",
            // 日历邮件
            "calendar",
            "email",
            // 通知
            "notification",
            // 云存储
            "cloud_sync",
            // 加密
            "file_encryption",
            // 图像视频
            "image_processing",
            "video_processing",
            "short_video_maker",
            // 语音
            "text_to_speech"
        };
    }
}
