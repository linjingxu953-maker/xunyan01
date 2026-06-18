using DesktopMascot.Core.Tools;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using DesktopMascot.Agent.Models;

namespace DesktopMascot.Agent.Tools;

/// <summary>
/// 笔记生成器 — 从网页/文档/代码生成结构化 Markdown 笔记
/// </summary>
public class NoteGeneratorTool : ITool
{
    private readonly ITool _browserContext;
    private readonly ITool _screenUnderstand;

    public string Name => "note_generator";
    public string Description => "笔记生成：从网页/文档/代码/屏幕内容生成结构化 Markdown 笔记。支持大纲/摘要/学习笔记/会议纪要。";
    public string ParametersSchema => """
    {
        "type": "object",
        "properties": {
            "action": { "type": "string", "enum": ["from_screen", "from_text", "from_code", "from_url", "from_file", "format"], "description": "操作类型" },
            "content": { "type": "string", "description": "输入内容" },
            "title": { "type": "string", "description": "笔记标题" },
            "template": { "type": "string", "enum": ["outline", "summary", "study", "meeting", "code_review", "custom"], "description": "笔记模板" },
            "output_path": { "type": "string", "description": "输出文件路径" },
            "language": { "type": "string", "description": "输出语言" }
        },
        "required": ["action"]
    }
    """;

