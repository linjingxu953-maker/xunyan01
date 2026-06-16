using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using DesktopMascot.Agent.Models;

namespace DesktopMascot.Agent.Tools;

/// <summary>
/// 网络请求工具 - HTTP 请求、API 调用、数据抓取
/// </summary>
public class NetworkRequestTool : ITool
{
    private readonly HttpClient _httpClient;
    private static readonly Dictionary<string, string> _headers = new()
    {
        ["User-Agent"] = "DesktopMascot/1.0",
        ["Accept"] = "application/json"
    };

    public NetworkRequestTool(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
    }

    public string Name => "network_request";
    public string Description => "网络请求：HTTP GET/POST/PUT/DELETE、API 调用、数据抓取。";
    public string ParametersSchema => """
    {
        "type": "object",
        "properties": {
            "method": { "type": "string", "enum": ["GET", "POST", "PUT", "DELETE", "PATCH"], "description": "HTTP 方法" },
            "url": { "type": "string", "description": "请求 URL" },
            "headers": { "type": "object", "description": "请求头" },
            "body": { "type": "string", "description": "请求体（POST/PUT）" },
            "timeout": { "type": "integer", "description": "超时时间秒数" }
        },
        "required": ["method", "url"]
    }
    """;

    public async Task<ToolResult> ExecuteAsync(string arguments, CancellationToken ct = default)
    {
        try
        {
            var doc = JsonDocument.Parse(arguments);
            var root = doc.RootElement;
            var method = root.TryGetProperty("method", out var mEl) ? mEl.GetString() ?? "GET" : "GET";
            var url = root.TryGetProperty("url", out var uEl) ? uEl.GetString() ?? "" : "";

            if (string.IsNullOrEmpty(url)) return Fail("缺少 url 参数");

            var timeout = root.TryGetProperty("timeout", out var tEl) ? tEl.GetInt32() : 30;
            _httpClient.Timeout = TimeSpan.FromSeconds(timeout);

            // 添加自定义 headers
            if (root.TryGetProperty("headers", out var hEl))
            {
                foreach (var prop in hEl.EnumerateObject())
                {
                    _headers[prop.Name] = prop.Value.GetString() ?? "";
                }
            }

            var request = new HttpRequestMessage(new HttpMethod(method), url);
            foreach (var header in _headers)
            {
                request.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }

            // 添加请求体
            if (method is "POST" or "PUT" or "PATCH")
            {
                var body = root.TryGetProperty("body", out var bEl) ? bEl.GetString() ?? "" : "";
                request.Content = new StringContent(body, Encoding.UTF8, "application/json");
            }

            var sw = System.Diagnostics.Stopwatch.StartNew();
            var response = await _httpClient.SendAsync(request, ct);
            sw.Stop();

            var responseContent = await response.Content.ReadAsStringAsync(ct);
            var sb = new StringBuilder();
            sb.AppendLine($"HTTP {method} {url}");
            sb.AppendLine($"状态码：{(int)response.StatusCode} {response.StatusCode}");
            sb.AppendLine($"耗时：{sw.ElapsedMilliseconds} ms");
            sb.AppendLine($"响应大小：{responseContent.Length} 字符");
            sb.AppendLine();
            sb.AppendLine("响应内容：");

            // 格式化 JSON 响应
            try
            {
                var jsonDoc = JsonDocument.Parse(responseContent);
                sb.AppendLine(JsonSerializer.Serialize(jsonDoc, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch
            {
                sb.AppendLine(responseContent.Length > 2000 ? responseContent[..2000] + "..." : responseContent);
            }

            return new ToolResult { Name = Name, Success = true, Content = sb.ToString() };
        }
        catch (Exception ex)
        {
            return Fail($"网络请求失败：{ex.Message}");
        }
    }

    private static ToolResult Fail(string error) => new() { Name = "network_request", Success = false, Error = error };
}
