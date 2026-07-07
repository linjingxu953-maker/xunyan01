using DesktopMascot.Agent.Context;
using DesktopMascot.Agent.Models;
using DesktopMascot.Agent.Providers;
using DesktopMascot.Core.Character;

namespace DesktopMascot.Agent.Tools;

/// <summary>
/// Registers all built-in tools exposed to the Agent and ToolLauncher.
/// </summary>
public static class ToolRegistryInitializer
{
    public static void RegisterBuiltInTools(
        ToolRegistry registry,
        IContextProvider contextProvider,
        ILlmProvider? llmProvider = null,
        ITextToSpeechProvider? ttsProvider = null,
        ICharacterManager? characterManager = null,
        Action<AgentPersonality>? onPersonalityChanged = null,
        ISpeechRecognitionProvider? speechProvider = null,
        ICharacterMarketStore? marketStore = null)
    {
        registry.SetContextProvider(contextProvider);
        if (llmProvider != null)
        {
            registry.SetLlmProvider(llmProvider);
        }

        registry.Register(new GetCurrentTimeTool());
        registry.Register(new CalculatorTool());

        registry.Register(new GetActiveWindowTool(contextProvider));
        registry.Register(new ReadFileTool(contextProvider));
        registry.Register(new ScreenCaptureTool(contextProvider));
        registry.Register(new BrowserContextTool(contextProvider));
        registry.Register(new ClipboardTool(contextProvider));
        registry.Register(new ListDirectoryTool(contextProvider));

        registry.Register(new WriteFileTool(contextProvider));
        registry.Register(new EditFileTool(contextProvider));
        registry.Register(new RunCommandTool());
        registry.Register(new SearchFileTool(contextProvider));

        registry.Register(new ComputerUseTool());
        registry.Register(new ScreenUnderstandTool(contextProvider, () => registry.GetLlmProvider() ?? llmProvider));

        registry.Register(new FileOrganizerTool());
        registry.Register(new CompressionTool());
        registry.Register(new TaskTemplateTool());
        registry.Register(new FileCompareTool());
        registry.Register(new BatchFileProcessorTool());
        registry.Register(new FileVersionTool());

        registry.Register(new BrowserAutomationTool());
        registry.Register(new CodeAnalysisTool());
        registry.Register(new SecurityScanTool());
        registry.Register(new PerformanceAnalysisTool());
        registry.Register(new ConcurrencyControlTool());

        registry.Register(new NetworkRequestTool());
        registry.Register(new DatabaseTool());

        RegisterCompositeTools(registry);

        registry.Register(new CalendarTool());
        registry.Register(new EmailTool());
        registry.Register(new NotificationTool());
        registry.Register(new CloudStorageSyncTool());
        registry.Register(new FileEncryptionTool());
        registry.Register(new ImageProcessingTool());
        registry.Register(new VideoProcessingTool());
        registry.Register(new ShortVideoMakerTool());

        if (ttsProvider != null)
        {
            registry.Register(new TextToSpeechTool(ttsProvider));
        }

        if (characterManager != null)
        {
            registry.Register(new CharacterSwitchTool(characterManager, onPersonalityChanged));
        }

        if (speechProvider != null)
        {
            registry.Register(new SpeechRecognitionTool(speechProvider));
        }

        if (marketStore != null && characterManager != null)
        {
            registry.Register(new CharacterMarketTool(marketStore, characterManager));
        }

        var screenTool = registry.GetTool("screen_understand");
        var computerTool = registry.GetTool("computer_use");
        var browserTool = registry.GetTool("browser_context");
        if (screenTool != null && computerTool != null)
        {
            registry.Register(new CourseAssistTool(screenTool, computerTool));
            if (browserTool != null)
            {
                registry.Register(new ExamModeTool(screenTool, computerTool, browserTool));
            }
        }

        var goalEngine = new DesktopMascot.Core.Tools.GoalEngine();
        registry.Register(new GoalTool(goalEngine));
    }

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
            "screen_understand",
            "list_directory",
            "write_file",
            "edit_file",
            "run_command",
            "search_file",
            "file_organizer",
            "compression",
            "task_template",
            "computer_use",
            "file_compare",
            "batch_file_processor",
            "file_version",
            "browser_automation",
            "code_analysis",
            "security_scan",
            "performance_analysis",
            "concurrency_control",
            "network_request",
            "database",
            "translate",
            "ocr",
            "pdf_tool",
            "note_generator",
            "paper_writing",
            "calendar",
            "email",
            "notification",
            "cloud_sync",
            "file_encryption",
            "image_processing",
            "video_processing",
            "short_video_maker",
            "text_to_speech",
            "speech_recognition",
            "character_switch",
            "character_market",
            "course_assist",
            "exam_mode",
            "goal"
        };
    }

    private static void RegisterCompositeTools(ToolRegistry registry)
    {
        var screenUnderstand = registry.GetTool("screen_understand");
        var networkRequest = registry.GetTool("network_request");
        var clipboard = registry.GetTool("clipboard");
        var browserContext = registry.GetTool("browser_context");

        if (screenUnderstand != null)
        {
            registry.Register(new OcrTool(screenUnderstand));
        }

        var ocr = registry.GetTool("ocr");
        if (ocr != null)
        {
            registry.Register(new PdfTool(ocr));
        }

        if (networkRequest != null && clipboard != null)
        {
            registry.Register(new TranslateTool(networkRequest, clipboard));
        }

        if (browserContext != null && screenUnderstand != null)
        {
            registry.Register(new NoteGeneratorTool(browserContext, screenUnderstand));
        }

        var noteGenerator = registry.GetTool("note_generator");
        if (noteGenerator != null)
        {
            registry.Register(new PaperWritingTool(noteGenerator));
        }
    }
}
