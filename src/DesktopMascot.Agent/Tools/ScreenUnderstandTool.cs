using System.Text.Json;
using System.Text.Json.Serialization;
using DesktopMascot.Agent.Context;
using DesktopMascot.Agent.Models;
using DesktopMascot.Agent.Providers;

namespace DesktopMascot.Agent.Tools;

/// <summary>
/// 屏幕理解工具 - 截取指定区域并用视觉 LLM 理解内容
/// </summary>
public class ScreenUnderstandTool : ITool
{
    private readonly IContextProvider _contextProvider;
    private readonly ILlmProvider _llmProvider;

    public ScreenUnderstandTool(IContextProvider contextProvider, ILlmProvider llmProvider)
    {
        _contextProvider = contextProvider;
        _llmProvider = llmProvider;
    }

    public string Name => "screen_understand";
    public string Description => "截取屏幕指定区域，识别并理解内容，判断用户意图并给出建议。支持全屏或指定区域（x, y, width, height）。";
    public string ParametersSchema => """
    {
        "type": "object",
        "properties": {
            "region": {
                "type": "object",
                "description": "截取区域（可选，不传则截全屏）",
                "properties": {
                    "x": { "type": "integer", "description": "左上角 X 坐标" },
                    "y": { "type": "integer", "description": "左上角 Y 坐标" },
                    "width": { "type": "integer", "description": "宽度" },
                    "height": { "type": "integer", "description": "高度" }
                }
            },
            "user_hint": {
                "type": "string",
                "description": "用户的补充说明（可选）"
            },
            "content_type": {
                "type": "string",
                "description": "内容类型提示（可选：code/error/document/webpage/ui/data/terminal/chat）"
            }
        }
    }
    """;