    public NoteGeneratorTool(ITool browserContext, ITool screenUnderstand)
    {
        _browserContext = browserContext;
        _screenUnderstand = screenUnderstand;
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
                "from_screen" => await FromScreenAsync(root, ct),
                "from_text" => FromText(root),
                "from_code" => FromCode(root),
                "from_url" => await FromUrlAsync(root, ct),
                "from_file" => await FromFileAsync(root, ct),
                "format" => FormatNote(root),
                _ => Fail($"不支持的操作：{action}")
            };
        }
        catch (Exception ex)
        {
            return Fail($"笔记生成失败：{ex.Message}");
        }
    }

    private async Task<ToolResult> FromScreenAsync(JsonElement root, CancellationToken ct)
    {
        var title = root.TryGetProperty("title", out var tEl) ? tEl.GetString() ?? "屏幕笔记" : "屏幕笔记";
        var template = root.TryGetProperty("template", out var tmpEl) ? tmpEl.GetString() ?? "summary" : "summary";

        // 截屏并获取内容
        var contextResult = await _browserContext.ExecuteAsync("""{"action":"get_context"}""", ct);
        var understandResult = await _screenUnderstand.ExecuteAsync(
            """{"action":"understand","hint":"提取当前屏幕的主要内容、关键信息和要点"}""", ct);

        var content = understandResult.Content;
        var note = GenerateNote(title, content, template);
        var sb = new StringBuilder();
        sb.AppendLine("✅ 笔记已从屏幕内容生成");
        sb.AppendLine();
        sb.AppendLine(note);

        return new ToolResult { Name = Name, Success = true, Content = sb.ToString() };
    }

    private ToolResult FromText(JsonElement root)
    {
        var title = root.TryGetProperty("title", out var tEl) ? tEl.GetString() ?? "文本笔记" : "文本笔记";
        var content = root.TryGetProperty("content", out var cEl) ? cEl.GetString() ?? "" : "";
        var template = root.TryGetProperty("template", out var tmpEl) ? tmpEl.GetString() ?? "summary" : "summary";

        if (string.IsNullOrEmpty(content)) return Fail("缺少 content 参数");

        var note = GenerateNote(title, content, template);

        var sb = new StringBuilder();
        sb.AppendLine("✅ 笔记已生成");
        sb.AppendLine();
        sb.AppendLine(note);

        return new ToolResult { Name = Name, Success = true, Content = sb.ToString() };
    }

    private ToolResult FromCode(JsonElement root)
    {
        var title = root.TryGetProperty("title", out var tEl) ? tEl.GetString() ?? "代码笔记" : "代码笔记";
        var content = root.TryGetProperty("content", out var cEl) ? cEl.GetString() ?? "" : "";

        if (string.IsNullOrEmpty(content)) return Fail("缺少 content 参数");

        var sb = new StringBuilder();
        sb.AppendLine($"# {title}");
        sb.AppendLine();
        sb.AppendLine($"生成时间：{DateTime.Now:yyyy-MM-dd HH:mm}");
        sb.AppendLine();

        // 提取类名
        var classes = Regex.Matches(content, @"(?:public|internal|private)?\s*(?:partial\s+)?(?:class|interface|struct|enum)\s+(\w+)");
        if (classes.Count > 0)
        {
            sb.AppendLine("## 类/接口列表");
            foreach (Match m in classes)
                sb.AppendLine($"- `{m.Groups[1].Value}`");
            sb.AppendLine();
        }

        // 提取方法签名
        var methods = Regex.Matches(content, @"(?:public|private|protected|internal)\s+(?:static\s+)?(?:async\s+)?(?:[\w<>\[\]?,\s]+)\s+(\w+)\s*\([^)]*\)");
        if (methods.Count > 0)
        {
            sb.AppendLine("## 方法列表");
            foreach (Match m in methods)
                sb.AppendLine($"- `{m.Groups[0].Value.Trim()}`");
            sb.AppendLine();
        }

        // 提取注释
        var comments = Regex.Matches(content, @"///\s*(.+)");
        if (comments.Count > 0)
        {
            sb.AppendLine("## 文档注释");
            foreach (Match m in comments.Take(20))
                sb.AppendLine($"- {m.Groups[1].Value.Trim()}");
            sb.AppendLine();
        }

        // 统计
        var lines = content.Split('\n');
        sb.AppendLine("## 代码统计");
        sb.AppendLine($"- 总行数：{lines.Length}");
        sb.AppendLine($"- 类/接口数：{classes.Count}");
        sb.AppendLine($"- 方法数：{methods.Count}");
        sb.AppendLine($"- 注释数：{comments.Count}");

        return new ToolResult { Name = Name, Success = true, Content = sb.ToString() };
    }

    private async Task<ToolResult> FromUrlAsync(JsonElement root, CancellationToken ct)
    {
        var title = root.TryGetProperty("title", out var tEl) ? tEl.GetString() ?? "网页笔记" : "网页笔记";
        var template = root.TryGetProperty("template", out var tmpEl) ? tmpEl.GetString() ?? "summary" : "summary";

        // 从浏览器获取当前页面内容
        var contextResult = await _browserContext.ExecuteAsync("""{"action":"get_context"}""", ct);
        if (!contextResult.Success)
            return Fail($"获取网页内容失败：{contextResult.Error}");

        var content = contextResult.Content;
        var note = GenerateNote(title, content, template);

        var sb = new StringBuilder();
        sb.AppendLine("✅ 笔记已从网页生成");
        sb.AppendLine();
        sb.AppendLine(note);

        return new ToolResult { Name = Name, Success = true, Content = sb.ToString() };
    }

    private async Task<ToolResult> FromFileAsync(JsonElement root, CancellationToken ct)
    {
        var title = root.TryGetProperty("title", out var tEl) ? tEl.GetString() ?? "文件笔记" : "文件笔记";
        var filePath = root.TryGetProperty("content", out var cEl) ? cEl.GetString() ?? "" : "";
        var template = root.TryGetProperty("template", out var tmpEl) ? tmpEl.GetString() ?? "summary" : "summary";

        if (string.IsNullOrEmpty(filePath)) return Fail("缺少 content 参数（文件路径）");
        if (!File.Exists(filePath)) return Fail($"文件不存在：{filePath}");

        var content = await File.ReadAllTextAsync(filePath, ct);
        var fileName = Path.GetFileName(filePath);
        var note = GenerateNote(title, content, template);

        var sb = new StringBuilder();
        sb.AppendLine($"✅ 笔记已从文件生成：{fileName}");
        sb.AppendLine();
        sb.AppendLine(note);

        return new ToolResult { Name = Name, Success = true, Content = sb.ToString() };
    }

    private ToolResult FormatNote(JsonElement root)
    {
        var title = root.TryGetProperty("title", out var tEl) ? tEl.GetString() ?? "笔记" : "笔记";
        var content = root.TryGetProperty("content", out var cEl) ? cEl.GetString() ?? "" : "";
        var outputPath = root.TryGetProperty("output_path", out var oEl) ? oEl.GetString() : null;

        if (string.IsNullOrEmpty(content)) return Fail("缺少 content 参数");

        var note = $"# {title}\n\n> 生成时间：{DateTime.Now:yyyy-MM-dd HH:mm}\n\n{content}";

        if (!string.IsNullOrEmpty(outputPath))
        {
            var dir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(outputPath, note);
        }

        return new ToolResult { Name = Name, Success = true, Content = note };
    }

    #region 笔记模板

    private static string GenerateNote(string title, string content, string template)
    {
        return template switch
        {
            "outline" => GenerateOutlineNote(title, content),
            "summary" => GenerateSummaryNote(title, content),
            "study" => GenerateStudyNote(title, content),
            "meeting" => GenerateMeetingNote(title, content),
            "code_review" => GenerateCodeReviewNote(title, content),
            _ => GenerateSummaryNote(title, content)
        };
    }

    private static string GenerateOutlineNote(string title, string content)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# {title}");
        sb.AppendLine();
        sb.AppendLine($"生成时间：{DateTime.Now:yyyy-MM-dd HH:mm}");
        sb.AppendLine();
        sb.AppendLine("## 大纲");
        sb.AppendLine();

        var sentences = SplitIntoSentences(content);
        foreach (var sentence in sentences.Take(20))
        {
            var trimmed = sentence.Trim();
            if (trimmed.Length > 5)
                sb.AppendLine($"- {trimmed}");
        }

        if (sentences.Count > 20)
            sb.AppendLine($"- ... 共 {sentences.Count} 条要点");

        return sb.ToString();
    }

    private static string GenerateSummaryNote(string title, string content)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# {title} — 摘要");
        sb.AppendLine();
        sb.AppendLine($"生成时间：{DateTime.Now:yyyy-MM-dd HH:mm}");
        sb.AppendLine();

        // 提取关键句子
        var sentences = SplitIntoSentences(content);
        var important = sentences
            .Where(s => s.Trim().Length > 10)
            .Take(15)
            .ToList();

        sb.AppendLine("## 核心内容");
        sb.AppendLine();
        foreach (var s in important)
            sb.AppendLine($"> {s.Trim()}");
        sb.AppendLine();

        // 提取关键词
        var words = content.Split(new[] { ' ', '\n', '\t', ',', '.', '。', '，' },
            StringSplitOptions.RemoveEmptyEntries);
        var keywords = words
            .Where(w => w.Length >= 2 && !StopWords.Contains(w.ToLower()))
            .GroupBy(w => w.ToLower())
            .OrderByDescending(g => g.Count())
            .Take(10)
            .Select(g => g.Key)
            .ToList();

        if (keywords.Count > 0)
        {
            sb.AppendLine("## 关键词");
            sb.AppendLine();
            sb.AppendLine(string.Join(" | ", keywords));
        }

        return sb.ToString();
    }

    private static string GenerateStudyNote(string title, string content)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# {title} — 学习笔记");
        sb.AppendLine();
        sb.AppendLine($"日期：{DateTime.Now:yyyy-MM-dd HH:mm}");
        sb.AppendLine();

        sb.AppendLine("## 要点总结");
        sb.AppendLine();
        var sentences = SplitIntoSentences(content);
        foreach (var s in sentences.Take(10))
            sb.AppendLine($"- {s.Trim()}");
        sb.AppendLine();

        sb.AppendLine("## 重点难点");
        sb.AppendLine();
        sb.AppendLine("（请补充）");
        sb.AppendLine();

        sb.AppendLine("## 个人思考");
        sb.AppendLine();
        sb.AppendLine("（请补充）");
        sb.AppendLine();

        sb.AppendLine("## 相关链接");
        sb.AppendLine();
        var urls = Regex.Matches(content, @"https?://[^\s]+");
        foreach (Match url in urls.Take(5))
            sb.AppendLine($"- {url.Value}");

        return sb.ToString();
    }

    private static string GenerateMeetingNote(string title, string content)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# {title} — 会议纪要");
        sb.AppendLine();
        sb.AppendLine($"日期：{DateTime.Now:yyyy-MM-dd HH:mm}");
        sb.AppendLine();

        sb.AppendLine("## 会议内容");
        sb.AppendLine();
        var sentences = SplitIntoSentences(content);
        foreach (var s in sentences)
            sb.AppendLine($"- {s.Trim()}");
        sb.AppendLine();

        sb.AppendLine("## 待办事项");
        sb.AppendLine();
        sb.AppendLine("（请补充）");
        sb.AppendLine();

        sb.AppendLine("## 下次会议");
        sb.AppendLine();
        sb.AppendLine("（请补充）");

        return sb.ToString();
    }

    private static string GenerateCodeReviewNote(string title, string content)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# {title} — 代码审查");
        sb.AppendLine();
        sb.AppendLine($"日期：{DateTime.Now:yyyy-MM-dd HH:mm}");
        sb.AppendLine();

        sb.AppendLine("## 代码概览");
        sb.AppendLine();
        var sentences = SplitIntoSentences(content).Take(5);
        foreach (var s in sentences)
            sb.AppendLine($"- {s.Trim()}");
        sb.AppendLine();

        sb.AppendLine("## 潜在问题");
        sb.AppendLine();
        sb.AppendLine("（请补充）");
        sb.AppendLine();

        sb.AppendLine("## 改进建议");
        sb.AppendLine();
        sb.AppendLine("（请补充）");

        return sb.ToString();
    }

    #endregion

    #region Helpers

    private static List<string> SplitIntoSentences(string text)
    {
        return Regex.Split(text, @"[。！？\.\!\?\n]+")
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToList();
    }

    private static readonly HashSet<string> StopWords = new()
    {
        "的", "了", "在", "是", "我", "有", "和", "就", "不", "人", "都", "一", "一个",
        "上", "也", "很", "到", "说", "要", "去", "你", "会", "着", "没有", "看", "好",
        "the", "a", "an", "is", "are", "was", "were", "be", "been", "being",
        "have", "has", "had", "do", "does", "did", "will", "would", "could",
        "should", "may", "might", "can", "shall", "this", "that", "it",
        "and", "or", "but", "if", "then", "else", "when", "while", "for",
        "with", "to", "from", "by", "at", "in", "on", "of", "not"
    };

    private static ToolResult Fail(string error) => new() { Name = "note_generator", Success = false, Error = error };

    #endregion
}
