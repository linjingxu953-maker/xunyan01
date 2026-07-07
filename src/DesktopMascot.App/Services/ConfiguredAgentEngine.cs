using DesktopMascot.Agent.Engines;
using DesktopMascot.Agent.Memory;
using DesktopMascot.Agent.Models;
using DesktopMascot.Agent.Providers;
using DesktopMascot.Agent.Tools;
using DesktopMascot.Core.Character;
using DesktopMascot.Core.Configuration;
using DesktopMascot.Core.Conversation;
using DesktopMascot.Core.ErrorHandling;
using DesktopMascot.Core.Interfaces;
using DesktopMascot.Core.Learning;
using DesktopMascot.Core.Models;
using DesktopMascot.Core.Security;
using DesktopMascot.Core.Storage;
using DesktopMascot.Core.Tools;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using AgentToolRegistry = DesktopMascot.Agent.Tools.ToolRegistry;

namespace DesktopMascot.App.Services;

/// <summary>
/// 根据用户配置选择内置 Agent 或本机 MiMo Code CLI。
/// </summary>
public sealed class ConfiguredAgentEngine : IAgentEngine
{
    private readonly IConfigurationManager _configurationManager;
    private readonly AgentToolRegistry _toolRegistry;
    private readonly ITaskEventBus _eventBus;
    private readonly ITaskEventStream _eventStream;
    private readonly ILogger<AgentOrchestrator> _logger;
    private readonly ToolExecutionPipeline? _toolPipeline;
    private readonly LlmProviderFactory? _providerFactory;
    private readonly IApiKeyStore? _apiKeyStore;
    private readonly MemoryIntegrationService? _memoryService;
    private readonly ITaskHistoryStore? _historyStore;
    private readonly ConversationManager? _conversationManager;
    private readonly LearningEngine? _learningEngine;
    private readonly IAuditLogStore? _auditLogStore;
    private readonly ErrorHandler? _errorHandler;
    private readonly AgentPersonality? _personality;
    private readonly ICharacterManager? _characterManager;
    private readonly ComputerUseControlService? _computerUseControlService;

    public ConfiguredAgentEngine(
        IConfigurationManager configurationManager,
        AgentToolRegistry toolRegistry,
        ITaskEventBus eventBus,
        ITaskEventStream eventStream,
        ILogger<AgentOrchestrator> logger,
        ToolExecutionPipeline? toolPipeline = null,
        LlmProviderFactory? providerFactory = null,
        IApiKeyStore? apiKeyStore = null,
        MemoryIntegrationService? memoryService = null,
        ITaskHistoryStore? historyStore = null,
        ConversationManager? conversationManager = null,
        LearningEngine? learningEngine = null,
        IAuditLogStore? auditLogStore = null,
        ErrorHandler? errorHandler = null,
        AgentPersonality? personality = null,
        ICharacterManager? characterManager = null,
        ComputerUseControlService? computerUseControlService = null)
    {
        _configurationManager = configurationManager;
        _toolRegistry = toolRegistry;
        _eventBus = eventBus;
        _eventStream = eventStream;
        _logger = logger;
        _toolPipeline = toolPipeline;
        _providerFactory = providerFactory;
        _apiKeyStore = apiKeyStore;
        _memoryService = memoryService;
        _historyStore = historyStore;
        _conversationManager = conversationManager;
        _learningEngine = learningEngine;
        _auditLogStore = auditLogStore;
        _errorHandler = errorHandler;
        _personality = personality;
        _characterManager = characterManager;
        _computerUseControlService = computerUseControlService;
    }

    public async Task<TaskResult> ExecuteAsync(AgentTask task, CancellationToken ct = default)
    {
        var settings = await _configurationManager.GetAppSettingsAsync(ct);
        await MigrateInlineApiKeyAsync(settings, ct);

        if (settings.MimoCodeEnabled)
        {
            var mimoAgent = new MiMoCodeAgent(await BuildMimoCodeConfigAsync(settings, ct), _eventStream);
            return await mimoAgent.ExecuteAsync(task, ct);
        }

        var provider = await BuildProviderAsync(settings, ct);
        _toolRegistry.SetLlmProvider(provider);
        var orchestrator = CreateOrchestrator(provider, settings.MemoryEnabled);
        return await orchestrator.ExecuteAsync(task, ct);
    }

    public async IAsyncEnumerable<string> ExecuteStreamingAsync(AgentTask task, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        var settings = await _configurationManager.GetAppSettingsAsync(ct);
        await MigrateInlineApiKeyAsync(settings, ct);

        if (settings.MimoCodeEnabled)
        {
            var mimoAgent = new MiMoCodeAgent(await BuildMimoCodeConfigAsync(settings, ct), _eventStream);
            await foreach (var chunk in mimoAgent.ExecuteStreamingAsync(task, ct))
            {
                yield return chunk;
            }
            yield break;
        }

        var provider = await BuildProviderAsync(settings, ct);
        _toolRegistry.SetLlmProvider(provider);
        var orchestrator = CreateOrchestrator(provider, settings.MemoryEnabled);
        await foreach (var chunk in orchestrator.ExecuteStreamingAsync(task, ct))
        {
            yield return chunk;
        }
    }

