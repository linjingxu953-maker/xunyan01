# API 接入设计

更新日期：2026-06-12

## 1. 设计原则

1. **用户自行配置**：不内置任何 API Key，用户需自行申请并配置
2. **多 Provider 支持**：支持 OpenAI、Anthropic、本地模型等多种 Provider
3. **安全存储**：API Key 使用 Windows DPAPI 加密存储
4. **灵活切换**：用户可随时切换 Provider 和模型

## 2. 支持的 Provider

### 2.1 国际 Provider

| Provider | 模型示例 | API 格式 |
|----------|----------|----------|
| OpenAI | GPT-4o, GPT-4, GPT-3.5-turbo | OpenAI API |
| Anthropic | Claude 3.5 Sonnet, Claude 3 Opus | Anthropic API |

### 2.2 国产 Provider

| Provider | 模型示例 | API 格式 | Base URL |
|----------|----------|----------|----------|
| DeepSeek | DeepSeek-V3, DeepSeek-Chat, DeepSeek-Coder | OpenAI 兼容 | https://api.deepseek.com |
| Kimi (Moonshot) | moonshot-v1-128k, moonshot-v1-32k | OpenAI 兼容 | https://api.moonshot.cn |
| 智谱 AI | GLM-4, GLM-4-Flash | OpenAI 兼容 | https://open.bigmodel.cn/api/paas/v4 |
| 百川智能 | Baichuan4, Baichuan3-Turbo | OpenAI 兼容 | https://api.baichuan-ai.com |
| 讯飞星火 | Spark Max, Spark Pro | OpenAI 兼容 | https://spark-api-open.xf-yun.com |
| 阿里通义 | Qwen-Max, Qwen-Plus, Qwen-Turbo | OpenAI 兼容 | https://dashscope.aliyuncs.com/compatible-mode |
| 字节豆包 | Doubao-pro, Doubao-lite | OpenAI 兼容 | https://ark.cn-beijing.volces.com/api/v3 |
| 零一万物 | Yi-Lightning, Yi-Large | OpenAI 兼容 | https://api.lingyiwanwu.com |
| MiniMax | abab6.5, abab5.5 | OpenAI 兼容 | https://api.minimax.chat |
| 阶跃星辰 | Step-1V, Step-2 | OpenAI 兼容 | https://api.stepfun.com |

### 2.3 本地模型

| Provider | 模型示例 | API 格式 |
|----------|----------|----------|
| Ollama | llama3, qwen2, deepseek-coder | OpenAI 兼容 |
| LM Studio | 任意本地模型 | OpenAI 兼容 |
| vLLM | 任意本地模型 | OpenAI 兼容 |

### 2.4 自定义

| Provider | 说明 | API 格式 |
|----------|------|----------|
| 自定义 | 任意 OpenAI 兼容 API | OpenAI 格式 |

## 3. 配置流程

### 3.1 首次启动

1. 应用检测到无 API 配置
2. 弹出配置向导
3. 用户选择 Provider
4. 用户输入 API Key
5. 测试连接
6. 保存配置

### 3.2 设置页配置

用户可随时在设置页修改：

1. 切换 Provider
2. 修改 API Key
3. 选择模型
4. 调整参数（温度、最大 token 等）

## 4. 配置存储

### 4.1 配置文件位置

```text
C:/Users/{username}/AppData/Roaming/DesktopAIMascot/config/
  llm_config.json      # LLM 配置（不含 API Key）
  credentials.dpapi    # 加密的 API Key
```

### 4.2 配置文件格式

llm_config.json：

```json
{
  "provider": "deepseek",
  "model": "deepseek-chat",
  "temperature": 0.7,
  "maxTokens": 4096,
  "baseUrl": null,
  "customHeaders": {}
}
```

支持的 provider 值：

```text
国际: openai, anthropic
国产: deepseek, kimi, moonshot, zhipu, glm, baichuan, xunfei, spark, tongyi, qwen, doubao, lingyiwanwu, yi, minimax, stepfun
本地: ollama, lmstudio, vllm, local
自定义: custom
```

### 4.3 API Key 存储

使用 Windows DPAPI 加密：

```csharp
// 加密
byte[] encrypted = ProtectedData.Protect(
    Encoding.UTF8.GetBytes(apiKey),
    null,
    DataProtectionScope.CurrentUser);

// 解密
byte[] decrypted = ProtectedData.Unprotect(
    encrypted,
    null,
    DataProtectionScope.CurrentUser);
string apiKey = Encoding.UTF8.GetString(decrypted);
```

## 5. 接口设计

### 5.1 ILlmProvider 接口

