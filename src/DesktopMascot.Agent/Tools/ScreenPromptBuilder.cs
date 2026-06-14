using DesktopMascot.Agent.Models;

namespace DesktopMascot.Agent.Tools;

/// <summary>
/// 屏幕内容类型检测和 Prompt 生成器
/// </summary>
public static class ScreenPromptBuilder
{
    public static string BuildPrompt(ScreenContentType contentType, string? userHint = null)
    {
        var typePrompt = GetTypeSpecificPrompt(contentType);

        var prompt = @"你是一个专业的屏幕内容分析助手。你的任务是：
1. 识别屏幕上显示的内容
2. 理解用户可能的意图
3. 给出具体可执行的建议

__TYPE_PROMPT__

请严格按以下 JSON 格式返回：
{
  ""identification"": ""这是什么内容"",
  ""understanding"": ""用户可能想做什么"",
  ""userIntent"": ""如果无法理解用户意图，返回用户的原始输入或空字符串"",
  ""contentType"": ""__CONTENT_TYPE__"",
  ""extractedText"": ""提取的关键文字"",
  ""suggestions"": [""建议1"", ""建议2""],
  ""needsAction"": true,
  ""recommendedActions"": [
    {
      ""name"": ""操作名称"",
      ""description"": ""操作描述"",
      ""actionType"": ""read_file/run_command/open_url/copy_text/click/type"",
      ""parameters"": {},
      ""riskLevel"": ""low""
    }
  ],
  ""keyElements"": [""关键元素1"", ""关键元素2""],
  ""confidence"": 0.8
}

重要规则：
1. 不要猜测，如果不确定返回 userIntent
2. identification 要具体
3. suggestions 要可操作
4. riskLevel 要准确评估"
            .Replace("__TYPE_PROMPT__", typePrompt, StringComparison.Ordinal)
            .Replace("__CONTENT_TYPE__", contentType.ToString().ToLowerInvariant(), StringComparison.Ordinal);

        if (!string.IsNullOrEmpty(userHint))
        {
            prompt += $"\n\n用户补充说明：{userHint}";
        }

        return prompt;
    }

    private static string GetTypeSpecificPrompt(ScreenContentType contentType)
    {
        return contentType switch
        {
            ScreenContentType.Code => "当前是代码。重点：语言、功能、潜在问题、优化建议。",
            ScreenContentType.Error => "当前是错误信息。重点：错误类型、原因、修复步骤。",
            ScreenContentType.Document => "当前是文档。重点：类型、结构、关键信息。",
            ScreenContentType.WebPage => "当前是网页。重点：网站类型、主要内容、可交互元素。",
            ScreenContentType.UI => "当前是应用界面。重点：应用类型、可用功能、当前状态。",
            ScreenContentType.Data => "当前是数据。重点：数据类型、关键数据、趋势。",
            ScreenContentType.Terminal => "当前是终端。重点：当前目录、命令输出、下一步。",
            _ => "请仔细分析屏幕内容，识别类型、关键元素、用户意图。"
        };
    }

    public static ScreenContentType DetectContentType(string? windowTitle, string? appName)
    {
        var title = windowTitle?.ToLower() ?? "";
        var app = appName?.ToLower() ?? "";

        if (app.Contains("terminal") || app.Contains("cmd") || app.Contains("powershell") || app.Contains("console"))
            return ScreenContentType.Terminal;
        if (app.Contains("code") || app.Contains("visual studio") || app.Contains("rider") || app.Contains("idea"))
            return ScreenContentType.Code;
        if (app.Contains("chrome") || app.Contains("firefox") || app.Contains("edge") || app.Contains("browser"))
            return ScreenContentType.WebPage;
        if (app.Contains("word") || app.Contains("excel") || app.Contains("powerpoint") || app.Contains("document"))
            return ScreenContentType.Document;
        if (title.Contains("error") || title.Contains("exception") || title.Contains("错误") || title.Contains("异常"))
            return ScreenContentType.Error;
        if (title.Contains("data") || title.Contains("chart") || title.Contains("数据") || title.Contains("图表"))
            return ScreenContentType.Data;
        if (title.Contains("chat") || title.Contains("message") || title.Contains("聊天") || title.Contains("消息"))
            return ScreenContentType.Chat;
        return ScreenContentType.Unknown;
    }
}
