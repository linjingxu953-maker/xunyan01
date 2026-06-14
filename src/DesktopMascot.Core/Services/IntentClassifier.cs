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
/// </summary>
public static class IntentClassifier
{
    private static readonly Dictionary<string, IntentPattern> IntentPatterns = new();

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
            if (lowerInput.Contains(kvp.Key))
            {
                if (kvp.Value.Confidence > bestConfidence)
                {
                    bestIntent = kvp.Value.Intent;
                    bestConfidence = kvp.Value.Confidence;
                    bestContext = kvp.Value.Context;
                    bestTools = kvp.Value.Tools;
                }
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
    /// <summary>组装任务上下文（基础版本，不含浏览器/剪贴板）</summary>
    public static Dictionary<string, object> AssembleBaseContext(IntentClassification intent)
    {
        var context = new Dictionary<string, object>();
        // 基础上下文由调用者提供
        return context;
    }
}