```csharp
public interface ILlmProvider
{
    string ProviderName { get; }
    bool IsConfigured { get; }
    
    Task<LlmResponse> ChatAsync(
        List<LlmMessage> messages,
        LlmOptions? options = null,
        CancellationToken ct = default);
    
    Task<string> ChatAsync(
        string systemPrompt,
        string userMessage,
        CancellationToken ct = default);
    
    IAsyncEnumerable<LlmStreamChunk> ChatStreamAsync(
        List<LlmMessage> messages,
        LlmOptions? options = null,
        CancellationToken ct = default);
}
```

### 5.2 LlmProviderFactory

```csharp
public class LlmProviderFactory
{
    private readonly Dictionary<string, Func<ILlmProvider>> _providers;
    
    public ILlmProvider Create(string providerName)
    {
        if (_providers.TryGetValue(providerName, out var factory))
            return factory();
        
        throw new ArgumentException($"Unknown provider: {providerName}");
    }
    
    public IEnumerable<string> AvailableProviders => _providers.Keys;
}
```

### 5.3 IApiKeyStore 接口

```csharp
public interface IApiKeyStore
{
    Task<string?> GetApiKeyAsync(string provider);
    Task SetApiKeyAsync(string provider, string apiKey);
    Task RemoveApiKeyAsync(string provider);
    Task<bool> HasApiKeyAsync(string provider);
}
```

## 6. Provider 实现

### 6.1 OpenAI 兼容格式（通用）

国产模型大多支持 OpenAI 兼容格式，使用统一的 OpenAiCompatibleProvider：

```csharp
public class OpenAiCompatibleProvider : ILlmProvider
{
    private readonly HttpClient _httpClient;
    private readonly IApiKeyStore _apiKeyStore;
    private readonly LlmConfig _config;
    
    public async Task<LlmResponse> ChatAsync(
        List<LlmMessage> messages,
        LlmOptions? options = null,
        CancellationToken ct = default)
    {
        var apiKey = await _apiKeyStore.GetApiKeyAsync(_config.Provider);
        if (string.IsNullOrEmpty(apiKey))
            throw new InvalidOperationException($"{_config.Provider} API key not configured");
        
        var baseUrl = _config.BaseUrl ?? GetDefaultBaseUrl(_config.Provider);
        
        var request = new OpenAiChatRequest
        {
            Model = _config.Model,
            Messages = messages.Select(m => new OpenAiMessage
            {
                Role = m.Role,
                Content = m.Content
            }).ToList(),
            Temperature = options?.Temperature ?? _config.Temperature,
            MaxTokens = options?.MaxTokens ?? _config.MaxTokens
        };
        
        var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/v1/chat/completions")
        {
            Content = JsonContent.Create(request)
        };
        httpRequest.Headers.Add("Authorization", $"Bearer {apiKey}");
        
        var response = await _httpClient.SendAsync(httpRequest, ct);
        return await ParseResponseAsync(response, ct);
    }
    
    private string GetDefaultBaseUrl(string provider) => provider switch
    {
        "deepseek" => "https://api.deepseek.com",
        "kimi" or "moonshot" => "https://api.moonshot.cn",
        "zhipu" or "glm" => "https://open.bigmodel.cn/api/paas/v4",
        "baichuan" => "https://api.baichuan-ai.com",
        "xunfei" or "spark" => "https://spark-api-open.xf-yun.com",
        "tongyi" or "qwen" => "https://dashscope.aliyuncs.com/compatible-mode",
        "doubao" => "https://ark.cn-beijing.volces.com/api/v3",
        "lingyiwanwu" or "yi" => "https://api.lingyiwanwu.com",
        "minimax" => "https://api.minimax.chat",
        "stepfun" => "https://api.stepfun.com",
        _ => throw new ArgumentException($"Unknown provider: {provider}")
    };
}
```

### 6.2 OpenAI Provider

```csharp
public class OpenAiProvider : OpenAiCompatibleProvider
{
    public OpenAiProvider(
        HttpClient httpClient,
        IApiKeyStore apiKeyStore,
        IOptions<LlmConfig> config)
        : base(httpClient, apiKeyStore, config, "openai")
    {
    }
}
```

### 6.3 DeepSeek Provider

```csharp
public class DeepSeekProvider : OpenAiCompatibleProvider
{
    public DeepSeekProvider(
        HttpClient httpClient,
        IApiKeyStore apiKeyStore,
        IOptions<LlmConfig> config)
        : base(httpClient, apiKeyStore, config, "deepseek")
    {
    }
}
```

### 6.4 Kimi (Moonshot) Provider

```csharp
public class KimiProvider : OpenAiCompatibleProvider
{
    public KimiProvider(
        HttpClient httpClient,
        IApiKeyStore apiKeyStore,
        IOptions<LlmConfig> config)
        : base(httpClient, apiKeyStore, config, "kimi")
    {
    }
}
```

### 6.5 Anthropic Provider

