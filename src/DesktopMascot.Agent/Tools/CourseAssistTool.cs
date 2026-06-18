using DesktopMascot.Core.Tools;
using System.Text;
using System.Text.Json;
using DesktopMascot.Agent.Models;

namespace DesktopMascot.Agent.Tools;

/// <summary>
/// 网课辅助工具 — 检测网课平台、自动刷课、智能答题、进度记录
/// </summary>
public class CourseAssistTool : ITool
{
    private readonly ITool _screenUnderstand;
    private readonly ITool _computerUse;

    public string Name => "course_assist";
    public string Description => "网课辅助：检测网课平台、自动刷课、智能答题、课程进度记录。支持超星学习通、智慧树、中国大学MOOC等。";
    public string ParametersSchema => """
    {
        "type": "object",
        "properties": {
            "action": { "type": "string", "enum": ["detect", "answer", "skip", "next", "progress", "batch_answer", "screenshot_answer"], "description": "操作类型" },
            "platform": { "type": "string", "enum": ["auto", "chaoxing", "zhihuishu", "mooc", "bilibili", "other"], "description": "网课平台" },
            "context": { "type": "string", "description": "屏幕截图路径或文本上下文" },
            "question": { "type": "string", "description": "题目文本（直接传入）" },
            "answer": { "type": "string", "description": "指定答案（跳过AI分析时使用）" },
            "auto_mode": { "type": "boolean", "description": "全自动模式（自动答题+下一题）" },
            "delay_ms": { "type": "integer", "description": "操作间隔毫秒数（默认2000）" },
            "max_questions": { "type": "integer", "description": "最大答题数（防止无限循环）" },
            "output_file": { "type": "string", "description": "答题结果输出文件" }
        },
        "required": ["action"]
    }
    """;

