namespace DesktopMascot.Agent.Providers;

/// <summary>
/// LLM Provider 工厂 - 根据配置创建 Provider 实例
/// </summary>
public class LlmProviderFactory
{
    private readonly IApiKeyStore _apiKeyStore;

    public LlmProviderFactory(IApiKeyStore apiKeyStore)
    {
        _apiKeyStore = apiKeyStore;
    }

    /// <summary>
    /// 创建 Provider 实例
    /// </summary>
    public async Task<ILlmProvider> CreateProviderAsync(
        LlmProviderConfig config,
        CancellationToken ct = default)
    {
        var httpClient = new HttpClient();

        // 如果没有 API Key，尝试从存储中获取
        if (string.IsNullOrEmpty(config.EncryptedApiKey) && config.ProviderName != "local")
        {
            var apiKey = await _apiKeyStore.GetApiKeyAsync(config.ProviderName, ct);
            if (apiKey != null)
            {
                config.SetApiKey(apiKey);
            }
        }

        return new OpenAiCompatibleProvider(httpClient, config);
    }

    /// <summary>
    /// 根据 Provider 名称创建 Provider
    /// </summary>
    public async Task<ILlmProvider> CreateProviderByNameAsync(
        string providerName,
        string apiKey,
        string? model = null,
        CancellationToken ct = default)
    {
        var config = providerName.ToLower() switch
        {
            "openai" => LlmProviderPresets.OpenAi(apiKey, model ?? "gpt-4o"),
            "deepseek" => LlmProviderPresets.DeepSeek(apiKey, model ?? "deepseek-chat"),
            "kimi" or "moonshot" => LlmProviderPresets.Kimi(apiKey, model ?? "moonshot-v1-128k"),
            "zhipu" or "glm" => LlmProviderPresets.Zhipu(apiKey, model ?? "glm-4"),
            "baichuan" => LlmProviderPresets.Baichuan(apiKey, model ?? "Baichuan4"),
            "tongyi" or "qwen" => LlmProviderPresets.Tongyi(apiKey, model ?? "qwen-max"),
            "doubao" => LlmProviderPresets.Doubao(apiKey, model ?? "doubao-pro"),
            "local" => LlmProviderPresets.Local(model ?? "llama3"),
            _ => throw new ArgumentException($"未知的 Provider: {providerName}")
        };

        // 保存 API Key
        if (config.ProviderName != "local" && !string.IsNullOrEmpty(apiKey))
        {
            await _apiKeyStore.SetApiKeyAsync(config.ProviderName, apiKey, ct);
        }

        return await CreateProviderAsync(config, ct);
    }

    /// <summary>
    /// 获取所有支持的 Provider 名称
    /// </summary>
    public static List<string> GetSupportedProviders()
    {
        return new List<string>
        {
            "openai",
            "deepseek",
            "kimi",
            "moonshot",
            "zhipu",
            "glm",
            "baichuan",
            "tongyi",
            "qwen",
            "doubao",
            "local"
        };
    }
}