```csharp
public class AnthropicProvider : ILlmProvider
{
    public async Task<LlmResponse> ChatAsync(
        List<LlmMessage> messages,
        LlmOptions? options = null,
        CancellationToken ct = default)
    {
        var apiKey = await _apiKeyStore.GetApiKeyAsync("anthropic");
        
        var request = new AnthropicRequest
        {
            Model = _config.Model,
            MaxTokens = options?.MaxTokens ?? _config.MaxTokens,
            System = messages.FirstOrDefault(m => m.Role == "system")?.Content,
            Messages = messages.Where(m => m.Role != "system").Select(m => 
                new AnthropicMessage
                {
                    Role = m.Role,
                    Content = m.Content
                }).ToList()
        };
        
        var httpRequest = new HttpRequestMessage(HttpMethod.Post, "https://api.anthropic.com/v1/messages")
        {
            Content = JsonContent.Create(request)
        };
        httpRequest.Headers.Add("x-api-key", apiKey);
        httpRequest.Headers.Add("anthropic-version", "2023-06-01");
        
        var response = await _httpClient.SendAsync(httpRequest, ct);
        return await ParseResponseAsync(response, ct);
    }
}
```

### 6.6 本地模型 Provider

```csharp
public class LocalModelProvider : OpenAiCompatibleProvider
{
    public LocalModelProvider(
        HttpClient httpClient,
        IApiKeyStore apiKeyStore,
        IOptions<LlmConfig> config)
        : base(httpClient, apiKeyStore, config, "local")
    {
    }
    
    // 本地模型通常不需要 API Key，但保持接口一致性
}

## 7. 设置 UI

### 7.1 设置页布局

```text
LLM 设置
├── Provider 选择下拉框
│   ├── 国际
│   │   ├── OpenAI
│   │   └── Anthropic
│   ├── 国产
│   │   ├── DeepSeek
│   │   ├── Kimi (Moonshot)
│   │   ├── 智谱 AI (GLM)
│   │   ├── 百川智能
│   │   ├── 讯飞星火
│   │   ├── 阿里通义 (Qwen)
│   │   ├── 字节豆包
│   │   ├── 零一万物 (Yi)
│   │   ├── MiniMax
│   │   └── 阶跃星辰
│   ├── 本地
│   │   ├── Ollama
│   │   ├── LM Studio
│   │   └── vLLM
│   └── 自定义
├── API Key 输入框（掩码显示）
├── Base URL 输入框（自定义时显示）
├── 模型选择下拉框
├── 测试连接按钮
├── 高级设置
│   ├── 温度滑块
│   ├── 最大 token 输入
│   └── 自定义请求头
└── 保存按钮
```

### 7.2 Provider 配置示例

| Provider | 默认 Base URL | 需要 API Key |
|----------|---------------|--------------|
| DeepSeek | https://api.deepseek.com | 是 |
| Kimi | https://api.moonshot.cn | 是 |
| 智谱 AI | https://open.bigmodel.cn/api/paas/v4 | 是 |
| 百川智能 | https://api.baichuan-ai.com | 是 |
| 讯飞星火 | https://spark-api-open.xf-yun.com | 是 |
| 阿里通义 | https://dashscope.aliyuncs.com/compatible-mode | 是 |
| 字节豆包 | https://ark.cn-beijing.volces.com/api/v3 | 是 |
| 零一万物 | https://api.lingyiwanwu.com | 是 |
| MiniMax | https://api.minimax.chat | 是 |
| 阶跃星辰 | https://api.stepfun.com | 是 |
| Ollama | http://localhost:11434 | 否 |
| LM Studio | http://localhost:1234 | 否 |

### 7.3 测试连接

点击"测试连接"按钮：

1. 发送简单请求到 Provider
2. 显示连接结果
3. 显示模型信息
4. 显示响应时间
5. 显示可用模型列表（如果 API 支持）

## 8. 安全考虑

1. **API Key 不明文存储**：使用 Windows DPAPI 加密
2. **日志脱敏**：过滤 api_key、Authorization、Bearer
3. **不上传 Key**：所有 Key 仅本地存储
4. **用户可控**：用户可随时删除 Key
5. **导出排除**：导出日志/配置时排除 Key

## 9. 错误处理

| 错误场景 | 处理方式 |
|----------|----------|
| 未配置 API Key | 引导用户配置 |
| API Key 无效 | 提示重新配置 |
| 网络错误 | 提示检查网络 |
| 配额用尽 | 提示更换账户或等待 |
| 模型不存在 | 提示选择可用模型 |

## 10. 扩展性

1. **插件机制**：后续可支持自定义 Provider 插件
2. **代理支持**：支持 HTTP 代理配置
3. **多账户**：支持同一 Provider 多个 API Key
4. **负载均衡**：支持多个 Key 轮询使用