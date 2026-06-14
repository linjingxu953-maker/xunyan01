using DesktopMascot.Agent.Engines;
using DesktopMascot.Agent.Memory;
using DesktopMascot.Agent.Providers;
using DesktopMascot.Agent.Tools;
using DesktopMascot.Core.Configuration;
using DesktopMascot.Core.Interfaces;
using DesktopMascot.Core.Models;
using DesktopMascot.Core.Storage;
using Microsoft.Extensions.Logging;

namespace DesktopMascot.App.Services;

/// <summary>
/// 根据用户配置选择内置 Agent 或本机 MiMo Code CLI。
/// </summary>
public sealed class ConfiguredAgentEngine : IAgentEngine
{
    private readonly IConfigurationManager _configurationManager;
    private readonly ToolRegistry _toolRegistry;
    private readonly ITaskEventBus _eventBus;
    private readonly ITaskEventStream _eventStream;
    private readonly ILogger<AgentOrchestrator> _logger;
    private readonly MemoryIntegrationService? _memoryService;
    private readonly ITaskHistoryStore? _historyStore;

    public ConfiguredAgentEngine(
        IConfigurationManager configurationManager,
        ToolRegistry toolRegistry,
        ITaskEventBus eventBus,
        ITaskEventStream eventStream,
        ILogger<AgentOrchestrator> logger,
        MemoryIntegrationService? memoryService = null,
        ITaskHistoryStore? historyStore = null)
    {
        _configurationManager = configurationManager;
        _toolRegistry = toolRegistry;
        _eventBus = eventBus;
        _eventStream = eventStream;
        _logger = logger;
        _memoryService = memoryService;
        _historyStore = historyStore;
    }

    public async Task<TaskResult> ExecuteAsync(AgentTask task, CancellationToken ct = default)
    {
        var settings = await _configurationManager.GetAppSettingsAsync(ct);

        if (settings.MimoCodeEnabled)
        {
            var mimoAgent = new MiMoCodeAgent(BuildMimoCodeConfig(settings), _eventStream);
            return await mimoAgent.ExecuteAsync(task, ct);
        }

        var provider = BuildProvider(settings);
        var computerUseOrchestrator = new ComputerUseOrchestrator(provider, _eventBus, _logger);
        var orchestrator = CreateOrchestrator(provider);
        return await orchestrator.ExecuteAsync(task, ct);
    }

    public async IAsyncEnumerable<string> ExecuteStreamingAsync(AgentTask task, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        var settings = await _configurationManager.GetAppSettingsAsync(ct);

        if (settings.MimoCodeEnabled)
        {
            var mimoAgent = new MiMoCodeAgent(BuildMimoCodeConfig(settings), _eventStream);
            await foreach (var chunk in mimoAgent.ExecuteStreamingAsync(task, ct))
            {
                yield return chunk;
            }
            yield break;
        }

        var provider = BuildProvider(settings);
        var orchestrator = CreateOrchestrator(provider);
        await foreach (var chunk in orchestrator.ExecuteStreamingAsync(task, ct))
        {
            yield return chunk;
        }
    }

    private AgentOrchestrator CreateOrchestrator(ILlmProvider provider)
    {
        var computerUseOrchestrator = new ComputerUseOrchestrator(provider, _eventBus, _logger);
        return new AgentOrchestrator(
            provider,
            _toolRegistry,
            _eventBus,
            _logger,
            memoryService: _memoryService,
            computerUseOrchestrator: computerUseOrchestrator,
            historyStore: _historyStore);
    }

    private static ILlmProvider BuildProvider(AppSettings settings)
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

        return new OpenAiCompatibleProvider(new HttpClient(), config);
    }

    private static MiMoCodeConfig BuildMimoCodeConfig(AppSettings settings)
    {
        var useAppProvider = settings.MimoCodeModelConfigMode != "MimoLocalConfig";
        return new MiMoCodeConfig
        {
            ExecutablePath = string.IsNullOrWhiteSpace(settings.MimoCodeExecutablePath)
                ? "mimo"
                : settings.MimoCodeExecutablePath.Trim(),
            WorkingDirectory = string.IsNullOrWhiteSpace(settings.MimoCodeWorkspaceDirectory)
                ? null
                : settings.MimoCodeWorkspaceDirectory.Trim(),
            DefaultModel = useAppProvider ? BuildMimoModel(settings) : string.Empty,
            ProviderName = useAppProvider ? NormalizeProviderName(settings.ProviderName) : string.Empty,
            ApiKey = useAppProvider ? settings.ApiKey.Trim() : string.Empty,
            ApiEndpoint = useAppProvider ? NormalizeBaseUrl(settings.ApiEndpoint) : string.Empty,
            ModelConfigMode = useAppProvider ? "AppProvider" : "MimoLocalConfig",
            PureMode = true
        };
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
