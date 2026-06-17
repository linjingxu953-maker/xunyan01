namespace DesktopMascot.Core.Services;

/// <summary>
/// 意图分类结果
/// </summary>
public class IntentClassification
{
    public string Intent { get; set; } = string.Empty;
    public float Confidence { get; set; }
    public Dictionary<string, string> Entities { get; set; } = new();
    public string[] RequiredContext { get; set; } = Array.Empty<string>();
    public string[] SuggestedTools { get; set; } = Array.Empty<string>();
}

/// <summary>
/// 意图分类器 - 分析用户输入，识别意图和实体
/// 改进版：支持否定检测，避免"不想总结"误匹配
/// </summary>
public static class IntentClassifier
{
    private static readonly Dictionary<string, IntentPattern> IntentPatterns = new();

    // 否定前缀模式 — 匹配 "不要/别/不用/不想/不需要/别帮我 + keyword" 的结构
    private static readonly System.Text.RegularExpressions.Regex NegationPrefixRegex = new(
        @"(?:不要|别|不用|不想|不需要|别帮我|不想要)\s*");

    static IntentClassifier()
    {
        IntentPatterns["总结"] = new IntentPattern("summarize_page", 0.9f, new[] { "browser", "screen" }, new[] { "browser_context", "screen_capture" });
        IntentPatterns["summarize"] = new IntentPattern("summarize_page", 0.9f, new[] { "browser", "screen" }, new[] { "browser_context", "screen_capture" });
        IntentPatterns["报错"] = new IntentPattern("analyze_error", 0.9f, new[] { "screen", "clipboard" }, new[] { "screen_capture", "clipboard" });
        IntentPatterns["错误"] = new IntentPattern("analyze_error", 0.85f, new[] { "screen", "clipboard" }, new[] { "screen_capture", "clipboard" });
        IntentPatterns["error"] = new IntentPattern("analyze_error", 0.85f, new[] { "screen", "clipboard" }, new[] { "screen_capture", "clipboard" });
        IntentPatterns["项目"] = new IntentPattern("inspect_project", 0.8f, new[] { "file_system" }, new[] { "list_directory", "read_file" });
        IntentPatterns["目录"] = new IntentPattern("inspect_project", 0.75f, new[] { "file_system" }, new[] { "list_directory" });
        IntentPatterns["写入"] = new IntentPattern("write_file", 0.85f, new[] { "file_system" }, new[] { "write_file" });
        IntentPatterns["生成文件"] = new IntentPattern("write_file", 0.9f, new[] { "file_system" }, new[] { "write_file" });
        IntentPatterns["执行命令"] = new IntentPattern("run_command", 0.9f, new[] { "system" }, new[] { "run_command" });
        IntentPatterns["运行命令"] = new IntentPattern("run_command", 0.85f, new[] { "system" }, new[] { "run_command" });
        IntentPatterns["记住"] = new IntentPattern("update_memory", 0.8f, new[] { "memory" }, new string[0]);
        IntentPatterns["记忆"] = new IntentPattern("update_memory", 0.75f, new[] { "memory" }, new string[0]);
        IntentPatterns["截图"] = new IntentPattern("screen_understand", 0.85f, new[] { "screen" }, new[] { "screen_capture", "screen_understand" });
        IntentPatterns["圈选"] = new IntentPattern("screen_understand", 0.9f, new[] { "screen" }, new[] { "screen_understand" });
        IntentPatterns["代码审查"] = new IntentPattern("workflow_code_review", 0.85f, new[] { "file_system" }, new[] { "read_file", "list_directory" });
        IntentPatterns["文件整理"] = new IntentPattern("workflow_file_org", 0.8f, new[] { "file_system" }, new[] { "list_directory", "write_file" });
        IntentPatterns["数据分析"] = new IntentPattern("workflow_data_analysis", 0.8f, new[] { "file_system" }, new[] { "read_file", "write_file" });

        // 新增：文件操作
        IntentPatterns["对比文件"] = new IntentPattern("file_compare", 0.85f, new[] { "file_system" }, new[] { "file_compare" });
        IntentPatterns["批量重命名"] = new IntentPattern("batch_file_process", 0.85f, new[] { "file_system" }, new[] { "batch_file_processor" });
        IntentPatterns["文件版本"] = new IntentPattern("file_version", 0.8f, new[] { "file_system" }, new[] { "file_version" });

        // 新增：代码/安全分析
        IntentPatterns["代码分析"] = new IntentPattern("code_analysis", 0.85f, new[] { "file_system" }, new[] { "code_analysis" });
        IntentPatterns["安全扫描"] = new IntentPattern("security_scan", 0.9f, new[] { "file_system" }, new[] { "security_scan" });
        IntentPatterns["性能分析"] = new IntentPattern("performance_analysis", 0.85f, new[] { "file_system" }, new[] { "performance_analysis" });

        // 新增：网络/数据库
        IntentPatterns["发请求"] = new IntentPattern("network_request", 0.8f, new[] { "network" }, new[] { "network_request" });
        IntentPatterns["API调用"] = new IntentPattern("network_request", 0.85f, new[] { "network" }, new[] { "network_request" });
        IntentPatterns["数据库"] = new IntentPattern("database_operation", 0.8f, new[] { "database" }, new[] { "database" });

        // 新增：日历/邮件
        IntentPatterns["日程"] = new IntentPattern("calendar", 0.8f, new[] { "calendar" }, new[] { "calendar" });
        IntentPatterns["提醒"] = new IntentPattern("notification", 0.75f, new[] { "notification" }, new[] { "notification" });
        IntentPatterns["发邮件"] = new IntentPattern("email", 0.85f, new[] { "email" }, new[] { "email" });

        // 新增：文件处理
        IntentPatterns["加密"] = new IntentPattern("file_encryption", 0.85f, new[] { "file_system" }, new[] { "file_encryption" });
        IntentPatterns["压缩图片"] = new IntentPattern("image_processing", 0.8f, new[] { "file_system" }, new[] { "image_processing" });
        IntentPatterns["转换格式"] = new IntentPattern("image_processing", 0.75f, new[] { "file_system" }, new[] { "image_processing" });

        // 新增：视频制作
        IntentPatterns["剪视频"] = new IntentPattern("video_processing", 0.85f, new[] { "media" }, new[] { "video_processing" });
        IntentPatterns["视频剪辑"] = new IntentPattern("video_processing", 0.9f, new[] { "media" }, new[] { "video_processing" });
        IntentPatterns["短视频"] = new IntentPattern("short_video", 0.9f, new[] { "media" }, new[] { "short_video_maker" });
        IntentPatterns["配音"] = new IntentPattern("short_video", 0.8f, new[] { "media" }, new[] { "short_video_maker", "text_to_speech" });
        IntentPatterns["视频转音频"] = new IntentPattern("extract_audio", 0.85f, new[] { "media" }, new[] { "video_processing" });

        // 新增：云存储
        IntentPatterns["云同步"] = new IntentPattern("cloud_sync", 0.8f, new[] { "cloud" }, new[] { "cloud_sync" });
        IntentPatterns["上传文件"] = new IntentPattern("cloud_sync", 0.75f, new[] { "cloud" }, new[] { "cloud_sync" });
    }

