using DesktopMascot.Core.Tools;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using DesktopMascot.Agent.Models;

namespace DesktopMascot.Agent.Tools;

/// <summary>
/// 翻译工具 — 多语言翻译、剪贴板翻译、选区翻译、术语表
/// </summary>
public class TranslateTool : ITool
{
    private readonly ITool _networkRequest;
    private readonly ITool _clipboard;

    private static readonly Dictionary<string, string> LanguageNames = new()
    {
        ["zh"] = "中文", ["en"] = "English", ["ja"] = "日本語", ["ko"] = "한국어",
        ["fr"] = "Français", ["de"] = "Deutsch", ["es"] = "Español", ["ru"] = "Русский",
        ["ar"] = "العربية", ["pt"] = "Português", ["it"] = "Italiano", ["th"] = "ไทย",
        ["vi"] = "Tiếng Việt", ["hi"] = "हिन्दी", ["auto"] = "自动检测"
    };

    public string Name => "translate";
    public string Description => "翻译：多语言文本翻译、剪贴板翻译、术语表、批量翻译。支持 14+ 语言。";
    public string ParametersSchema => """
    {
        "type": "object",
        "properties": {
            "action": { "type": "string", "enum": ["translate", "clipboard", "batch", "languages", "detect"], "description": "操作类型" },
            "text": { "type": "string", "description": "要翻译的文本" },
            "from": { "type": "string", "description": "源语言（auto=自动检测）" },
            "to": { "type": "string", "description": "目标语言" },
            "texts": { "type": "string", "description": "批量翻译文本（JSON数组）" },
            "format": { "type": "string", "enum": ["text", "json", "srt"], "description": "输出格式" }
        },
        "required": ["action"]
    }
    """;

    public TranslateTool(ITool networkRequest, ITool clipboard)
    {
        _networkRequest = networkRequest;
        _clipboard = clipboard;
    }

    public async Task<ToolResult> ExecuteAsync(string arguments, CancellationToken ct = default)
    {
        try
        {
            var doc = JsonDocument.Parse(arguments);
            var root = doc.RootElement;
            var action = root.TryGetProperty("action", out var aEl) ? aEl.GetString() ?? "" : "";

            return action switch
            {
                "translate" => await TranslateAsync(root, ct),
                "clipboard" => await ClipboardTranslateAsync(root, ct),
                "batch" => await BatchTranslateAsync(root, ct),
                "languages" => GetLanguages(),
                "detect" => await DetectLanguageAsync(root, ct),
                _ => Fail($"不支持的操作：{action}")
            };
        }
        catch (Exception ex)
        {
            return Fail($"翻译失败：{ex.Message}");
        }
    }

    private async Task<ToolResult> TranslateAsync(JsonElement root, CancellationToken ct)
    {
        var text = GetRequiredString(root, "text");
        var from = root.TryGetProperty("from", out var fEl) ? fEl.GetString() ?? "auto" : "auto";
        var to = root.TryGetProperty("to", out var tEl) ? tEl.GetString() ?? "zh" : "zh";

        if (string.IsNullOrEmpty(text)) return Fail("缺少 text 参数");

        // 使用 MyMemory API（免费，无需 Key）
        var url = $"https://api.mymemory.translated.net/get?q={Uri.EscapeDataString(text)}&langpair={from}|{to}";
        var result = await _networkRequest.ExecuteAsync(
            $"{{\"method\":\"GET\",\"url\":\"{url}\",\"timeout\":15}}", ct);

        if (!result.Success)
            return Fail($"翻译请求失败：{result.Error}");

        // 解析响应
        try
        {
            var responseDoc = JsonDocument.Parse(result.Content);
            var responseData = responseDoc.RootElement;
            var translatedText = "";

            if (responseData.TryGetProperty("responseData", out var data) &&
                data.TryGetProperty("translatedText", out var translated))
            {
                translatedText = translated.GetString() ?? "";
            }

            if (string.IsNullOrEmpty(translatedText))
                return Fail("翻译结果为空");

            var sb = new StringBuilder();
            sb.AppendLine("翻译结果");
            sb.AppendLine($"  源语言：{LanguageNames.GetValueOrDefault(from, from)}");
            sb.AppendLine($"  目标语言：{LanguageNames.GetValueOrDefault(to, to)}");
            sb.AppendLine();
            sb.AppendLine($"  原文：{text}");
            sb.AppendLine($"  译文：{translatedText}");

            return new ToolResult { Name = Name, Success = true, Content = sb.ToString() };
        }
        catch
        {
            return Fail("翻译结果解析失败");
        }
    }

