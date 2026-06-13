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
            }

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

            var systemPrompt = BuildSystemPrompt();
            var userPrompt = BuildUserPrompt(region, userHint);

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

            var result = ParseResponse(response.Content, userHint);

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

    private static string BuildSystemPrompt()
    {
        return """
            你是一个专业的屏幕内容分析助手。你的任务是分析用户截取的屏幕区域，理解内容并帮助用户。

            请按以下三层结构分析：

            ## 第一层：识别（这是什么）
            - 屏幕上显示的是什么内容
            - 什么应用、什么界面元素
            - 关键文字、图标、按钮

            ## 第二层：理解（用户可能想做什么）
            - 根据内容判断用户可能的意图
            - 如果无法确定意图，返回用户的原始输入
            - 不要猜测，宁可说"不确定"也不要瞎猜

            ## 第三层：行动（怎么帮用户）
            - 给出具体的建议操作
            - 如果需要执行操作，说明操作类型和参数
            - 评估操作风险

            请严格按以下 JSON 格式返回：
            ```json
            {
              "identification": "这是什么内容",
              "understanding": "用户可能想做什么",
              "userIntent": "如果无法理解，返回用户的原始输入",
              "suggestions": ["建议1", "建议2"],
              "needsAction": true/false,
              "recommendedActions": [
                {
                  "name": "操作名称",
                  "description": "操作描述",
                  "actionType": "read_file/run_command/open_url/copy_text",
                  "parameters": {},
                  "riskLevel": "low/medium/high"
                }
              ],
              "confidence": 0.8
            }
            ```
            """;
    }

    private static string BuildUserPrompt(ScreenRegion? region, string? userHint)
    {
        var prompt = "请分析这个屏幕区域的内容。";

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

    private static ScreenUnderstandResult ParseResponse(string content, string? userHint)
    {
        try
        {
            var jsonStart = content.IndexOf('{');
            var jsonEnd = content.LastIndexOf('}');
            if (jsonStart >= 0 && jsonEnd > jsonStart)
            {
                var json = content.Substring(jsonStart, jsonEnd - jsonStart + 1);
                var parsed = JsonSerializer.Deserialize<JsonElement>(json);

                var result = new ScreenUnderstandResult
                {
                    Identification = parsed.TryGetProperty("identification", out var id) ? id.GetString() ?? "" : "",
                    Understanding = parsed.TryGetProperty("understanding", out var ud) ? ud.GetString() ?? "" : "",
                    UserIntent = parsed.TryGetProperty("userIntent", out var ui) ? ui.GetString() : null,
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

                if (string.IsNullOrEmpty(result.Understanding) && !string.IsNullOrEmpty(userHint))
                {
                    result.UserIntent = userHint;
                }

                return result;
            }
        }
        catch { }

        return new ScreenUnderstandResult
        {
            Identification = "无法解析",
            Understanding = content,
            UserIntent = userHint,
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
