namespace DesktopMascot.Agent.Providers;

/// <summary>
/// LLM Provider 配置
/// </summary>
public class LlmProviderConfig
{
    /// <summary>Provider 名称</summary>
    public string ProviderName { get; set; } = string.Empty;

    /// <summary>模型名称</summary>
    public string Model { get; set; } = string.Empty;

    /// <summary>API Base URL</summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>API Key（加密存储）</summary>
    public string EncryptedApiKey { get; set; } = string.Empty;

    /// <summary>温度</summary>
    public float Temperature { get; set; } = 0.7f;

    /// <summary>最大 Token 数</summary>
    public int MaxTokens { get; set; } = 4096;

    /// <summary>是否启用</summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>创建时间</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>最后使用时间</summary>
    public DateTime? LastUsedAt { get; set; }

    /// <summary>获取解密的 API Key</summary>
    public string GetApiKey()
    {
        return CredentialProtector.Unprotect(EncryptedApiKey);
    }

    /// <summary>设置 API Key（自动加密）</summary>
    public void SetApiKey(string apiKey)
    {
        EncryptedApiKey = CredentialProtector.Protect(apiKey);
    }
}

/// <summary>
/// 预定义的 Provider 配置
/// </summary>
public static class LlmProviderPresets
{
    /// <summary>OpenAI</summary>
    public static LlmProviderConfig OpenAi(string apiKey, string model = "gpt-4o") => new()
    {
        ProviderName = "openai",
        Model = model,
        BaseUrl = "https://api.openai.com/v1",
        EncryptedApiKey = CredentialProtector.Protect(apiKey)
    };

    /// <summary>DeepSeek</summary>
    public static LlmProviderConfig DeepSeek(string apiKey, string model = "deepseek-chat") => new()
    {
        ProviderName = "deepseek",
        Model = model,
        BaseUrl = "https://api.deepseek.com",
        EncryptedApiKey = CredentialProtector.Protect(apiKey)
    };

    /// <summary>Kimi (Moonshot)</summary>
    public static LlmProviderConfig Kimi(string apiKey, string model = "moonshot-v1-128k") => new()
    {
        ProviderName = "kimi",
        Model = model,
        BaseUrl = "https://api.moonshot.cn",
        EncryptedApiKey = CredentialProtector.Protect(apiKey)
    };

    /// <summary>智谱 AI (GLM)</summary>
    public static LlmProviderConfig Zhipu(string apiKey, string model = "glm-4") => new()
    {
        ProviderName = "zhipu",
        Model = model,
        BaseUrl = "https://open.bigmodel.cn/api/paas/v4",
        EncryptedApiKey = CredentialProtector.Protect(apiKey)
    };

    /// <summary>百川智能</summary>
    public static LlmProviderConfig Baichuan(string apiKey, string model = "Baichuan4") => new()
    {
        ProviderName = "baichuan",
        Model = model,
        BaseUrl = "https://api.baichuan-ai.com",
        EncryptedApiKey = CredentialProtector.Protect(apiKey)
    };

    /// <summary>阿里通义 (Qwen)</summary>
    public static LlmProviderConfig Tongyi(string apiKey, string model = "qwen-max") => new()
    {
        ProviderName = "tongyi",
        Model = model,
        BaseUrl = "https://dashscope.aliyuncs.com/compatible-mode",
        EncryptedApiKey = CredentialProtector.Protect(apiKey)
    };

    /// <summary>字节豆包</summary>
    public static LlmProviderConfig Doubao(string apiKey, string model = "doubao-pro") => new()
    {
        ProviderName = "doubao",
        Model = model,
        BaseUrl = "https://ark.cn-beijing.volces.com/api/v3",
        EncryptedApiKey = CredentialProtector.Protect(apiKey)
    };

    /// <summary>本地模型 (Ollama)</summary>
    public static LlmProviderConfig Local(string model = "llama3", string baseUrl = "http://localhost:11434") => new()
    {
        ProviderName = "local",
        Model = model,
        BaseUrl = baseUrl,
        EncryptedApiKey = "" // 本地模型不需要 Key
    };
}
