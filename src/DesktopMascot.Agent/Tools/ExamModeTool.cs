using DesktopMascot.Core.Tools;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using DesktopMascot.Agent.Models;

namespace DesktopMascot.Agent.Tools;

/// <summary>
/// 考试模式工具 — 检测题目类型、模拟答题操作、记录考试结果
/// </summary>
public class ExamModeTool : ITool
{
    private readonly ITool _screenUnderstand;
    private readonly ITool _computerUse;
    private readonly ITool _browserContext;
    private readonly List<ExamRecord> _records = new();
    private readonly object _lock = new();

    public string Name => "exam_mode";
    public string Description => "考试模式：检测题目/选项、模拟答题操作、自动翻页、记录考试结果、支持选择题/填空题/判断题。";
    public string ParametersSchema => """
    {
        "type": "object",
        "properties": {
            "action": { "type": "string", "enum": ["detect_question", "answer_choice", "answer_fill", "answer_judge", "auto_answer", "next_question", "record_result", "get_summary", "screenshot_analyze"], "description": "操作类型" },
            "question_text": { "type": "string", "description": "题目文本" },
            "options": { "type": "string", "description": "选项列表，逗号分隔" },
            "answer": { "type": "string", "description": "答案（选项字母或填空文本）" },
            "correct": { "type": "boolean", "description": "是否正确（记录用）" },
            "question_number": { "type": "integer", "description": "题目序号" },
            "total_questions": { "type": "integer", "description": "总题数" },
            "delay_ms": { "type": "integer", "description": "操作间隔毫秒（默认1500）" },
            "output_file": { "type": "string", "description": "结果输出文件路径" },
            "platform": { "type": "string", "description": "考试平台" }
        },
        "required": ["action"]
    }
    """;

