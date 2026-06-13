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
    public static void RegisterBuiltInTools(ToolRegistry registry, IContextProvider contextProvider, ILlmProvider? llmProvider = null)
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

        // 屏幕理解工具（需要 LLM）
        if (llmProvider != null)
        {
            registry.Register(new ScreenUnderstandTool(contextProvider, llmProvider));
        }
    }

    /// <summary>
    /// 获取所有内置工具名称
    /// </summary>
    public static List<string> GetBuiltInToolNames()
    {
        return new List<string>
        {
            "get_current_time",
            "calculator",
            "get_active_window",
            "read_file",
            "screen_capture",
            "browser_context",
            "clipboard",
            "screen_understand"
        };
    }
}
