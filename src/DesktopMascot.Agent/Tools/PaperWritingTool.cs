using DesktopMascot.Core.Tools;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using DesktopMascot.Agent.Models;

namespace DesktopMascot.Agent.Tools;

/// <summary>
/// 论文写作工具 — 大纲生成、初稿撰写、降AI率、润色、格式化
/// </summary>
public class PaperWritingTool : ITool
{
    private readonly ITool _noteGenerator;

    public string Name => "paper_writing";
    public string Description => "论文写作：大纲生成、初稿撰写、降AI率、学术润色、引用格式、摘要生成。";
    public string ParametersSchema => """
    {
        "type": "object",
        "properties": {
            "action": { "type": "string", "enum": ["outline", "draft", "deai", "polish", "abstract", "cite", "structure_check", "paraphrase"], "description": "操作类型" },
            "title": { "type": "string", "description": "论文/章节标题" },
            "content": { "type": "string", "description": "输入文本/大纲/内容" },
            "topic": { "type": "string", "description": "研究主题" },
            "style": { "type": "string", "enum": ["academic", "technical", "narrative"], "description": "写作风格" },
            "word_count": { "type": "integer", "description": "目标字数" },
            "language": { "type": "string", "description": "输出语言（zh/en）" },
            "citation_format": { "type": "string", "enum": ["apa", "mla", "gb", "ieee"], "description": "引用格式" },
            "output_path": { "type": "string", "description": "输出文件路径" }
        },
        "required": ["action"]
    }
    """;