    private AgentOrchestrator CreateOrchestrator(ILlmProvider provider, bool memoryEnabled)
    {
        // 优先从 CharacterManager 获取当前角色人格
        var personality = _personality;
        if (_characterManager?.IsReady == true && _characterManager.Current != null)
        {
            personality = CharacterToPersonalityConverter.Convert(_characterManager.Current);
        }

        var computerUseLogger = new Logger<ComputerUseOrchestrator>(NullLoggerFactory.Instance);
        var computerUseOrchestrator = new ComputerUseOrchestrator(provider, _eventBus, computerUseLogger);
        _computerUseControlService?.Attach(computerUseOrchestrator);
        return new AgentOrchestrator(new AgentOrchestratorOptions
        {
            LlmProvider = provider,
            ToolRegistry = _toolRegistry,
            EventBus = _eventBus,
            Logger = _logger,
            ToolPipeline = _toolPipeline,
            MemoryService = _memoryService,
            MemoryEnabled = memoryEnabled,
            ComputerUseOrchestrator = computerUseOrchestrator,
            HistoryStore = _historyStore,
            ConversationManager = _conversationManager,
            LearningEngine = _learningEngine,
            AuditLogStore = _auditLogStore,
            ErrorHandler = _errorHandler,
            Personality = personality
        });
    }

    private async Task<ILlmProvider> BuildProviderAsync(AppSettings settings, CancellationToken ct)
    {
        var config = new LlmProviderConfig
        {
            ProviderName = NormalizeProviderName(settings.ProviderName),
            Model = string.IsNullOrWhiteSpace(settings.ModelName) ? "gpt-4o-mini" : settings.ModelName.Trim(),
            BaseUrl = NormalizeBaseUrl(settings.ApiEndpoint),
            IsEnabled = true
        };

        if (!string.IsNullOrWhiteSpace(settings.ApiKey))
        {
            config.SetApiKey(settings.ApiKey.Trim());
        }

        if (_providerFactory != null)
        {
            return await _providerFactory.CreateProviderAsync(config, ct);
        }

        return new OpenAiCompatibleProvider(new HttpClient(), config);
    }

    private async Task<MiMoCodeConfig> BuildMimoCodeConfigAsync(AppSettings settings, CancellationToken ct)
    {
        var useAppProvider = settings.MimoCodeModelConfigMode != "MimoLocalConfig";
        var providerName = NormalizeProviderName(settings.ProviderName);
        var apiKey = string.Empty;
        if (useAppProvider)
        {
            apiKey = _apiKeyStore != null
                ? await _apiKeyStore.GetApiKeyAsync(providerName, ct) ?? string.Empty
                : settings.ApiKey.Trim();
        }

        return new MiMoCodeConfig
        {
            ExecutablePath = string.IsNullOrWhiteSpace(settings.MimoCodeExecutablePath)
                ? "mimo"
                : settings.MimoCodeExecutablePath.Trim(),
            WorkingDirectory = string.IsNullOrWhiteSpace(settings.MimoCodeWorkspaceDirectory)
                ? null
                : settings.MimoCodeWorkspaceDirectory.Trim(),
            DefaultModel = useAppProvider ? BuildMimoModel(settings) : string.Empty,
            ProviderName = useAppProvider ? providerName : string.Empty,
            ApiKey = apiKey,
            ApiEndpoint = useAppProvider ? NormalizeBaseUrl(settings.ApiEndpoint) : string.Empty,
            ModelConfigMode = useAppProvider ? "AppProvider" : "MimoLocalConfig",
            PureMode = true
        };
    }

    private async Task MigrateInlineApiKeyAsync(AppSettings settings, CancellationToken ct)
    {
        if (_apiKeyStore == null || string.IsNullOrWhiteSpace(settings.ApiKey))
            return;

        await _apiKeyStore.SetApiKeyAsync(NormalizeProviderName(settings.ProviderName), settings.ApiKey.Trim(), ct);
        settings.ApiKey = string.Empty;
        await _configurationManager.SaveAppSettingsAsync(settings, ct);
    }

    private static string BuildMimoModel(AppSettings settings)
    {
        var model = string.IsNullOrWhiteSpace(settings.ModelName) ? "deepseek-chat" : settings.ModelName.Trim();
        var provider = NormalizeProviderName(settings.ProviderName);

        return provider == "custom" ? model : $"{provider}/{model}";
    }

    private static string NormalizeProviderName(string providerName)
    {
        if (string.IsNullOrWhiteSpace(providerName))
            return "openai";

        return providerName.Trim().ToLowerInvariant() switch
        {
            "moonshot" => "kimi",
            "glm" => "zhipu",
            "qwen" => "tongyi",
            "stepfun" => "stepfun",
            "stepfun ai" => "stepfun",
            "custom" => "custom",
            var value => value
        };
    }

    private static string NormalizeBaseUrl(string endpoint)
    {
        return string.IsNullOrWhiteSpace(endpoint)
            ? "https://api.openai.com/v1"
            : endpoint.Trim().TrimEnd('/');
    }
}
