using DesktopMascot.Agent.Context;
using DesktopMascot.Agent.Engines;
using DesktopMascot.Agent.Memory;
using DesktopMascot.Agent.Models;
using DesktopMascot.Agent.Providers;
using DesktopMascot.Agent.Tools;
using DesktopMascot.App.Services;
using DesktopMascot.Core.Caching;
using DesktopMascot.Core.Character;
using DesktopMascot.Core.Configuration;
using DesktopMascot.Core.Conversation;
using DesktopMascot.Core.ErrorHandling;
using DesktopMascot.Core.Interfaces;
using DesktopMascot.Core.Learning;
using DesktopMascot.Core.Logging;
using DesktopMascot.Core.Memory;
using DesktopMascot.Core.Plugins;
using DesktopMascot.Core.Scheduling;
using DesktopMascot.Core.Security;
using DesktopMascot.Core.Services;
using DesktopMascot.Core.Storage;
using DesktopMascot.Core.Tools;
using DesktopMascot.Core.Workflow;
using Microsoft.Extensions.DependencyInjection;
using ILogger = DesktopMascot.Core.Logging.ILogger;

namespace DesktopMascot.App;

/// <summary>
/// 服务注册扩展方法
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// 注册所有应用服务
    /// </summary>
    public static IServiceCollection AddAppServices(this IServiceCollection services, string? dataDirectory = null)
    {
        var dataDir = dataDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "DesktopMascot");

        // 确保目录存在
        Directory.CreateDirectory(dataDir);
        Directory.CreateDirectory(Path.Combine(dataDir, "config"));
        Directory.CreateDirectory(Path.Combine(dataDir, "db"));
        Directory.CreateDirectory(Path.Combine(dataDir, "logs"));
        Directory.CreateDirectory(Path.Combine(dataDir, "memory"));

        // 配置管理
        services.AddSingleton<IConfigurationManager>(sp =>
            new FileConfigurationManager(Path.Combine(dataDir, "config")));

        // 日志
        services.AddSingleton<ILogStore>(sp =>
            new FileLogStore(Path.Combine(dataDir, "logs")));
        services.AddSingleton<ILogger>(sp =>
        {
            var store = sp.GetRequiredService<ILogStore>();
            return new LogManager(store);
        });

        // 内存
        services.AddSingleton<IMemoryStore>(sp =>
            new FileMemoryStore(Path.Combine(dataDir, "memory")));
        services.AddSingleton<IMemoryConfirmationPrompt, DefaultMemoryConfirmationPrompt>();
        services.AddSingleton<MemoryConfirmationService>();
        services.AddSingleton<MemoryManager>();
        services.AddSingleton<MemoryIntegrationService>();

        // 对话和学习
        services.AddSingleton<ConversationManager>();
        services.AddSingleton<LearningEngine>(sp =>
            new LearningEngine(Path.Combine(dataDir, "learning", "learning_data.json")));

        // 权限
        services.AddSingleton<IPermissionManager, PermissionManager>();
        services.AddSingleton<IAuditLogStore>(sp =>
            new FileAuditLogStore(Path.Combine(dataDir, "logs")));
        services.AddSingleton<IPermissionPrompt, DefaultPermissionPrompt>();
        services.AddSingleton<PermissionConfirmationService>();

        // 任务历史
        services.AddSingleton<ITaskHistoryStore>(sp =>
            new FileTaskHistoryStore(Path.Combine(dataDir, "db")));

        // 工具注册表
        services.AddSingleton<Core.Tools.IToolRegistry, Core.Tools.ToolRegistry>();
        services.AddSingleton<ToolExecutionPipeline>();

        // 工作流
        services.AddSingleton<IWorkflowEngine, WorkflowEngine>();

        // 调度器
        services.AddSingleton<ITaskScheduler, AppTaskScheduler>();

        // 缓存
        services.AddSingleton<MemoryCache>();

        // 插件
        services.AddSingleton<PluginLoader>();
        services.AddSingleton<PluginRegistry>();

        // 错误处理
        services.AddSingleton<ErrorHandler>();

        // Agent 人格
        services.AddSingleton(new AgentPersonality());

        // 语音
        services.AddSingleton<ITextToSpeechProvider>(sp =>
            new EdgeTtsProvider(Path.Combine(dataDir, "tts")));
        services.AddSingleton<ISpeechRecognitionProvider, WhisperSpeechProvider>();
        services.AddSingleton<VoiceConversationMode>();

        // 角色包
        services.AddSingleton<CharacterPackageLoader>();
        services.AddSingleton<PetdexImportConverter>();
        services.AddSingleton<ICharacterManager, CharacterManager>();

        // Agent 层
        services.AddSingleton<IApiKeyStore, FileApiKeyStore>();
        services.AddSingleton<LlmProviderFactory>();
        services.AddSingleton<ITaskEventBus, TaskEventBus>();
        services.AddSingleton<ITaskEventStream, TaskEventStream>();
        services.AddSingleton<IContextProvider, WindowsContextProvider>();
        services.AddSingleton<ILlmProvider>(sp =>
        {
            // 默认使用本地 Ollama；用户配置后可通过 LlmProviderFactory 创建新 Provider
            var httpClient = new HttpClient();
            var config = LlmProviderPresets.Local();
            return new OpenAiCompatibleProvider(httpClient, config);
        });
        services.AddSingleton<Agent.Tools.ToolRegistry>(sp =>
        {
            var registry = new Agent.Tools.ToolRegistry();
            var contextProvider = sp.GetRequiredService<IContextProvider>();
            var llmProvider = sp.GetRequiredService<ILlmProvider>();
            var ttsProvider = sp.GetService<ITextToSpeechProvider>();
            var characterManager = sp.GetService<ICharacterManager>();
            var speechProvider = sp.GetService<ISpeechRecognitionProvider>();
            ToolRegistryInitializer.RegisterBuiltInTools(registry, contextProvider, llmProvider, ttsProvider, characterManager, null, speechProvider);
            return registry;
        });
        services.AddSingleton<ComputerUseOrchestrator>();
        services.AddSingleton<IAgentEngine, ConfiguredAgentEngine>();

        // 任务路由
        services.AddSingleton<ITaskRouter, EnhancedTaskRouter>();

        // 状态机
        services.AddSingleton<MascotStateMachine>();

        // 桥接服务（供 UI 层调用）
        services.AddSingleton<IContextBridgeService, ContextBridgeService>();
        services.AddSingleton<IEventStreamBridge, EventStreamBridge>();
        services.AddSingleton<ISettingsService, SettingsService>();

        // 应用协调器
        services.AddSingleton<ApplicationCoordinator>();

        return services;
    }

    /// <summary>
    /// 注册 UI 服务（由 UI 层调用）
    /// </summary>
    public static IServiceCollection AddUIServices(this IServiceCollection services)
    {
        // UI 服务在 DesktopMascot.UI 层注册
        // 这里只添加必要的桥接服务
        return services;
    }
}