    public PaperWritingTool(ITool noteGenerator)
    {
        _noteGenerator = noteGenerator;
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
                "outline" => GenerateOutline(root),
                "draft" => GenerateDraft(root),
                "deai" => DeaiRate(root),
                "polish" => PolishText(root),
                "abstract" => GenerateAbstract(root),
                "cite" => GenerateCitation(root),
                "structure_check" => StructureCheck(root),
                "paraphrase" => ParaphraseText(root),
                _ => Fail($"不支持的操作：{action}")
            };
        }
        catch (Exception ex)
        {
            return Fail($"论文写作失败：{ex.Message}");
        }
    }

    #region 大纲生成

    private ToolResult GenerateOutline(JsonElement root)
    {
        var title = GetRequiredString(root, "title") ?? "";
        var topic = root.TryGetProperty("topic", out var tpEl) ? tpEl.GetString() ?? "" : "";
        var wordCount = root.TryGetProperty("word_count", out var wcEl) ? wcEl.GetInt32() : 5000;
        var style = root.TryGetProperty("style", out var sEl) ? sEl.GetString() ?? "academic" : "academic";

        if (string.IsNullOrEmpty(title)) return Fail("缺少 title 参数");

        var sectionCount = Math.Max(4, wordCount / 1000);
        var sectionWordCount = wordCount / sectionCount;

        var sb = new StringBuilder();
        sb.AppendLine($"# {title}");
        sb.AppendLine();
        sb.AppendLine($"论文类型：{(style == "academic" ? "学术论文" : style == "technical" ? "技术报告" : "综述文章")}");
        sb.AppendLine($"目标字数：约 {wordCount} 字");
        sb.AppendLine($"预计章节：{sectionCount} 节，每节约 {sectionWordCount} 字");
        sb.AppendLine();
        sb.AppendLine("═══ 论文大纲 ═══");
        sb.AppendLine();

        // 标准学术论文结构
        sb.AppendLine("## 摘要（Abstract）");
        sb.AppendLine("  - 研究背景（1-2句）");
        sb.AppendLine("  - 研究目的（1句）");
        sb.AppendLine("  - 研究方法（1-2句）");
        sb.AppendLine("  - 主要结果（1-2句）");
        sb.AppendLine("  - 结论（1句）");
        sb.AppendLine();

        sb.AppendLine("## 1. 引言（Introduction）");
        sb.AppendLine("  1.1 研究背景与意义");
        sb.AppendLine("      - 领域现状");
        sb.AppendLine("      - 存在的问题");
        sb.AppendLine("      - 研究的必要性");
        sb.AppendLine("  1.2 国内外研究现状");
        sb.AppendLine("      - 国内研究进展");
        sb.AppendLine("      - 国外研究进展");
        sb.AppendLine("      - 现有方法的不足");
        sb.AppendLine("  1.3 研究内容与创新点");
        sb.AppendLine("      - 本文主要贡献");
        sb.AppendLine("      - 技术路线");
        sb.AppendLine();

        sb.AppendLine("## 2. 相关工作（Related Work）");
        sb.AppendLine("  2.1 理论基础");
        sb.AppendLine("  2.2 关键技术");
        sb.AppendLine("  2.3 现有方法对比分析");
        sb.AppendLine();

        sb.AppendLine("## 3. 方法（Methodology）");
        sb.AppendLine("  3.1 问题定义");
        sb.AppendLine("  3.2 方法概述");
        sb.AppendLine("  3.3 核心算法/模型");
        sb.AppendLine("  3.4 实现细节");
        sb.AppendLine();

        sb.AppendLine("## 4. 实验（Experiments）");
        sb.AppendLine("  4.1 实验环境");
        sb.AppendLine("  4.2 数据集");
        sb.AppendLine("  4.3 评估指标");
        sb.AppendLine("  4.4 对比实验");
        sb.AppendLine("  4.5 消融实验");
        sb.AppendLine("  4.6 结果分析");
        sb.AppendLine();

        sb.AppendLine("## 5. 结论（Conclusion）");
        sb.AppendLine("  5.1 主要贡献");
        sb.AppendLine("  5.2 局限性");
        sb.AppendLine("  5.3 未来工作");
        sb.AppendLine();

        sb.AppendLine("## 参考文献");
        sb.AppendLine();

        if (!string.IsNullOrEmpty(topic))
        {
            sb.AppendLine("═══ 写作提示 ═══");
            sb.AppendLine();
            sb.AppendLine($"主题：{topic}");
            sb.AppendLine("建议：");
            sb.AppendLine("  1. 先完成文献综述，了解领域现状");
            sb.AppendLine("  2. 方法部分配图说明算法流程");
            sb.AppendLine("  3. 实验部分用表格对比结果");
            sb.AppendLine("  4. 引用近3年的高水平论文");
        }

        return new ToolResult { Name = Name, Success = true, Content = sb.ToString() };
    }

    #endregion

    #region 初稿撰写

    private ToolResult GenerateDraft(JsonElement root)
    {
        var title = GetRequiredString(root, "title") ?? "";
        var content = GetRequiredString(root, "content") ?? "";
        var wordCount = root.TryGetProperty("word_count", out var wcEl) ? wcEl.GetInt32() : 2000;
        var style = root.TryGetProperty("style", out var sEl) ? sEl.GetString() ?? "academic" : "academic";

        if (string.IsNullOrEmpty(title)) return Fail("缺少 title 参数");

        var sb = new StringBuilder();
        sb.AppendLine($"# {title}");
        sb.AppendLine();

        // 根据内容生成对应章节
        if (string.IsNullOrEmpty(content))
        {
            // 纯大纲模式——生成各章节的写作提示
            sb.AppendLine("═══ 写作指南 ═══");
            sb.AppendLine();
            sb.AppendLine("## 摘要写作模板");
            sb.AppendLine();
            sb.AppendLine("随着{领域}的快速发展，{问题}已成为当前研究的热点。");
            sb.AppendLine("本文针对{具体问题}，提出了一种基于{方法}的解决方案。");
            sb.AppendLine("通过{实验方法}，在{数据集}上进行了验证。");
            sb.AppendLine("实验结果表明，本文方法在{指标}上达到了{数值}，");
            sb.AppendLine("相比基线方法提升了{百分比}。");
            sb.AppendLine();

            sb.AppendLine("## 引言写作要点");
            sb.AppendLine();
            sb.AppendLine("第一段（背景）：");
            sb.AppendLine("  - 从宏观到微观，逐步聚焦研究问题");
            sb.AppendLine("  - 引用2-3篇高影响力论文");
            sb.AppendLine("  - 指出现有方法的不足");
            sb.AppendLine();
            sb.AppendLine("第二段（现状）：");
            sb.AppendLine("  - 按时间线或方法分类综述");
            sb.AppendLine("  - 用表格对比不同方法的优缺点");
            sb.AppendLine("  - 指出研究空白（gap）");
            sb.AppendLine();
            sb.AppendLine("第三段（贡献）：");
            sb.AppendLine("  - 明确列出3个创新点");
            sb.AppendLine("  - 说明每个创新点解决了什么问题");
            sb.AppendLine();

            sb.AppendLine("## 方法写作要点");
            sb.AppendLine();
            sb.AppendLine("  - 先给出整体框架图");
            sb.AppendLine("  - 每个模块配独立小节");
            sb.AppendLine("  - 给出数学公式（用LaTeX格式）");
            sb.AppendLine("  - 复杂步骤配伪代码");
            sb.AppendLine();
        }
        else
        {
            // 有内容——生成初稿段落
            sb.AppendLine("═══ 初稿内容 ═══");
            sb.AppendLine();
            var paragraphs = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            foreach (var para in paragraphs)
            {
                sb.AppendLine(para.Trim());
                sb.AppendLine();
            }
        }

        return new ToolResult { Name = Name, Success = true, Content = sb.ToString() };
    }

    #endregion

    #region 降AI率

    private ToolResult DeaiRate(JsonElement root)
    {
        var content = GetRequiredString(root, "content") ?? "";
        if (string.IsNullOrEmpty(content)) return Fail("缺少 content 参数");

        var sb = new StringBuilder();
        sb.AppendLine("═══ 降AI率策略 ═══");
        sb.AppendLine();
        sb.AppendLine("检测到的 AI 痕迹模式及修改建议：");
        sb.AppendLine();

        var issues = new List<(string Pattern, string Suggestion)>();

        // 常见AI模式检测
        var aiPatterns = new Dictionary<string, string>
        {
            ["首先.*其次.*最后"] = "拆分为独立段落，每段用不同句式开头",
            ["值得注意的是"] = "删除或改为具体描述",
            ["总的来说"] = "改为「综合以上分析」或直接进入结论",
            ["综上所述"] = "改为「基于上述实验结果」",
            ["不难发现"] = "改为「实验数据显示」或「观察表明」",
            ["毫无疑问"] = "删除，学术论文中避免绝对化表述",
            ["众所周知"] = "改为引用具体文献，如「研究表明[1]」",
            ["非常重要"] = "改为量化描述，如「该指标直接影响模型精度达30%」",
            ["大量的"] = "改为具体数字，如「约5000条数据」",
            ["广泛应用于"] = "引用具体应用案例",
            ["取得了显著的成果"] = "用具体数据替换，如「准确率提升至95.2%」",
            ["具有重要的理论意义和实践价值"] = "删除空话，直接说明具体贡献",
            ["随着.*的不断发展"] = "改为具体时间节点+事件，如「自2020年GPT-3发布以来」",
            ["近年来"] = "改为具体年份范围，如「2022-2024年间」",
            ["在当今社会"] = "删除，直接进入主题",
        };

        foreach (var (pattern, suggestion) in aiPatterns)
        {
            if (Regex.IsMatch(content, pattern))
            {
                issues.Add((pattern, suggestion));
                sb.AppendLine($"  ⚠ 检测到：「{pattern}」");
                sb.AppendLine($"    → 建议：{suggestion}");
                sb.AppendLine();
            }
        }

        if (issues.Count == 0)
        {
            sb.AppendLine("  ✅ 未检测到常见 AI 痕迹模式");
            sb.AppendLine();
            sb.AppendLine("通用降AI率技巧：");
            sb.AppendLine("  1. 句式多样化：交替使用主动/被动语态");
            sb.AppendLine("  2. 加入个人见解：添加「笔者认为」「从实践角度看」等主观表述");
            sb.AppendLine("  3. 引用具体数据：用数字替代模糊描述");
            sb.AppendLine("  4. 口语化过渡：适当使用「实际上」「有趣的是」");
            sb.AppendLine("  5. 段落长度变化：避免每段都差不多长");
            sb.AppendLine("  6. 添加不完美：适当加入「虽然...但仍存在...」");
        }
        else
        {
            sb.AppendLine($"═══ 共检测到 {issues.Count} 处 AI 痕迹 ═══");
            sb.AppendLine();
            sb.AppendLine("通用降AI率技巧：");
            sb.AppendLine("  1. 句式多样化：交替使用主动/被动语态");
            sb.AppendLine("  2. 加入个人见解：添加「笔者认为」「从实践角度看」等");
            sb.AppendLine("  3. 引用具体数据：用数字替代模糊描述");
            sb.AppendLine("  4. 段落长度变化：避免每段都差不多长");
        }

        return new ToolResult { Name = Name, Success = true, Content = sb.ToString() };
    }

    #endregion

    #region 润色

    private ToolResult PolishText(JsonElement root)
    {
        var content = GetRequiredString(root, "content") ?? "";
        var style = root.TryGetProperty("style", out var sEl) ? sEl.GetString() ?? "academic" : "academic";

        if (string.IsNullOrEmpty(content)) return Fail("缺少 content 参数");

        var sb = new StringBuilder();
        sb.AppendLine("═══ 学术润色建议 ═══");
        sb.AppendLine();

        // 检查常见学术写作问题
        var polishIssues = new List<string>();

        // 1. 检查口语化表达
        var colloquialPatterns = new[] { "我觉得", "我认为", "你", "我们", "他们", "超级", "很", "非常", "特别" };
        foreach (var pattern in colloquialPatterns)
        {
            if (content.Contains(pattern))
                polishIssues.Add($"口语化表达「{pattern}」→ 学术化替换");
        }

        // 2. 检查标点
        if (content.Contains("。。") || content.Contains("，，"))
            polishIssues.Add("重复标点");

        // 3. 检查中英文混用
        if (Regex.IsMatch(content, @"[\u4e00-\u9fff][a-zA-Z]|[a-zA-Z][\u4e00-\u9fff]"))
            polishIssues.Add("中英文之间建议加空格");

        // 4. 检查数字格式
        if (Regex.IsMatch(content, @"\d{4,}"))
            polishIssues.Add("大数字建议用逗号分隔（如1,000）");

        // 5. 检查引用
        if (!content.Contains("[") && !content.Contains("参考"))
            polishIssues.Add("未发现引用标注，学术论文需要引用");

        if (polishIssues.Count > 0)
        {
            sb.AppendLine("发现以下可改进之处：");
            foreach (var issue in polishIssues)
                sb.AppendLine($"  ⚠ {issue}");
        }
        else
        {
            sb.AppendLine("✅ 文本质量良好，未发现明显问题");
        }

        sb.AppendLine();
        sb.AppendLine("通用润色建议：");
        sb.AppendLine("  1. 学术用语：「很多」→「大量/众多」，「做」→「执行/实施」");
        sb.AppendLine("  2. 句式结构：长短交替，避免连续短句");
        sb.AppendLine("  3. 逻辑连接：使用「因此/然而/此外/相比之下」等连接词");
        sb.AppendLine("  4. 数据支撑：关键论点配数据或引用");
        sb.AppendLine("  5. 段落主题：每段首句点明主题");

        return new ToolResult { Name = Name, Success = true, Content = sb.ToString() };
    }

    #endregion

    #region 摘要生成

    private ToolResult GenerateAbstract(JsonElement root)
    {
        var title = GetRequiredString(root, "title") ?? "";
        var topic = root.TryGetProperty("topic", out var tpEl) ? tpEl.GetString() ?? "" : "";
        var content = GetRequiredString(root, "content") ?? "";
        var wordCount = root.TryGetProperty("word_count", out var wcEl) ? wcEl.GetInt32() : 300;

        if (string.IsNullOrEmpty(title)) return Fail("缺少 title 参数");

        var sb = new StringBuilder();
        sb.AppendLine("═══ 学术摘要模板 ═══");
        sb.AppendLine();
        sb.AppendLine("**【中文摘要】**");
        sb.AppendLine();
        sb.AppendLine($"【背景】随着{topic}领域的快速发展，相关研究日益受到关注。");
        sb.AppendLine($"本文针对{title}展开研究。");
        sb.AppendLine();
        sb.AppendLine($"【方法】本文提出了一种基于{{方法}}的解决方案，通过{{具体方法}}实现{{目标}}。");
        sb.AppendLine();
        sb.AppendLine($"【结果】实验在{{数据集}}上进行验证，结果表明{{关键指标}}达到了{{数值}}，");
        sb.AppendLine($"较基线方法提升了{{百分比}}。");
        sb.AppendLine();
        sb.AppendLine($"【结论】本研究为{{领域}}提供了新的思路，具有一定的理论和实践意义。");
        sb.AppendLine();
        sb.AppendLine($"**【关键词】** {{关键词1}}；{{关键词2}}；{{关键词3}}；{{关键词4}}；{{关键词5}}");
        sb.AppendLine();
        sb.AppendLine("═══ 摘要写作要点 ═══");
        sb.AppendLine();
        sb.AppendLine("  1. 字数：中文200-300字，英文150-250词");
        sb.AppendLine("  2. 结构：背景→方法→结果→结论（四段式）");
        sb.AppendLine("  3. 时态：背景用一般现在时，方法和结果用一般过去时");
        sb.AppendLine("  4. 避免：引用文献编号、缩写（首次出现除外）、图表编号");
        sb.AppendLine("  5. 关键词：3-5个，覆盖研究领域、方法、应用");

        return new ToolResult { Name = Name, Success = true, Content = sb.ToString() };
    }

    #endregion

    #region 引用格式

    private ToolResult GenerateCitation(JsonElement root)
    {
        var format = root.TryGetProperty("citation_format", out var cfEl) ? cfEl.GetString() ?? "gb" : "gb";
        var content = GetRequiredString(root, "content") ?? "";

        var sb = new StringBuilder();
        sb.AppendLine($"═══ {format.ToUpper()} 引用格式参考 ═══");
        sb.AppendLine();

        switch (format.ToLower())
        {
            case "gb":
                sb.AppendLine("GB/T 7714 格式（中文论文标准）：");
                sb.AppendLine();
                sb.AppendLine("  [期刊] 作者.题名[J].刊名,年,卷(期):起止页码.");
                sb.AppendLine("  示例：张三,李四.深度学习在图像识别中的应用[J].计算机学报,2024,47(1):123-135.");
                sb.AppendLine();
                sb.AppendLine("  [会议] 作者.题名[C]//会议名.出版地:出版者,年:起止页码.");
                sb.AppendLine();
                sb.AppendLine("  [学位论文] 作者.题名[D].城市:学校,年.");
                sb.AppendLine();
                sb.AppendLine("  [专著] 作者.书名[M].出版地:出版者,年.");
                sb.AppendLine();
                sb.AppendLine("  [网络] 作者.题名[EB/OL].(发布日期)[引用日期].URL.");
                break;
            case "apa":
                sb.AppendLine("APA 格式：");
                sb.AppendLine();
                sb.AppendLine("  [期刊] Author, A. A., & Author, B. B. (Year). Title. Journal, volume(issue), pages.");
                sb.AppendLine();
                sb.AppendLine("  [专著] Author, A. A. (Year). Title of work. Publisher.");
                sb.AppendLine();
                sb.AppendLine("  [会议] Author, A. A. (Year). Title of paper. Conference Name, pages.");
                break;
            case "mla":
                sb.AppendLine("MLA 格式：");
                sb.AppendLine();
                sb.AppendLine("  Author. \"Title.\" Journal, vol. #, no. #, Year, pp. #-#.");
                break;
            case "ieee":
                sb.AppendLine("IEEE 格式：");
                sb.AppendLine();
                sb.AppendLine("  [1] A. Author, \"Title,\" Journal, vol. #, no. #, pp. #-#, Year.");
                break;
        }

        if (!string.IsNullOrEmpty(content))
        {
            sb.AppendLine();
            sb.AppendLine("你的引用内容：");
            sb.AppendLine(content);
        }

        return new ToolResult { Name = Name, Success = true, Content = sb.ToString() };
    }

    #endregion

    #region 结构检查

    private ToolResult StructureCheck(JsonElement root)
    {
        var content = GetRequiredString(root, "content") ?? "";
        if (string.IsNullOrEmpty(content)) return Fail("缺少 content 参数");

        var sb = new StringBuilder();
        sb.AppendLine("═══ 论文结构检查 ═══");
        sb.AppendLine();

        var issues = new List<string>();

        // 检查必要章节
        var requiredSections = new[] { "摘要", "引言", "方法", "实验", "结论", "参考文献" };
        foreach (var section in requiredSections)
        {
            if (!content.Contains(section))
                issues.Add($"缺少必要章节：{section}");
        }

        // 检查章节编号
        var sectionNumbers = Regex.Matches(content, @"^#{1,3}\s*\d+[\.\s]", RegexOptions.Multiline);
        if (sectionNumbers.Count < 3)
            issues.Add("章节编号不规范（建议使用 1. / 1.1 / 1.1.1 格式）");

        // 检查引用
        var citations = Regex.Matches(content, @"\[\d+\]");
        if (citations.Count < 3)
            issues.Add($"引用数量偏少（当前 {citations.Count} 处，建议至少 10 处）");

        // 检查图表
        if (!content.Contains("图") && !content.Contains("表") && !content.Contains("Figure") && !content.Contains("Table"))
            issues.Add("未发现图表引用（建议添加数据图表）");

        // 检查公式
        if (!content.Contains("$") && !content.Contains("\\") && !content.Contains("公式"))
            issues.Add("未发现数学公式（学术论文通常需要公式支撑）");

        if (issues.Count == 0)
        {
            sb.AppendLine("✅ 论文结构完整");
        }
        else
        {
            sb.AppendLine($"发现 {issues.Count} 个结构问题：");
            foreach (var issue in issues)
                sb.AppendLine($"  ⚠ {issue}");
        }

        // 统计信息
        var wordCount = content.Length;
        var paragraphs = content.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length;
        sb.AppendLine();
        sb.AppendLine($"字数统计：{wordCount} 字");
        sb.AppendLine($"段落数：{paragraphs}");
        sb.AppendLine($"引用数：{citations.Count}");

        return new ToolResult { Name = Name, Success = true, Content = sb.ToString() };
    }

    #endregion

    #region 改写

    private ToolResult ParaphraseText(JsonElement root)
    {
        var content = GetRequiredString(root, "content") ?? "";
        if (string.IsNullOrEmpty(content)) return Fail("缺少 content 参数");

        var sb = new StringBuilder();
        sb.AppendLine("═══ 学术改写策略 ═══");
        sb.AppendLine();
        sb.AppendLine("原文：");
        sb.AppendLine(content);
        sb.AppendLine();
        sb.AppendLine("改写策略：");
        sb.AppendLine("  1. 同义替换：将关键词替换为同义词/近义词");
        sb.AppendLine("  2. 句式重组：主被动互换、长短句调整");
        sb.AppendLine("  3. 拆分合并：长句拆短、短句合并");
        sb.AppendLine("  4. 增删细节：添加限定词、删除冗余修饰");
        sb.AppendLine("  5. 逻辑重组：调整信息呈现顺序");
        sb.AppendLine();
        sb.AppendLine("提示：将原文传给 AI 助手，使用以下指令改写：");
        sb.AppendLine("  「请用学术语言改写以下段落，保持原意，改变句式结构，");
        sb.AppendLine("    替换同义词，确保查重率低于15%」");

        return new ToolResult { Name = Name, Success = true, Content = sb.ToString() };
    }

    #endregion

    #region Helpers

    private static string? GetRequiredString(JsonElement root, string name)
    {
        return root.TryGetProperty(name, out var el) ? el.GetString() : null;
    }

    private static ToolResult Fail(string error) => new() { Name = "paper_writing", Success = false, Error = error };

    #endregion
}