    /// <summary>分类用户意图</summary>
    public static IntentClassification Classify(string input)
    {
        var lowerInput = input.ToLowerInvariant();
        var bestIntent = "chat";
        var bestConfidence = 0.5f;
        var bestContext = Array.Empty<string>();
        var bestTools = Array.Empty<string>();

        foreach (var kvp in IntentPatterns)
        {
            var keyword = kvp.Key;
            var matchIndex = lowerInput.IndexOf(keyword, StringComparison.Ordinal);
            if (matchIndex < 0)
                continue;

            // 检查是否为否定模式：输入在关键词前的部分是否以否定前缀结尾
            if (matchIndex > 0)
            {
                var prefix = lowerInput[..matchIndex];
                var negationMatch = NegationPrefixRegex.Match(prefix);
                if (negationMatch.Success && negationMatch.Index + negationMatch.Length == prefix.Length)
                {
                    // 否定 + 关键词 → 跳过此意图
                    continue;
                }
            }

            if (kvp.Value.Confidence > bestConfidence)
            {
                bestIntent = kvp.Value.Intent;
                bestConfidence = kvp.Value.Confidence;
                bestContext = kvp.Value.Context;
                bestTools = kvp.Value.Tools;
            }
        }

        var entities = ExtractEntities(input);

        return new IntentClassification
        {
            Intent = bestIntent,
            Confidence = bestConfidence,
            Entities = entities,
            RequiredContext = bestContext,
            SuggestedTools = bestTools
        };
    }

    /// <summary>提取实体</summary>
    private static Dictionary<string, string> ExtractEntities(string input)
    {
        var entities = new Dictionary<string, string>();

        var pathMatch = System.Text.RegularExpressions.Regex.Match(input, @"[A-Za-z]:\\[^\s""<>]+|[./][^\s""<>]+");
        if (pathMatch.Success) entities["file_path"] = pathMatch.Value;

        var urlMatch = System.Text.RegularExpressions.Regex.Match(input, @"https?://[^\s""<>]+");
        if (urlMatch.Success) entities["url"] = urlMatch.Value;

        var numberMatch = System.Text.RegularExpressions.Regex.Match(input, @"\d+");
        if (numberMatch.Success) entities["number"] = numberMatch.Value;

        return entities;
    }
}

/// <summary>
/// 意图模式
/// </summary>
public class IntentPattern
{
    public string Intent { get; }
    public float Confidence { get; }
    public string[] Context { get; }
    public string[] Tools { get; }

    public IntentPattern(string intent, float confidence, string[] context, string[] tools)
    {
        Intent = intent;
        Confidence = confidence;
        Context = context;
        Tools = tools;
    }
}

/// <summary>
/// 上下文组装器 - 根据意图组装执行上下文
/// </summary>
public static class ContextAssembler
{
    /// <summary>组装任务执行上下文（基础版本）</summary>
    /// <param name="intent">意图分类结果</param>
    /// <returns>包含意图、实体及所需上下文标记的字典</returns>
    public static Dictionary<string, object> AssembleBaseContext(IntentClassification intent)
    {
        var context = new Dictionary<string, object>
        {
            ["intent"] = intent.Intent,
            ["confidence"] = intent.Confidence,
            ["required_context"] = intent.RequiredContext,
            ["suggested_tools"] = intent.SuggestedTools
        };

        foreach (var (key, value) in intent.Entities)
        {
            context[$"entity_{key}"] = value;
        }

        return context;
    }
}
