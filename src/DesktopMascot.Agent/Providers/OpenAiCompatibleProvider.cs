using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using DesktopMascot.Agent.Models;

namespace DesktopMascot.Agent.Providers;

/// <summary>
/// OpenAI 兼容格式 Provider - 支持 OpenAI、DeepSeek、Kimi、智谱、通义、豆包等
/// </summary>
public class OpenAiCompatibleProvider : ILlmProvider
{
    private readonly HttpClient _httpClient;
    private readonly LlmProviderConfig _config;

    public OpenAiCompatibleProvider(HttpClient httpClient, LlmProviderConfig config)
    {
        _httpClient = httpClient;
        _config = config;
    }

    public string ProviderName => _config.ProviderName;
    public string Model => _config.Model;
    public bool IsConfigured => !string.IsNullOrEmpty(_config.EncryptedApiKey) || _config.ProviderName == "local";

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

            var request = new HttpRequestMessage(HttpMethod.Post, $"{_config.BaseUrl}/chat/completions")
            {
                Content = content
            };

            // 添加 Authorization header（本地模型可能不需要）
            if (!string.IsNullOrEmpty(_config.EncryptedApiKey))
            {
                var apiKey = _config.GetApiKey();
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            }

            var httpResponse = await _httpClient.SendAsync(request, ct);
            var responseJson = await httpResponse.Content.ReadAsStringAsync(ct);

            if (!httpResponse.IsSuccessStatusCode)
            {
                return new LlmResponse
                {
                    Success = false,
                    Error = $"API 错误 ({_config.ProviderName}): {httpResponse.StatusCode} - {responseJson}"
                };
            }

            var result = JsonSerializer.Deserialize<JsonElement>(responseJson);
            var choice = result.GetProperty("choices")[0].GetProperty("message");

            var llmResponse = new LlmResponse
            {
                Success = true,
                Content = choice.TryGetProperty("content", out var c) ? c.GetString() ?? "" : "",
                TokensUsed = result.TryGetProperty("usage", out var usage) && usage.TryGetProperty("total_tokens", out var tokens)
                    ? tokens.GetInt32()
                    : 0
            };

            // 解析 tool_calls
            if (choice.TryGetProperty("tool_calls", out var toolCallsElement))
            {
                llmResponse.ToolCalls = new List<ToolCall>();
                foreach (var tc in toolCallsElement.EnumerateArray())
                {
                    var func = tc.GetProperty("function");
                    llmResponse.ToolCalls.Add(new ToolCall
                    {
                        Id = tc.TryGetProperty("id", out var id) ? id.GetString() ?? "" : Guid.NewGuid().ToString("N"),
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
                Error = $"请求异常 ({_config.ProviderName}): {ex.Message}"
            };
        }
    }

    public async IAsyncEnumerable<string> ChatStreamAsync(
        IEnumerable<LlmMessage> messages,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var bodyDict = BuildRequestBody(messages, null) as Dictionary<string, object>;
        if (bodyDict == null)
        {
            yield return "[错误: 无法构建请求]";
            yield break;
        }
        bodyDict["stream"] = true;

        var content = new StringContent(
            JsonSerializer.Serialize(bodyDict),
            Encoding.UTF8,
            "application/json");

        var request = new HttpRequestMessage(HttpMethod.Post, $"{_config.BaseUrl}/chat/completions")
        {
            Content = content
        };

        if (!string.IsNullOrEmpty(_config.EncryptedApiKey))
        {
            var apiKey = _config.GetApiKey();
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        }

        HttpResponseMessage? httpResponse = null;
        string? error = null;

        try
        {
            httpResponse = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        }
        catch (Exception ex)
        {
            error = $"[连接错误: {ex.Message}]";
        }

        if (error != null)
        {
            yield return error;
            yield break;
        }

        if (!httpResponse!.IsSuccessStatusCode)
        {
            var errorContent = await httpResponse.Content.ReadAsStringAsync(ct);
            yield return $"[API 错误: {httpResponse.StatusCode} - {errorContent}]";
            httpResponse.Dispose();
            yield break;
        }

        await using var stream = await httpResponse.Content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(stream);

        while (!reader.EndOfStream)
        {
            ct.ThrowIfCancellationRequested();
            var line = await reader.ReadLineAsync(ct);
            if (string.IsNullOrEmpty(line)) continue;
            if (!line.StartsWith("data: ")) continue;
            var data = line[6..];
            if (data == "[DONE]") break;

            if (TryParseStreamChunk(data, out var chunk))
            {
                yield return chunk;
            }
        }

        httpResponse.Dispose();
    }

    private static bool TryParseStreamChunk(string data, out string chunk)
    {
        chunk = "";
        try
        {
            var doc = JsonDocument.Parse(data);
            var root = doc.RootElement;
            if (root.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
            {
                var delta = choices[0].GetProperty("delta");
                if (delta.TryGetProperty("content", out var contentElement))
                {
                    chunk = contentElement.GetString() ?? "";
                    return !string.IsNullOrEmpty(chunk);
                }
            }
        }
        catch { }
        return false;
    }

    private object BuildRequestBody(IEnumerable<LlmMessage> messages, IEnumerable<ToolDefinition>? tools)
    {
        var body = new Dictionary<string, object>
        {
            ["model"] = _config.Model,
            ["messages"] = messages.Select(m => BuildMessageObject(m)).ToArray(),
            ["temperature"] = _config.Temperature,
            ["max_tokens"] = _config.MaxTokens
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

    private static object BuildMessageObject(LlmMessage m)
    {
        if (m.Images != null && m.Images.Count > 0)
        {
            var contentParts = new List<object>();
            if (!string.IsNullOrEmpty(m.Content))
            {
                contentParts.Add(new { type = "text", text = m.Content });
            }
            foreach (var img in m.Images)
            {
                var imageUrl = img.Url ?? $"data:{img.MediaType};base64,{img.Base64Data}";
                contentParts.Add(new
                {
                    type = "image_url",
                    image_url = new { url = imageUrl }
                });
            }
            return new { role = m.Role, content = contentParts };
        }

        return new { role = m.Role, content = m.Content };
    }
}