    public CourseAssistTool(ITool screenUnderstand, ITool computerUse)
    {
        _screenUnderstand = screenUnderstand;
        _computerUse = computerUse;
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
                "detect" => await DetectPlatformAsync(root, ct),
                "answer" => await AnswerQuestionAsync(root, ct),
                "skip" => await SkipQuestionAsync(ct),
                "next" => await NextQuestionAsync(ct),
                "progress" => await GetProgressAsync(root, ct),
                "batch_answer" => await BatchAnswerAsync(root, ct),
                "screenshot_answer" => await ScreenshotAnswerAsync(root, ct),
                _ => Fail($"不支持的操作：{action}")
            };
        }
        catch (Exception ex)
        {
            return Fail($"网课辅助失败：{ex.Message}");
        }
    }

    #region 平台检测

    private async Task<ToolResult> DetectPlatformAsync(JsonElement root, CancellationToken ct)
    {
        // 截取当前窗口
        var screenshotResult = await _screenUnderstand.ExecuteAsync("""{"action":"capture"}""", ct);
        if (!screenshotResult.Success)
            return Fail($"截图失败：{screenshotResult.Error}");

        var sb = new StringBuilder();
        sb.AppendLine("网课平台检测");
        sb.AppendLine();

        // 检测已知平台特征
        var platforms = new Dictionary<string, string[]>
        {
            ["chaoxing"] = new[] { "超星", "学习通", "chaoxing", "xinghuo", "考试", "视频观看" },
            ["zhihuishu"] = new[] { "智慧树", "zhihuishu", "知到", "见面课", "回放" },
            ["mooc"] = new[] { "中国大学MOOC", "icourse163", "慕课", "课程讨论" },
            ["bilibili"] = new[] { "bilibili", "B站", "慕课", "课堂" },
        };

        var detectedPlatform = "unknown";
        var windowTitle = screenshotResult.Content;

        foreach (var (platform, keywords) in platforms)
        {
            foreach (var keyword in keywords)
            {
                if (windowTitle.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                {
                    detectedPlatform = platform;
                    sb.AppendLine($"检测到平台：{platform}");
                    break;
                }
            }
            if (detectedPlatform != "unknown") break;
        }

        if (detectedPlatform == "unknown")
        {
            sb.AppendLine("未检测到已知网课平台");
            sb.AppendLine("当前窗口内容预览：");
            sb.AppendLine($"  {windowTitle[..Math.Min(200, windowTitle.Length)]}...");
        }

        sb.AppendLine();
        sb.AppendLine("支持的平台：超星学习通、智慧树、中国大学MOOC、B站课堂");

        return new ToolResult { Name = Name, Success = true, Content = sb.ToString() };
    }

    #endregion

    #region 答题

    private async Task<ToolResult> AnswerQuestionAsync(JsonElement root, CancellationToken ct)
    {
        var question = root.TryGetProperty("question", out var qEl) ? qEl.GetString() ?? "" : "";
        var context = root.TryGetProperty("context", out var cEl) ? cEl.GetString() : null;

        if (string.IsNullOrEmpty(question))
        {
            // 从屏幕获取题目
            var screenshotResult = await _screenUnderstand.ExecuteAsync("""{"action":"capture"}""", ct);
            if (!screenshotResult.Success)
                return Fail($"截图失败：{screenshotResult.Error}");
            question = screenshotResult.Content;
        }

        var sb = new StringBuilder();
        sb.AppendLine("答题分析");
        sb.AppendLine($"题目/上下文：{question[..Math.Min(300, question.Length)]}");
        sb.AppendLine();
        sb.AppendLine("提示：此工具为 AI 辅助分析，实际答题需要 LLM 配合。");
        sb.AppendLine("建议流程：");
        sb.AppendLine("  1. 截图当前题目");
        sb.AppendLine("  2. 将截图传给 LLM 分析答案");
        sb.AppendLine("  3. 使用 computer_use 模拟点击选项");

        return new ToolResult { Name = Name, Success = true, Content = sb.ToString() };
    }

    private async Task<ToolResult> ScreenshotAnswerAsync(JsonElement root, CancellationToken ct)
    {
        var delayMs = root.TryGetProperty("delay_ms", out var dEl) ? dEl.GetInt32() : 2000;

        // 截图
        var screenshotResult = await _screenUnderstand.ExecuteAsync("""{"action":"capture"}""", ct);
        if (!screenshotResult.Success)
            return Fail($"截图失败：{screenshotResult.Error}");

        // 截取选区理解
        var understandResult = await _screenUnderstand.ExecuteAsync(
            """{"action":"understand","hint":"这是一个网课题目，需要识别题目内容和选项"}""", ct);

        var sb = new StringBuilder();
        sb.AppendLine("截图答题模式");
        sb.AppendLine();
        sb.AppendLine("当前屏幕内容：");
        sb.AppendLine(understandResult.Content);

        return new ToolResult { Name = Name, Success = true, Content = sb.ToString() };
    }

    #endregion

    #region 操作

    private async Task<ToolResult> SkipQuestionAsync(CancellationToken ct)
    {
        // 尝试点击"下一题"或"跳过"
        var clickResult = await _computerUse.ExecuteAsync(
            """{"action":"type","keys":"Tab,Tab,Enter"}""", ct);

        await Task.Delay(500, ct);

        return new ToolResult
        {
            Name = Name,
            Success = clickResult.Success,
            Content = clickResult.Success ? "已尝试跳过/下一题" : $"操作失败：{clickResult.Error}"
        };
    }

    private async Task<ToolResult> NextQuestionAsync(CancellationToken ct)
    {
        // 点击下一题按钮
        var clickResult = await _computerUse.ExecuteAsync(
            """{"action":"type","keys":"Tab,Enter"}""", ct);

        await Task.Delay(1000, ct);

        return new ToolResult
        {
            Name = Name,
            Success = clickResult.Success,
            Content = clickResult.Success ? "已切换到下一题" : $"操作失败：{clickResult.Error}"
        };
    }

    #endregion

    #region 批量答题

    private async Task<ToolResult> BatchAnswerAsync(JsonElement root, CancellationToken ct)
    {
        var autoMode = root.TryGetProperty("auto_mode", out var amEl) && amEl.GetBoolean();
        var delayMs = root.TryGetProperty("delay_ms", out var dEl) ? dEl.GetInt32() : 2000;
        var maxQuestions = root.TryGetProperty("max_questions", out var mqEl) ? mqEl.GetInt32() : 50;

        var sb = new StringBuilder();
        sb.AppendLine($"批量答题模式");
        sb.AppendLine($"自动模式：{(autoMode ? "是" : "否")}");
        sb.AppendLine($"操作间隔：{delayMs}ms");
        sb.AppendLine($"最大答题数：{maxQuestions}");
        sb.AppendLine();
        sb.AppendLine("批量答题流程：");
        sb.AppendLine("  1. 截图当前题目");
        sb.AppendLine("  2. 传给 LLM 分析答案和选项");
        sb.AppendLine("  3. 模拟点击正确选项");
        sb.AppendLine("  4. 等待后进入下一题");
        sb.AppendLine("  5. 重复直到完成或达到上限");

        if (!autoMode)
        {
            sb.AppendLine();
            sb.AppendLine("提示：设置 auto_mode=true 启用全自动模式");
        }

        return new ToolResult { Name = Name, Success = true, Content = sb.ToString() };
    }

    #endregion

    #region 进度

    private async Task<ToolResult> GetProgressAsync(JsonElement root, CancellationToken ct)
    {
        var outputFile = root.TryGetProperty("output_file", out var ofEl) ? ofEl.GetString() : null;

        var sb = new StringBuilder();
        sb.AppendLine("课程进度");

        if (!string.IsNullOrEmpty(outputFile) && File.Exists(outputFile))
        {
            var content = await File.ReadAllTextAsync(outputFile, ct);
            var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            var answered = lines.Count(l => l.Contains("正确") || l.Contains("wrong"));
            var total = lines.Length;

            sb.AppendLine($"已答题：{total} 题");
            sb.AppendLine($"文件：{outputFile}");
            sb.AppendLine();
            sb.AppendLine("最近记录：");
            foreach (var line in lines.TakeLast(5))
                sb.AppendLine($"  {line}");
        }
        else
        {
            sb.AppendLine("暂无答题记录");
            if (!string.IsNullOrEmpty(outputFile))
                sb.AppendLine($"记录文件：{outputFile}");
        }

        return new ToolResult { Name = Name, Success = true, Content = sb.ToString() };
    }

    #endregion

    private static ToolResult Fail(string error) => new() { Name = "course_assist", Success = false, Error = error };
}