    public async Task<ToolResult> ExecuteAsync(string arguments, CancellationToken ct = default)
    {
        try
        {
            ScreenRegion? region = null;
            string? userHint = null;
            ScreenContentType? contentType = null;

            if (!string.IsNullOrEmpty(arguments) && arguments != "{}")
            {
                var doc = JsonDocument.Parse(arguments);
                if (doc.RootElement.TryGetProperty("region", out var regionElement))
                {
                    region = new ScreenRegion
                    {
                        X = regionElement.GetProperty("x").GetInt32(),
                        Y = regionElement.GetProperty("y").GetInt32(),
                        Width = regionElement.GetProperty("width").GetInt32(),
                        Height = regionElement.GetProperty("height").GetInt32()
                    };
                }
                if (doc.RootElement.TryGetProperty("user_hint", out var hintElement))
                {
                    userHint = hintElement.GetString();
                }
                if (doc.RootElement.TryGetProperty("content_type", out var ctElement))
                {
                    var ctStr = ctElement.GetString()?.ToLower();
                    if (Enum.TryParse<ScreenContentType>(ctStr, true, out var parsedCt))
                    {
                        contentType = parsedCt;
                    }
                }
            }

            // 获取当前窗口信息用于内容类型检测
            var snapshot = await _contextProvider.GetActiveWindowContextAsync(ct);
            if (contentType == null)
            {
                contentType = ScreenPromptBuilder.DetectContentType(snapshot.ActiveWindowTitle, snapshot.ActiveApplication);
            }

            // 截图
            var screenshotPath = await _contextProvider.CaptureScreenshotAsync(ct: ct);
            if (string.IsNullOrEmpty(screenshotPath) || screenshotPath.StartsWith("["))
            {
                return new ToolResult
                {
                    Name = Name,
                    Success = false,
                    Error = "截图失败"
                };
            }

            var imageBytes = await File.ReadAllBytesAsync(screenshotPath, ct);
            var base64 = Convert.ToBase64String(imageBytes);

            // 生成专门的 prompt
            var systemPrompt = ScreenPromptBuilder.BuildPrompt(contentType.Value, userHint);
            var userPrompt = BuildUserPrompt(region, userHint, snapshot);

            var messages = new List<LlmMessage>
            {
                new() { Role = "system", Content = systemPrompt },
                new() { Role = "user", Content = userPrompt, Images = new List<VisionContent>
                {
                    new() { Base64Data = base64, MediaType = "image/png" }
                }}
            };

            var response = await _llmProvider.ChatAsync(messages, null, ct);
            if (!response.Success)
            {
                return new ToolResult
                {
                    Name = Name,
                    Success = false,
                    Error = response.Error ?? "视觉 LLM 调用失败"
                };
            }

            var result = ParseResponse(response.Content, userHint, contentType.Value);

            return new ToolResult
            {
                Name = Name,
                Success = true,
                Content = JsonSerializer.Serialize(result, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                })
            };
        }
        catch (Exception ex)
        {
            return new ToolResult
            {
                Name = Name,
                Success = false,
                Error = $"屏幕理解失败: {ex.Message}"
            };
        }
    }

    private static string BuildUserPrompt(ScreenRegion? region, string? userHint, ContextSnapshot snapshot)
    {
        var prompt = $"请分析这个屏幕区域的内容。\n\n当前窗口：{snapshot.ActiveWindowTitle}（{snapshot.ActiveApplication}）";

        if (region != null)
        {
            prompt += $"\n\n截取区域：位置({region.X}, {region.Y})，大小({region.Width}x{region.Height})";
        }
        else
        {
            prompt += "\n\n这是全屏截图。";
        }

        if (!string.IsNullOrEmpty(userHint))
        {
            prompt += $"\n\n用户补充说明：{userHint}";
        }

        prompt += "\n\n请按三层结构（识别→理解→行动）分析并返回 JSON。";

        return prompt;
    }

    private static EnhancedScreenResult ParseResponse(string content, string? userHint, ScreenContentType contentType)
    {
        try
        {
            var jsonStart = content.IndexOf('{');
            var jsonEnd = content.LastIndexOf('}');
            if (jsonStart >= 0 && jsonEnd > jsonStart)
            {
                var json = content.Substring(jsonStart, jsonEnd - jsonStart + 1);
                var parsed = JsonSerializer.Deserialize<JsonElement>(json);

                var result = new EnhancedScreenResult
                {
                    Identification = parsed.TryGetProperty("identification", out var id) ? id.GetString() ?? "" : "",
                    Understanding = parsed.TryGetProperty("understanding", out var ud) ? ud.GetString() ?? "" : "",
                    UserIntent = parsed.TryGetProperty("userIntent", out var ui) ? ui.GetString() : null,
                    ContentType = contentType,
                    ExtractedText = parsed.TryGetProperty("extractedText", out var et) ? et.GetString() : null,
                    NeedsAction = parsed.TryGetProperty("needsAction", out var na) && na.GetBoolean(),
                    Confidence = parsed.TryGetProperty("confidence", out var cf) ? cf.GetSingle() : 0.5f,
                    RawResponse = content
                };

                if (parsed.TryGetProperty("suggestions", out var sug) && sug.ValueKind == JsonValueKind.Array)
                {
                    foreach (var s in sug.EnumerateArray())
                        result.Suggestions.Add(s.GetString() ?? "");
                }

                if (parsed.TryGetProperty("recommendedActions", out var acts) && acts.ValueKind == JsonValueKind.Array)
                {
                    foreach (var act in acts.EnumerateArray())
                    {
                        result.RecommendedActions.Add(new ScreenAction
                        {
                            Name = act.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "",
                            Description = act.TryGetProperty("description", out var d) ? d.GetString() ?? "" : "",
                            ActionType = act.TryGetProperty("actionType", out var at) ? at.GetString() ?? "" : "",
                            RiskLevel = act.TryGetProperty("riskLevel", out var rl) ? rl.GetString() ?? "low" : "low"
                        });
                    }
                }

                if (parsed.TryGetProperty("keyElements", out var ke) && ke.ValueKind == JsonValueKind.Array)
                {
                    foreach (var k in ke.EnumerateArray())
                        result.KeyElements.Add(k.GetString() ?? "");
                }

                if (parsed.TryGetProperty("errorType", out var et2))
                    result.ErrorType = et2.GetString();
                if (parsed.TryGetProperty("errorCode", out var ec))
                    result.ErrorCode = ec.GetString();
                if (parsed.TryGetProperty("errorMessage", out var em))
                    result.ErrorMessage = em.GetString();

                if (string.IsNullOrEmpty(result.Understanding) && !string.IsNullOrEmpty(userHint))
                {
                    result.UserIntent = userHint;
                }

                return result;
            }
        }
        catch { }

        return new EnhancedScreenResult
        {
            Identification = "无法解析",
            Understanding = content,
            UserIntent = userHint,
            ContentType = contentType,
            RawResponse = content,
            Confidence = 0.3f
        };
    }
}

/// <summary>
/// 屏幕区域
/// </summary>
public class ScreenRegion
{
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
}