    private async Task<ToolResult> ClipboardTranslateAsync(JsonElement root, CancellationToken ct)
    {
        var to = root.TryGetProperty("to", out var tEl) ? tEl.GetString() ?? "zh" : "zh";

        // 读取剪贴板
        var clipResult = await _clipboard.ExecuteAsync("""{"action":"read"}""", ct);
        if (!clipResult.Success)
            return Fail($"剪贴板读取失败：{clipResult.Error}");

        var text = clipResult.Content.Trim();
        if (string.IsNullOrEmpty(text))
            return Fail("剪贴板为空");

        // 翻译
        var translateArgs = $"{{\"action\":\"translate\",\"text\":\"{EscapeJson(text)}\",\"to\":\"{to}\"}}";
        var translateDoc = JsonDocument.Parse(translateArgs);
        return await TranslateAsync(translateDoc.RootElement, ct);
    }

    private async Task<ToolResult> BatchTranslateAsync(JsonElement root, CancellationToken ct)
    {
        var textsJson = GetRequiredString(root, "texts");
        var to = root.TryGetProperty("to", out var tEl) ? tEl.GetString() ?? "zh" : "zh";
        var format = root.TryGetProperty("format", out var fmtEl) ? fmtEl.GetString() ?? "text" : "text";

        if (string.IsNullOrEmpty(textsJson)) return Fail("缺少 texts 参数");

        List<string> texts;
        try
        {
            texts = JsonSerializer.Deserialize<List<string>>(textsJson) ?? new();
        }
        catch
        {
            texts = textsJson.Split('\n', StringSplitOptions.RemoveEmptyEntries).ToList();
        }

        if (texts.Count == 0) return Fail("文本列表为空");

        var results = new List<(string Original, string Translated)>();

        foreach (var text in texts)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var url = $"https://api.mymemory.translated.net/get?q={Uri.EscapeDataString(text)}&langpair=auto|{to}";
                var result = await _networkRequest.ExecuteAsync(
                    $"{{\"method\":\"GET\",\"url\":\"{url}\",\"timeout\":15}}", ct);

                if (result.Success)
                {
                    var doc = JsonDocument.Parse(result.Content);
                    if (doc.RootElement.TryGetProperty("responseData", out var data) &&
                        data.TryGetProperty("translatedText", out var translated))
                    {
                        results.Add((text, translated.GetString() ?? ""));
                        continue;
                    }
                }

                results.Add((text, "[翻译失败]"));
            }
            catch
            {
                results.Add((text, "[翻译失败]"));
            }

            await Task.Delay(300, ct); // 避免 API 限流
        }

        if (format == "json")
        {
            var jsonResults = results.Select(r => new { original = r.Original, translated = r.Translated });
            return new ToolResult
            {
                Name = Name,
                Success = true,
                Content = JsonSerializer.Serialize(jsonResults, new JsonSerializerOptions { WriteIndented = true })
            };
        }

        var sb = new StringBuilder();
        sb.AppendLine($"批量翻译完成（{results.Count} 条，目标：{LanguageNames.GetValueOrDefault(to, to)}）");
        sb.AppendLine();
        foreach (var (original, translated) in results)
        {
            sb.AppendLine($"  {original}");
            sb.AppendLine($"  → {translated}");
            sb.AppendLine();
        }

        return new ToolResult { Name = Name, Success = true, Content = sb.ToString() };
    }

    private ToolResult GetLanguages()
    {
        var sb = new StringBuilder();
        sb.AppendLine("支持的语言：");
        foreach (var (code, name) in LanguageNames)
            sb.AppendLine($"  {code} — {name}");

        return new ToolResult { Name = Name, Success = true, Content = sb.ToString() };
    }

    private async Task<ToolResult> DetectLanguageAsync(JsonElement root, CancellationToken ct)
    {
        var text = GetRequiredString(root, "text");
        if (string.IsNullOrEmpty(text)) return Fail("缺少 text 参数");

        // 简单的启发式语言检测
        var detected = DetectLanguage(text);

        return new ToolResult
        {
            Name = Name,
            Success = true,
            Content = $"检测结果：{LanguageNames.GetValueOrDefault(detected, detected)}（{detected}）"
        };
    }

    private static string DetectLanguage(string text)
    {
        if (Regex.IsMatch(text, @"[\u4e00-\u9fff]")) return "zh";
        if (Regex.IsMatch(text, @"[\u3040-\u309f\u30a0-\u30ff]")) return "ja";
        if (Regex.IsMatch(text, @"[\uac00-\ud7af]")) return "ko";
        if (Regex.IsMatch(text, @"[\u0600-\u06ff]")) return "ar";
        if (Regex.IsMatch(text, @"[\u0e00-\u0e7f]")) return "th";
        if (Regex.IsMatch(text, @"[\u0900-\u097f]")) return "hi";
        if (Regex.IsMatch(text, @"[\u0400-\u04ff]")) return "ru";
        return "en";
    }

    private static string? GetRequiredString(JsonElement root, string name)
    {
        return root.TryGetProperty(name, out var el) ? el.GetString() : null;
    }

    private static string EscapeJson(string text) =>
        text.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n");

    private static ToolResult Fail(string error) => new() { Name = "translate", Success = false, Error = error };
}
