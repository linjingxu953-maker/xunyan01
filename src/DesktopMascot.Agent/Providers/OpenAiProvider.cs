using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using DesktopMascot.Agent.Models;

namespace DesktopMascot.Agent.Providers;

/// <summary>
/// OpenAI API 提供者（API Key 使用 DPAPI 加密存储）
/// </summary>
public class OpenAiProvider : ILlmProvider
{
    private readonly HttpClient _httpClient;
    private readonly string _encryptedApiKey;
    private readonly string _model;
    private readonly string _baseUrl;

    public OpenAiProvider(
        HttpClient httpClient,
        string apiKey,
        string model = "gpt-4",
        string baseUrl = "https://api.openai.com/v1")
    {
        _httpClient = httpClient;
        // 立即加密存储，避免明文 Key 长时间驻留内存
        _encryptedApiKey = CredentialProtector.Protect(apiKey);
        _model = model;
        _baseUrl = baseUrl;
    }

    /// <summary>
    /// 获取解密的 API Key（仅在需要使用时调用）
    /// </summary>
    private string GetApiKey()
    {
        return CredentialProtector.Unprotect(_encryptedApiKey);
    }

    public async Task<LlmResponse> ChatAsync(
        IEnumerable<LlmMessage> messages,
        IEnumerable<ToolDefinition>? tools = null,
        CancellationToken ct = default)
    {
        try
        {
            var requestBody = BuildRequestBody(messages, tools);
            var content = new StringContent(
                JsonSerializer.Serialize(requestBody),
                Encoding.UTF8,
                "application/json");

            var request = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/chat/completions")
            {
                Content = content
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", GetApiKey());

            var httpResponse = await _httpClient.SendAsync(request, ct);
            var responseJson = await httpResponse.Content.ReadAsStringAsync(ct);

            if (!httpResponse.IsSuccessStatusCode)
            {
                return new LlmResponse
                {
                    Success = false,
                    Error = $"API 错误: {httpResponse.StatusCode} - {responseJson}"
                };
            }

            var result = JsonSerializer.Deserialize<JsonElement>(responseJson);
            var choice = result.GetProperty("choices")[0].GetProperty("message");

            var llmResponse = new LlmResponse
            {
                Success = true,
                Content = choice.TryGetProperty("content", out var c) ? c.GetString() ?? "" : "",
                TokensUsed = result.GetProperty("usage").GetProperty("total_tokens").GetInt32()
            };

            // 解析 OpenAI 原生 tool_calls
            if (choice.TryGetProperty("tool_calls", out var toolCallsElement))
            {
                llmResponse.ToolCalls = new List<ToolCall>();
                foreach (var tc in toolCallsElement.EnumerateArray())
                {
                    var func = tc.GetProperty("function");
                    llmResponse.ToolCalls.Add(new ToolCall
                    {
                        Id = tc.GetProperty("id").GetString() ?? "",
                        Name = func.GetProperty("name").GetString() ?? "",
                        Arguments = func.GetProperty("arguments").GetString() ?? "{}"
                    });
                }
            }

            return llmResponse;
        }
        catch (Exception ex)
        {
            return new LlmResponse
            {
                Success = false,
                Error = $"请求异常: {ex.Message}"
            };
        }
    }

    public async IAsyncEnumerable<string> ChatStreamAsync(
        IEnumerable<LlmMessage> messages,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        // 简化实现：非流式返回
        var response = await ChatAsync(messages, null, ct);
        if (response.Success)
        {
            yield return response.Content;
        }
        else
        {
            yield return $"错误: {response.Error}";
        }
    }

    private object BuildRequestBody(IEnumerable<LlmMessage> messages, IEnumerable<ToolDefinition>? tools)
    {
        var body = new Dictionary<string, object>
        {
            ["model"] = _model,
            ["messages"] = messages.Select(m => new { role = m.Role, content = m.Content }).ToArray()
        };

        if (tools != null && tools.Any())
        {
            body["tools"] = tools.Select(t => new
            {
                type = "function",
                function = new
                {
                    name = t.Name,
                    description = t.Description,
                    parameters = JsonSerializer.Deserialize<object>(t.Parameters)
                }
            }).ToArray();
        }

        return body;
    }
}