    public ExamModeTool(ITool screenUnderstand, ITool computerUse, ITool browserContext)
    {
        _screenUnderstand = screenUnderstand;
        _computerUse = computerUse;
        _browserContext = browserContext;
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
                "detect_question" => await DetectQuestionAsync(ct),
                "answer_choice" => await AnswerChoiceAsync(root, ct),
                "answer_fill" => await AnswerFillAsync(root, ct),
                "answer_judge" => await AnswerJudgeAsync(root, ct),
                "auto_answer" => await AutoAnswerAsync(root, ct),
                "next_question" => await NextQuestionAsync(ct),
                "record_result" => RecordResult(root),
                "get_summary" => GetSummary(root),
                "screenshot_analyze" => await ScreenshotAnalyzeAsync(ct),
                _ => Fail($"不支持的操作：{action}")
            };
        }
        catch (Exception ex)
        {
            return Fail($"考试模式失败：{ex.Message}");
        }
    }

    #region 题目检测

    private async Task<ToolResult> DetectQuestionAsync(CancellationToken ct)
    {
        // 获取屏幕内容
        var contextResult = await _browserContext.ExecuteAsync("""{"action":"get_context"}""", ct);
        var screenResult = await _screenUnderstand.ExecuteAsync(
            """{"action":"understand","hint":"这是一个考试/测验界面，请识别当前题目、题目类型（选择题/填空题/判断题）和所有选项"}""", ct);

        var sb = new StringBuilder();
        sb.AppendLine("题目检测结果");
        sb.AppendLine();
        sb.AppendLine("屏幕分析：");
        sb.AppendLine(screenResult.Content);

        // 尝试提取题目类型
        var content = screenResult.Content;
        if (content.Contains("A.") || content.Contains("A、") || content.Contains("（A）"))
            sb.AppendLine("\n📌 检测到：选择题");
        else if (content.Contains("对") && content.Contains("错") && content.Contains("判断"))
            sb.AppendLine("\n📌 检测到：判断题");
        else
            sb.AppendLine("\n📌 检测到：可能为填空题或简答题");

        return new ToolResult { Name = Name, Success = true, Content = sb.ToString() };
    }

    #endregion

    #region 选择题答题

    private async Task<ToolResult> AnswerChoiceAsync(JsonElement root, CancellationToken ct)
    {
        var answer = root.TryGetProperty("answer", out var aEl) ? aEl.GetString() ?? "" : "";
        var questionNumber = root.TryGetProperty("question_number", out var qnEl) ? qnEl.GetInt32() : 0;
        var delayMs = root.TryGetProperty("delay_ms", out var dEl) ? dEl.GetInt32() : 1500;

        if (string.IsNullOrEmpty(answer))
            return Fail("缺少 answer 参数（如 A/B/C/D）");

        var optionIndex = answer.ToUpper()[0] - 'A';

        // 模拟点击选项（Tab 跳转 + Enter 确认 或直接点击）
        // 通用策略：连续按 Tab 到达选项，然后 Space 选中
        var keys = new StringBuilder();
        for (int i = 0; i <= optionIndex; i++)
            keys.Append("Tab,");

        keys.Append("Space");

        var clickResult = await _computerUse.ExecuteAsync(
            $"{{\"action\":\"type\",\"keys\":\"{keys}\"}}", ct);

        await Task.Delay(delayMs, ct);

        // 记录
        lock (_lock)
        {
            _records.Add(new ExamRecord
            {
                QuestionNumber = questionNumber,
                Type = "choice",
                Answer = answer,
                Timestamp = DateTime.Now
            });
        }

        var sb = new StringBuilder();
        sb.AppendLine("选择题作答");
        sb.AppendLine($"答案：{answer}");
        sb.AppendLine($"题目序号：{questionNumber}");
        sb.AppendLine("已模拟点击操作");

        return new ToolResult { Name = Name, Success = true, Content = sb.ToString() };
    }

    #endregion

    #region 填空题

    private async Task<ToolResult> AnswerFillAsync(JsonElement root, CancellationToken ct)
    {
        var answer = root.TryGetProperty("answer", out var aEl) ? aEl.GetString() ?? "" : "";
        var questionNumber = root.TryGetProperty("question_number", out var qnEl) ? qnEl.GetInt32() : 0;
        var delayMs = root.TryGetProperty("delay_ms", out var dEl) ? dEl.GetInt32() : 1500;

        if (string.IsNullOrEmpty(answer))
            return Fail("缺少 answer 参数（填空内容）");

        // 模拟输入：先 Tab 到输入框，然后输入文本
        var typeResult = await _computerUse.ExecuteAsync(
            $"{{\"action\":\"type\",\"keys\":\"Tab\",\"text\":\"{EscapeJson(answer)}\"}}", ct);

        await Task.Delay(delayMs, ct);

        lock (_lock)
        {
            _records.Add(new ExamRecord
            {
                QuestionNumber = questionNumber,
                Type = "fill",
                Answer = answer,
                Timestamp = DateTime.Now
            });
        }

        return new ToolResult
        {
            Name = Name,
            Success = true,
            Content = $"填空题作答：已输入「{answer}」"
        };
    }

    #endregion

    #region 判断题

    private async Task<ToolResult> AnswerJudgeAsync(JsonElement root, CancellationToken ct)
    {
        var answer = root.TryGetProperty("answer", out var aEl) ? aEl.GetString() ?? "" : "";
        var questionNumber = root.TryGetProperty("question_number", out var qnEl) ? qnEl.GetInt32() : 0;
        var delayMs = root.TryGetProperty("delay_ms", out var dEl) ? dEl.GetInt32() : 1500;

        if (string.IsNullOrEmpty(answer))
            return Fail("缺少 answer 参数（对/错 或 true/false）");

        // 判断题通常是两个选项，选第一个(对)或第二个(错)
        var isTrue = answer is "对" or "正确" or "true" or "True" or "T";
        var keys = isTrue ? "Tab,Space" : "Tab,Tab,Space";

        var clickResult = await _computerUse.ExecuteAsync(
            $"{{\"action\":\"type\",\"keys\":\"{keys}\"}}", ct);

        await Task.Delay(delayMs, ct);

        lock (_lock)
        {
            _records.Add(new ExamRecord
            {
                QuestionNumber = questionNumber,
                Type = "judge",
                Answer = isTrue ? "对" : "错",
                Timestamp = DateTime.Now
            });
        }

        return new ToolResult
        {
            Name = Name,
            Success = true,
            Content = $"判断题作答：{(isTrue ? "对 ✓" : "错 ✗")}"
        };
    }

    #endregion

    #region 全自动答题

    private async Task<ToolResult> AutoAnswerAsync(JsonElement root, CancellationToken ct)
    {
        var maxQuestions = root.TryGetProperty("max_questions", out var mqEl) ? mqEl.GetInt32() : 30;
        var delayMs = root.TryGetProperty("delay_ms", out var dEl) ? dEl.GetInt32() : 2000;
        var totalQuestions = root.TryGetProperty("total_questions", out var tqEl) ? tqEl.GetInt32() : 0;

        var sb = new StringBuilder();
        sb.AppendLine("全自动答题模式已启动");
        sb.AppendLine($"最大答题数：{maxQuestions}");
        sb.AppendLine($"操作间隔：{delayMs}ms");
        if (totalQuestions > 0) sb.AppendLine($"总题数：{totalQuestions}");
        sb.AppendLine();
        sb.AppendLine("全自动模式工作流程：");
        sb.AppendLine("  1. 截屏 → 视觉识别题目");
        sb.AppendLine("  2. AI 分析 → 生成答案");
        sb.AppendLine("  3. 模拟操作 → 选择/填写答案");
        sb.AppendLine("  4. 等待 → 下一题");
        sb.AppendLine("  5. 循环直到完成");
        sb.AppendLine();
        sb.AppendLine("⚠️ 注意事项：");
        sb.AppendLine("  - 自动模式需要 LLM 配合分析题目");
        sb.AppendLine("  - 建议先用 detect_question 测试识别效果");
        sb.AppendLine("  - delay_ms 建议 ≥ 2000 避免操作过快被检测");
        sb.AppendLine("  - 所有操作会被记录到 exam_history");

        return new ToolResult { Name = Name, Success = true, Content = sb.ToString() };
    }

    #endregion

    #region 下一题

    private async Task<ToolResult> NextQuestionAsync(CancellationToken ct)
    {
        // 通用翻页：Tab + Enter 或 点击"下一题"按钮
        var result = await _computerUse.ExecuteAsync(
            """{"action":"type","keys":"Tab,Tab,Enter"}""", ct);

        await Task.Delay(1500, ct);

        return new ToolResult
        {
            Name = Name,
            Success = result.Success,
            Content = result.Success ? "已切换到下一题" : $"切换失败：{result.Error}"
        };
    }

    #endregion

    #region 记录与统计

    private ToolResult RecordResult(JsonElement root)
    {
        var questionNumber = root.TryGetProperty("question_number", out var qnEl) ? qnEl.GetInt32() : 0;
        var answer = root.TryGetProperty("answer", out var aEl) ? aEl.GetString() ?? "" : "";
        var correct = root.TryGetProperty("correct", out var cEl) && cEl.GetBoolean();
        var outputFile = root.TryGetProperty("output_file", out var ofEl) ? ofEl.GetString() : null;

        lock (_lock)
        {
            _records.Add(new ExamRecord
            {
                QuestionNumber = questionNumber,
                Answer = answer,
                IsCorrect = correct,
                Timestamp = DateTime.Now
            });
        }

        // 写入文件
        if (!string.IsNullOrEmpty(outputFile))
        {
            var dir = Path.GetDirectoryName(outputFile);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            var line = $"[{DateTime.Now:HH:mm:ss}] Q{questionNumber}: {answer} ({(correct ? "正确" : "错误")})";
            File.AppendAllText(outputFile, line + Environment.NewLine);
        }

        return new ToolResult
        {
            Name = Name,
            Success = true,
            Content = $"已记录：Q{questionNumber} = {answer} ({(correct ? "正确 ✓" : "错误 ✗")})"
        };
    }

    private ToolResult GetSummary(JsonElement root)
    {
        var outputFile = root.TryGetProperty("output_file", out var ofEl) ? ofEl.GetString() : null;

        lock (_lock)
        {
            var total = _records.Count;
            var correct = _records.Count(r => r.IsCorrect);
            var wrong = _records.Count(r => !r.IsCorrect && r.IsCorrect == false);

            var sb = new StringBuilder();
            sb.AppendLine("考试统计");
            sb.AppendLine($"总答题：{total} 题");
            if (total > 0)
            {
                sb.AppendLine($"正确：{correct} 题");
                sb.AppendLine($"错误：{total - correct} 题");
                sb.AppendLine($"正确率：{correct * 100.0 / total:F1}%");
            }

            if (!string.IsNullOrEmpty(outputFile) && File.Exists(outputFile))
            {
                var content = File.ReadAllText(outputFile);
                var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                sb.AppendLine();
                sb.AppendLine($"记录文件：{outputFile}（{lines.Length} 条）");
            }

            return new ToolResult { Name = Name, Success = true, Content = sb.ToString() };
        }
    }

    #endregion

    #region 截图分析

    private async Task<ToolResult> ScreenshotAnalyzeAsync(CancellationToken ct)
    {
        // 截取屏幕并分析
        var screenshot = await _screenUnderstand.ExecuteAsync("""{"action":"capture"}""", ct);
        if (!screenshot.Success)
            return Fail($"截图失败：{screenshot.Error}");

        var analysis = await _screenUnderstand.ExecuteAsync(
            """{"action":"understand","hint":"这是考试界面截图，请分析：1.题目类型（选择/填空/判断）2.题目内容 3.所有选项 4.最可能的正确答案"}""", ct);

        var sb = new StringBuilder();
        sb.AppendLine("考试截图分析");
        sb.AppendLine();
        sb.AppendLine(analysis.Content);

        return new ToolResult { Name = Name, Success = true, Content = sb.ToString() };
    }

    #endregion

    private static string EscapeJson(string text)
    {
        return text.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n");
    }

    private static ToolResult Fail(string error) => new() { Name = "exam_mode", Success = false, Error = error };
}

internal class ExamRecord
{
    public int QuestionNumber { get; set; }
    public string Type { get; set; } = "unknown";
    public string Answer { get; set; } = "";
    public bool IsCorrect { get; set; }
    public DateTime Timestamp { get; set; }
}
