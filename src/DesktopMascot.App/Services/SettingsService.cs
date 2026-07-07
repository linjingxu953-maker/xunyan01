using DesktopMascot.Agent.Models;
using DesktopMascot.Agent.Providers;
using DesktopMascot.Core.Configuration;
using DesktopMascot.Core.Services;

namespace DesktopMascot.App.Services;

/// <summary>
/// 设置服务实现
/// </summary>
public class SettingsService : ISettingsService
{
    private readonly IConfigurationManager _configManager;
    private readonly IApiKeyStore _apiKeyStore;
    private readonly LlmProviderFactory _providerFactory;

    public SettingsService(
        IConfigurationManager configManager,
        IApiKeyStore apiKeyStore,
        LlmProviderFactory providerFactory)
    {
        _configManager = configManager;
        _apiKeyStore = apiKeyStore;
        _providerFactory = providerFactory;
    }

    public async Task<AppSettings> GetSettingsAsync(CancellationToken ct = default)
    {
        return await _configManager.GetAppSettingsAsync(ct);
    }

    public async Task UpdateSettingsAsync(AppSettings settings, CancellationToken ct = default)
    {
        if (!string.IsNullOrWhiteSpace(settings.ApiKey))
        {
            await _apiKeyStore.SetApiKeyAsync(settings.ProviderName, settings.ApiKey.Trim(), ct);
            settings.ApiKey = string.Empty;
        }

        await _configManager.SaveAppSettingsAsync(settings, ct);
    }

    public async Task<string?> GetApiKeyAsync(string provider, CancellationToken ct = default)
    {
        return await _apiKeyStore.GetApiKeyAsync(NormalizeProviderName(provider), ct);
    }

    public async Task SetApiKeyAsync(string provider, string apiKey, CancellationToken ct = default)
    {
        await _apiKeyStore.SetApiKeyAsync(NormalizeProviderName(provider), apiKey, ct);
    }

    public async Task RemoveApiKeyAsync(string provider, CancellationToken ct = default)
    {
        await _apiKeyStore.RemoveApiKeyAsync(NormalizeProviderName(provider), ct);
    }

    public async Task<List<string>> GetConfiguredProvidersAsync(CancellationToken ct = default)
    {
        return await _apiKeyStore.GetConfiguredProvidersAsync(ct);
    }

    public async Task<ApiTestResult> TestApiKeyAsync(string provider, string apiKey, CancellationToken ct = default)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            var llmProvider = await _providerFactory.CreateProviderByNameAsync(
                provider, apiKey, null, ct);

            var testMessage = new LlmMessage
            {
                Role = "user",
                Content = "Hello, this is a test message."
            };

            var response = await llmProvider.ChatAsync(
                new List<LlmMessage> { testMessage },
                null,
                ct);

            stopwatch.Stop();

            return new ApiTestResult
            {
                Success = response.Success,
                Message = response.Success ? "连接成功" : response.Error,
                Latency = stopwatch.Elapsed,
                ModelInfo = response.Success ? $"Provider: {provider}" : null
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return new ApiTestResult
            {
                Success = false,
                Message = $"连接失败: {ex.Message}",
                Latency = stopwatch.Elapsed
            };
        }
    }

    private static string NormalizeProviderName(string provider)
    {
        if (string.IsNullOrWhiteSpace(provider))
            return "openai";

        return provider.Trim().ToLowerInvariant() switch
        {
            "moonshot" => "kimi",
            "glm" => "zhipu",
            "qwen" => "tongyi",
            "stepfun ai" => "stepfun",
            var value => value
        };
    }
}
