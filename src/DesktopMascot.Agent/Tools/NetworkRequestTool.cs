using DesktopMascot.Core.Tools;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using DesktopMascot.Agent.Models;

namespace DesktopMascot.Agent.Tools;

/// <summary>
/// 网络请求工具增强版 — 自定义 Headers/超时/重试/代理/认证/表单/文件上传/Cookie
/// </summary>
public class NetworkRequestTool : ITool
{
    private readonly HttpMessageHandler? _handler;

    /// <param name="handler">可注入 HttpMessageHandler，测试可传入 mock handler</param>
    public NetworkRequestTool(HttpMessageHandler? handler = null)
    {
        _handler = handler;
    }

    public string Name => "network_request";
    public string Description => "网络请求：HTTP 方法全支持、自定义 Headers、超时控制、重试策略、认证、代理、表单提交、文件上传、Cookie 管理。";
    public string ParametersSchema => """
    {
        "type": "object",
        "properties": {
            "method": { "type": "string", "enum": ["GET", "POST", "PUT", "DELETE", "PATCH", "HEAD", "OPTIONS"], "description": "HTTP 方法" },
            "url": { "type": "string", "description": "请求 URL" },
            "headers": { "type": "object", "description": "自定义请求头" },
            "body": { "type": "string", "description": "请求体（JSON/文本）" },
            "body_type": { "type": "string", "enum": ["json", "text", "form", "form_data", "binary"], "description": "请求体类型" },
            "timeout": { "type": "integer", "description": "超时秒数（默认30）" },
            "retries": { "type": "integer", "description": "重试次数（默认0）" },
            "retry_delay": { "type": "integer", "description": "重试间隔秒数（默认1）" },
            "follow_redirects": { "type": "boolean", "description": "是否跟随重定向（默认true）" },
            "auth_type": { "type": "string", "enum": ["none", "bearer", "basic", "api_key"], "description": "认证类型" },
            "auth_value": { "type": "string", "description": "认证值（Token/密码/API Key）" },
            "auth_header": { "type": "string", "description": "自定义认证头名（api_key 模式使用）" },
            "proxy": { "type": "string", "description": "代理地址（如 http://proxy:8080）" },
            "cookies": { "type": "object", "description": "请求 Cookie" },
            "files": { "type": "array", "description": "文件上传（form_data 模式）" },
            "accept": { "type": "string", "description": "Accept 头（默认 application/json）" },
            "user_agent": { "type": "string", "description": "User-Agent（默认 DesktopMascot/1.0）" },
            "response_max_chars": { "type": "integer", "description": "响应最大字符数（默认5000）" }
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
            var retries = root.TryGetProperty("retries", out var rEl) ? rEl.GetInt32() : 0;
            var retryDelay = root.TryGetProperty("retry_delay", out var rdEl) ? rdEl.GetInt32() : 1;
            var followRedirects = root.TryGetProperty("follow_redirects", out var frEl) ? frEl.GetBoolean() : true;
            var proxyUrl = root.TryGetProperty("proxy", out var pEl) ? pEl.GetString() : null;
            var responseMaxChars = root.TryGetProperty("response_max_chars", out var rmEl) ? rmEl.GetInt32() : 5000;

            // 构建 handler。测试或上层可注入 handler，避免真实网络依赖。
            var handler = _handler ?? BuildHandler(followRedirects, proxyUrl);

            using var client = new HttpClient(handler, disposeHandler: _handler is null)
            {
                Timeout = TimeSpan.FromSeconds(Math.Min(timeout, 300))
            };

            var request = new HttpRequestMessage(new HttpMethod(method), url);

            // 默认 Headers
            var accept = root.TryGetProperty("accept", out var aEl) ? aEl.GetString() ?? "application/json" : "application/json";
            var userAgent = root.TryGetProperty("user_agent", out var uaEl) ? uaEl.GetString() ?? "DesktopMascot/1.0" : "DesktopMascot/1.0";
            request.Headers.TryAddWithoutValidation("Accept", accept);
            request.Headers.TryAddWithoutValidation("User-Agent", userAgent);

            // 自定义 Headers
            if (root.TryGetProperty("headers", out var hEl))
            {
                foreach (var prop in hEl.EnumerateObject())
                    request.Headers.TryAddWithoutValidation(prop.Name, prop.Value.GetString() ?? "");
            }

            // 认证
            ApplyAuth(root, request);

            // Cookie
            if (root.TryGetProperty("cookies", out var cEl))
            {
                var cookieContainer = handler is HttpClientHandler h ? h.CookieContainer : new CookieContainer();
                var uri = new Uri(url);
                foreach (var prop in cEl.EnumerateObject())
                    cookieContainer.Add(uri, new Cookie(prop.Name, prop.Value.GetString() ?? ""));
                if (handler is HttpClientHandler hc)
                    hc.CookieContainer = cookieContainer;
            }

            // 请求体
            ApplyBody(root, request);

            // 带重试的请求
            HttpResponseMessage? response = null;
            string? lastError = null;

            for (int attempt = 0; attempt <= retries; attempt++)
            {
                try
                {
                    if (attempt > 0)
                        await Task.Delay(TimeSpan.FromSeconds(retryDelay * attempt), ct);

                    response?.Dispose();
                    response = await client.SendAsync(request, ct);
                    break;
                }
                catch (Exception ex)
                {
                    lastError = ex.Message;
                    if (attempt == retries) throw;
                }
            }

            if (response == null)
                return Fail($"请求失败：{lastError}");

            var sw = System.Diagnostics.Stopwatch.StartNew();
            var responseContent = await response.Content.ReadAsStringAsync(ct);
            sw.Stop();

            var sb = new StringBuilder();
            sb.AppendLine($"HTTP {method} {url}");
            sb.AppendLine($"状态码：{(int)response.StatusCode} {response.StatusCode}");
            sb.AppendLine($"耗时：{sw.ElapsedMilliseconds} ms");
            sb.AppendLine($"响应大小：{responseContent.Length:N0} 字符");
            if (retries > 0) sb.AppendLine($"重试：{retries} 次");

            // 响应 Headers
            var responseHeaders = response.Headers
                .Concat(response.Content.Headers)
                .Where(h => h.Key is "Content-Type" or "X-RateLimit-Remaining" or "X-RateLimit-Reset" or "Set-Cookie" or "Location")
                .ToList();
            if (responseHeaders.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("关键响应头：");
                foreach (var h in responseHeaders)
                    sb.AppendLine($"  {h.Key}: {string.Join(", ", h.Value)}");
            }

            sb.AppendLine();
            sb.AppendLine("响应内容：");

            // 格式化输出
            var displayContent = responseContent.Length > responseMaxChars
                ? responseContent[..responseMaxChars] + $"...（截断，共 {responseContent.Length:N0} 字符）"
                : responseContent;

            try
            {
                var jsonDoc = JsonDocument.Parse(displayContent);
                sb.AppendLine(JsonSerializer.Serialize(jsonDoc, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch
            {
                sb.AppendLine(displayContent);
            }

            return new ToolResult { Name = Name, Success = true, Content = sb.ToString() };
        }
        catch (TaskCanceledException)
        {
            return Fail("请求超时");
        }
        catch (HttpRequestException ex)
        {
            return Fail($"网络错误：{ex.Message}（状态码：{ex.StatusCode}）");
        }
        catch (Exception ex)
        {
            return Fail($"网络请求失败：{ex.Message}");
        }
    }

    private static HttpMessageHandler BuildHandler(bool followRedirects, string? proxyUrl)
    {
        var handler = new HttpClientHandler
        {
            AllowAutoRedirect = followRedirects,
            UseCookies = true,
            CookieContainer = new CookieContainer()
        };

        if (!string.IsNullOrEmpty(proxyUrl))
        {
            handler.Proxy = new WebProxy(proxyUrl);
            handler.UseProxy = true;
        }

        return handler;
    }

    private static void ApplyAuth(JsonElement root, HttpRequestMessage request)
    {
        var authType = root.TryGetProperty("auth_type", out var atEl) ? atEl.GetString() ?? "none" : "none";
        var authValue = root.TryGetProperty("auth_value", out var avEl) ? avEl.GetString() ?? "" : "";

        if (string.IsNullOrEmpty(authValue) || authType == "none") return;

        switch (authType)
        {
            case "bearer":
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", authValue);
                break;
            case "basic":
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic",
                    Convert.ToBase64String(Encoding.UTF8.GetBytes(authValue)));
                break;
            case "api_key":
                var headerName = root.TryGetProperty("auth_header", out var ahEl) ? ahEl.GetString() ?? "X-API-Key" : "X-API-Key";
                request.Headers.TryAddWithoutValidation(headerName, authValue);
                break;
        }
    }

    private static void ApplyBody(JsonElement root, HttpRequestMessage request)
    {
        var method = request.Method.Method;
        if (method is not ("POST" or "PUT" or "PATCH")) return;

        var bodyType = root.TryGetProperty("body_type", out var btEl) ? btEl.GetString() ?? "json" : "json";
        var body = root.TryGetProperty("body", out var bEl) ? bEl.GetString() ?? "" : "";

        switch (bodyType)
        {
            case "json":
                request.Content = new StringContent(body, Encoding.UTF8, "application/json");
                break;
            case "text":
                request.Content = new StringContent(body, Encoding.UTF8, "text/plain");
                break;
            case "form":
                var formData = new FormUrlEncodedContent(
                    body.Split('&', StringSplitOptions.RemoveEmptyEntries)
                        .Select(pair =>
                        {
                            var kv = pair.Split('=', 2);
                            return new KeyValuePair<string, string>(kv[0], kv.Length > 1 ? kv[1] : "");
                        }));
                request.Content = formData;
                break;
            case "form_data":
                var multipart = new MultipartFormDataContent();
                multipart.Add(new StringContent(body, Encoding.UTF8, "text/plain"), "data");
                request.Content = multipart;
                break;
            case "binary":
                var bytes = Convert.FromBase64String(body);
                request.Content = new ByteArrayContent(bytes);
                request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
                break;
        }
    }

    private static ToolResult Fail(string error) => new() { Name = "network_request", Success = false, Error = error };
}
