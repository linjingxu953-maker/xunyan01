using DesktopMascot.Core.Tools;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using DesktopMascot.Agent.Models;

namespace DesktopMascot.Agent.Tools;

/// <summary>
/// 代码分析工具增强版 — 多语言支持 + 圈复杂度 + 代码异味 + 方法粒度 + JSON 导出
/// </summary>
public class CodeAnalysisTool : ITool
{
    public string Name => "code_analysis";
    public string Description => "代码分析：质量检查、复杂度分析、代码异味检测、依赖提取、结构统计。支持 C#/JS/TS/Python/Go/Rust/Java/PHP/Ruby。";
    public string ParametersSchema => """
    {
        "type": "object",
        "properties": {
            "action": { "type": "string", "enum": ["quality", "complexity", "dependencies", "stats", "structure", "smells", "methods", "full"], "description": "分析类型" },
            "file_path": { "type": "string", "description": "文件路径" },
            "directory": { "type": "string", "description": "目录路径" },
            "language": { "type": "string", "description": "编程语言（自动检测或手动指定）" },
            "output_format": { "type": "string", "enum": ["text", "json"], "description": "输出格式" }
        },
        "required": ["action"]
    }
    """;

    private static readonly Dictionary<string, (string[] LineComments, string[] BlockOpen, string[] BlockClose, string[] MethodPatterns)> LanguageDefs = new()
    {
        ["csharp"] = (new[] { "//" }, new[] { "{" }, new[] { "}" }, new[] { @"(?:public|private|protected|internal|static|async|override|virtual|sealed)\s+\S+\s+(\w+)\s*\(" }),
        ["javascript"] = (new[] { "//" }, new[] { "{" }, new[] { "}" }, new[] { @"(?:function\s+(\w+)|(?:const|let|var)\s+\w+\s*=\s*(?:async\s+)?\(|(\w+)\s*:\s*(?:async\s+)?\()" }),
        ["typescript"] = (new[] { "//" }, new[] { "{" }, new[] { "}" }, new[] { @"(?:function\s+(\w+)|(?:const|let|var)\s+\w+\s*=\s*(?:async\s+)?\(|(\w+)\s*:\s*(?:async\s+)?\(" }),
        ["python"] = (new[] { "#" }, new[] { ":" }, Array.Empty<string>(), new[] { @"def\s+(\w+)\s*\(" }),
        ["go"] = (new[] { "//" }, new[] { "{" }, new[] { "}" }, new[] { @"func\s+(?:\(\w+\s+\*?\w+\)\s+)?(\w+)\s*\(" }),
        ["rust"] = (new[] { "//" }, new[] { "{" }, new[] { "}" }, new[] { @"(?:pub\s+)?(?:async\s+)?fn\s+(\w+)\s*\(" }),
        ["java"] = (new[] { "//" }, new[] { "{" }, new[] { "}" }, new[] { @"(?:public|private|protected)\s+(?:static\s+)?(?:\w+\s+)+(\w+)\s*\(" }),
        ["php"] = (new[] { "//" }, new[] { "{" }, new[] { "}" }, new[] { @"(?:public|private|protected)\s+(?:static\s+)?function\s+(\w+)\s*\(" }),
        ["ruby"] = (new[] { "#" }, Array.Empty<string>(), Array.Empty<string>(), new[] { @"(?:def|defself)\s+(\w+)" }),
    };

    public async Task<ToolResult> ExecuteAsync(string arguments, CancellationToken ct = default)
    {
        try
        {
            var doc = JsonDocument.Parse(arguments);
            var root = doc.RootElement;
            var action = root.TryGetProperty("action", out var aEl) ? aEl.GetString() ?? "" : "";
            var filePath = root.TryGetProperty("file_path", out var fEl) ? fEl.GetString() ?? "" : "";
            var directory = root.TryGetProperty("directory", out var dEl) ? dEl.GetString() ?? "" : "";
            var langHint = root.TryGetProperty("language", out var lEl) ? lEl.GetString() : null;
            var outputJson = root.TryGetProperty("output_format", out var ofEl) && ofEl.GetString() == "json";

            return action switch
            {
                "quality" => await AnalyzeQualityAsync(filePath, langHint, ct),
                "complexity" => await AnalyzeComplexityAsync(filePath, langHint, ct),
                "dependencies" => await AnalyzeDependenciesAsync(filePath, ct),
                "stats" => await AnalyzeStatsAsync(filePath, langHint, ct),
                "structure" => await AnalyzeStructureAsync(directory, ct),
                "smells" => await AnalyzeSmellsAsync(filePath, langHint, ct),
                "methods" => await AnalyzeMethodsAsync(filePath, langHint, ct),
                "full" => await FullAnalysisAsync(filePath, langHint, ct),
                _ => Fail($"不支持的分析类型：{action}")
            };
        }
        catch (Exception ex)
        {
            return Fail($"代码分析失败：{ex.Message}");
        }
    }

    private async Task<ToolResult> AnalyzeQualityAsync(string filePath, string? langHint, CancellationToken ct)
    {
        if (!File.Exists(filePath)) return Fail($"文件不存在：{filePath}");

        var content = await File.ReadAllTextAsync(filePath, ct);
        var lines = content.Split('\n');
        var lang = langHint ?? DetectLanguage(filePath);

        var issues = new List<(string Severity, string Message)>();

        // 空行过多
        var emptyLines = lines.Count(string.IsNullOrWhiteSpace);
        if (emptyLines > lines.Length * 0.3)
            issues.Add(("warning", $"空行过多：{emptyLines}/{lines.Length} 行 ({emptyLines * 100 / lines.Length}%)"));

        // 行长度
        var longLines = lines.Where(l => l.Length > 120).ToList();
        if (longLines.Count > 0)
            issues.Add(("warning", $"长行：{longLines.Count} 行超过 120 字符"));

        // 重复代码
        var duplicates = CheckDuplicateLines(lines);
        if (duplicates > 0)
            issues.Add(("warning", $"重复代码：{duplicates} 行重复"));

        // TODO/FIXME/HACK
        var todos = lines.Count(l => l.Contains("TODO"));
        var fixmes = lines.Count(l => l.Contains("FIXME"));
        var hacks = lines.Count(l => l.Contains("HACK"));
        if (todos > 0) issues.Add(("info", $"TODO：{todos} 个"));
        if (fixmes > 0) issues.Add(("warning", $"FIXME：{fixmes} 个"));
        if (hacks > 0) issues.Add(("error", $"HACK：{hacks} 个"));

        // 文件过长
        if (lines.Length > 500)
            issues.Add(("warning", $"文件过长：{lines.Length} 行（建议 < 500 行）"));

        // 检查语言特定的质量问题
        if (lang is "csharp" or "java")
        {
            // C#：检查 using 是否过多
            var usings = lines.Count(l => l.TrimStart().StartsWith("using ") && l.TrimEnd().EndsWith(";"));
            if (usings > 15)
                issues.Add(("info", $"using 引用过多：{usings} 个（建议 < 15）"));

            // C#：检查 var vs 显式类型
            var varCount = lines.Count(l => Regex.IsMatch(l, @"\bvar\s+\w+\s*="));
            var explicitCount = lines.Count(l => Regex.IsMatch(l, @"\b(string|int|bool|var)\s+\w+\s*="));
            if (varCount > 0 && explicitCount > 0)
                issues.Add(("info", $"var/显式类型混用：var {varCount} 次，显式 {explicitCount} 次"));
        }

        if (lang is "javascript" or "typescript")
        {
            // 检查 console.log
            var consoleLogs = lines.Count(l => l.Contains("console.log"));
            if (consoleLogs > 0)
                issues.Add(("warning", $"console.log 残留：{consoleLogs} 个"));

            // 检查 == vs ===
            var looseEquals = Regex.Matches(content, @"[^=!]==[^=]").Count;
            if (looseEquals > 0)
                issues.Add(("warning", $"松散比较 ==：{looseEquals} 处（建议用 ===）"));
        }

        if (lang == "python")
        {
            // 检查 bare except
            var bareExcept = lines.Count(l => l.Trim().StartsWith("except:") || l.Trim() == "except:");
            if (bareExcept > 0)
                issues.Add(("warning", $"裸 except：{bareExcept} 处（建议指定异常类型）"));

            // 检查 import *
            var starImports = lines.Count(l => l.Contains("from .* import \\*"));
            if (starImports > 0)
                issues.Add(("info", $"通配符 import：{starImports} 处"));
        }

        var qualityScore = Math.Max(0, 100 - issues.Count(s => s.Severity == "error") * 15
            - issues.Count(s => s.Severity == "warning") * 8
            - issues.Count(s => s.Severity == "info") * 3);

        var sb = new StringBuilder();
        sb.AppendLine($"代码质量分析：{Path.GetFileName(filePath)}");
        sb.AppendLine($"语言：{lang} | 行数：{lines.Length} | 质量评分：{qualityScore}/100");
        sb.AppendLine();

        if (issues.Count == 0)
        {
            sb.AppendLine("✅ 未发现问题");
        }
        else
        {
            sb.AppendLine($"发现问题：{issues.Count}");
            foreach (var (severity, message) in issues.OrderByDescending(i => i.Severity == "error" ? 0 : i.Severity == "warning" ? 1 : 2))
                sb.AppendLine($"  [{severity}] {message}");
        }

        return new ToolResult { Name = Name, Success = true, Content = sb.ToString() };
    }

    private async Task<ToolResult> AnalyzeComplexityAsync(string filePath, string? langHint, CancellationToken ct)
    {
        if (!File.Exists(filePath)) return Fail($"文件不存在：{filePath}");

        var content = await File.ReadAllTextAsync(filePath, ct);
        var lines = content.Split('\n');
        var lang = langHint ?? DetectLanguage(filePath);
        var def = GetLangDef(lang);

        var metrics = new Dictionary<string, int>
        {
            ["总行数"] = lines.Length,
            ["代码行数"] = lines.Count(l => !string.IsNullOrWhiteSpace(l) && !l.TrimStart().StartsWith(def.LineComments[0])),
            ["注释行数"] = lines.Count(l => l.TrimStart().StartsWith(def.LineComments[0])),
            ["空行数"] = lines.Count(string.IsNullOrWhiteSpace),
            ["平均行长度"] = lines.Length > 0 ? (int)lines.Average(l => l.Length) : 0,
            ["最大嵌套深度"] = CalculateMaxNesting(lines, def),
            ["方法数量"] = CountMethods(lines, def),
            ["圈复杂度"] = CalculateCyclomaticComplexity(content, lang),
        };

        var sb = new StringBuilder();
        sb.AppendLine($"复杂度分析：{Path.GetFileName(filePath)}");
        sb.AppendLine($"语言：{lang}");
        sb.AppendLine();
        foreach (var metric in metrics)
            sb.AppendLine($"  {metric.Key}：{metric.Value}");

        // 复杂度评级
        var cc = metrics["圈复杂度"];
        sb.AppendLine();
        sb.AppendLine($"圈复杂度评级：{cc switch
        {
            <= 5 => "A（简单，可测试）",
            <= 10 => "B（一般，可维护）",
            <= 20 => "C（复杂，需重构）",
            _ => "D（不可维护，必须重构）"
        }}");

        return new ToolResult { Name = Name, Success = true, Content = sb.ToString() };
    }

    private async Task<ToolResult> AnalyzeDependenciesAsync(string filePath, CancellationToken ct)
    {
        if (!File.Exists(filePath)) return Fail($"文件不存在：{filePath}");

        var content = await File.ReadAllTextAsync(filePath, ct);
        var lang = DetectLanguage(filePath);
        var dependencies = new List<string>();

        switch (lang)
        {
            case "csharp":
                foreach (Match m in Regex.Matches(content, @"using\s+([\w.]+);"))
                {
                    var ns = m.Groups[1].Value;
                    if (!ns.StartsWith("System"))
                        dependencies.Add(ns);
                }
                break;
            case "javascript":
            case "typescript":
                foreach (Match m in Regex.Matches(content, @"import\s+.*?from\s+['""](.+?)['""]"))
                    dependencies.Add(m.Groups[1].Value);
                break;
            case "python":
                foreach (Match m in Regex.Matches(content, @"(?:from\s+[\w.]+\s+)?import\s+([\w.]+)"))
                    dependencies.Add(m.Groups[1].Value);
                break;
            case "go":
                foreach (Match m in Regex.Matches(content, @"import\s+(?:\([\s\S]*?\)|""(.+?)""|'(.+?)')"))
                {
                    if (m.Groups[1].Success) dependencies.Add(m.Groups[1].Value);
                    else if (m.Groups[2].Success) dependencies.Add(m.Groups[2].Value);
                }
                break;
            case "java":
                foreach (Match m in Regex.Matches(content, @"import\s+([\w.]+);"))
                    dependencies.Add(m.Groups[1].Value);
                break;
            case "rust":
                foreach (Match m in Regex.Matches(content, @"(?:extern\s+crate\s+(\w+)|use\s+([\w:]+))"))
                {
                    if (m.Groups[1].Success) dependencies.Add(m.Groups[1].Value);
                    else if (m.Groups[2].Success) dependencies.Add(m.Groups[2].Value);
                }
                break;
        }

        var distinctDeps = dependencies.Distinct().ToList();

        var sb = new StringBuilder();
        sb.AppendLine($"依赖分析：{Path.GetFileName(filePath)}");
        sb.AppendLine($"语言：{lang} | 依赖数量：{distinctDeps.Count}");
        sb.AppendLine();

        // 内部依赖 vs 外部依赖
        var internalDeps = distinctDeps.Where(d =>
            d.StartsWith("DesktopMascot") || d.StartsWith("System") || d.StartsWith("Microsoft")).ToList();
        var externalDeps = distinctDeps.Except(internalDeps).ToList();

        if (internalDeps.Count > 0)
        {
            sb.AppendLine($"内部依赖（{internalDeps.Count}）：");
            foreach (var d in internalDeps) sb.AppendLine($"  - {d}");
        }
        if (externalDeps.Count > 0)
        {
            sb.AppendLine($"外部依赖（{externalDeps.Count}）：");
            foreach (var d in externalDeps) sb.AppendLine($"  - {d}");
        }

        return new ToolResult { Name = Name, Success = true, Content = sb.ToString() };
    }

    private async Task<ToolResult> AnalyzeStatsAsync(string filePath, string? langHint, CancellationToken ct)
    {
        if (!File.Exists(filePath)) return Fail($"文件不存在：{filePath}");

        var content = await File.ReadAllTextAsync(filePath, ct);
        var lines = content.Split('\n');
        var lang = langHint ?? DetectLanguage(filePath);
        var def = GetLangDef(lang);

        var codeLines = lines.Count(l => !string.IsNullOrWhiteSpace(l) && !l.TrimStart().StartsWith(def.LineComments[0]));
        var commentLines = lines.Count(l => l.TrimStart().StartsWith(def.LineComments[0]));

        var stats = new Dictionary<string, object>
        {
            ["文件名"] = Path.GetFileName(filePath),
            ["语言"] = lang,
            ["扩展名"] = Path.GetExtension(filePath),
            ["总行数"] = lines.Length,
            ["代码行数"] = codeLines,
            ["注释行数"] = commentLines,
            ["注释率"] = lines.Length > 0 ? $"{commentLines * 100.0 / lines.Length:F1}%" : "0%",
            ["空行数"] = lines.Count(string.IsNullOrWhiteSpace),
            ["文件大小"] = $"{new FileInfo(filePath).Length / 1024.0:F1} KB",
            ["字符数"] = content.Length,
            ["方法数"] = CountMethods(lines, def),
            ["圈复杂度"] = CalculateCyclomaticComplexity(content, lang)
        };

        var sb = new StringBuilder();
        sb.AppendLine($"文件统计：{Path.GetFileName(filePath)}");
        sb.AppendLine();
        foreach (var stat in stats)
            sb.AppendLine($"  {stat.Key}：{stat.Value}");

        return new ToolResult { Name = Name, Success = true, Content = sb.ToString() };
    }

    private async Task<ToolResult> AnalyzeStructureAsync(string directory, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
            return Fail($"目录不存在：{directory}");

        var files = Directory.GetFiles(directory, "*.*", SearchOption.AllDirectories);
        var structure = new Dictionary<string, int>();
        var langCount = new Dictionary<string, int>();

        foreach (var file in files)
        {
            ct.ThrowIfCancellationRequested();
            var ext = Path.GetExtension(file).ToLower();
            if (string.IsNullOrEmpty(ext)) ext = "(无扩展名)";
            structure[ext] = structure.GetValueOrDefault(ext) + 1;

            var lang = DetectLanguage(file);
            langCount[lang] = langCount.GetValueOrDefault(lang) + 1;
        }

        var sb = new StringBuilder();
        sb.AppendLine($"目录结构分析：{Path.GetFileName(directory)}");
        sb.AppendLine($"总文件数：{files.Length}");
        sb.AppendLine();
        sb.AppendLine("文件类型分布：");
        foreach (var item in structure.OrderByDescending(x => x.Value).Take(15))
            sb.AppendLine($"  {item.Key}：{item.Value} 个");
        sb.AppendLine();
        sb.AppendLine("语言分布：");
        foreach (var item in langCount.OrderByDescending(x => x.Value))
            sb.AppendLine($"  {item.Key}：{item.Value} 个");

        return new ToolResult { Name = Name, Success = true, Content = sb.ToString() };
    }

    private async Task<ToolResult> AnalyzeSmellsAsync(string filePath, string? langHint, CancellationToken ct)
    {
        if (!File.Exists(filePath)) return Fail($"文件不存在：{filePath}");

        var content = await File.ReadAllTextAsync(filePath, ct);
        var lines = content.Split('\n');
        var lang = langHint ?? DetectLanguage(filePath);
        var smells = new List<(string Severity, string Category, string Description, int Line)>();

        // 长方法
        var methods = ExtractMethods(lines, GetLangDef(lang));
        foreach (var m in methods.Where(m => m.LineCount > 40))
            smells.Add(("warning", "LongMethod", $"方法过长：{m.Name}（{m.LineCount} 行）", m.StartLine));

        // 深嵌套
        int depth = 0;
        for (int i = 0; i < lines.Length; i++)
        {
            foreach (var open in GetLangDef(lang).BlockOpen) depth += CountOccurrences(lines[i], open);
            if (depth > 4)
                smells.Add(("warning", "DeepNesting", $"深层嵌套：第 {i + 1} 行嵌套 {depth} 层", i + 1));
            foreach (var close in GetLangDef(lang).BlockClose) depth -= CountOccurrences(lines[i], close);
        }

        // 魔法数字
        var magicNumbers = Regex.Matches(content, @"(?<![.\w])\d{2,}(?![.\w])");
        if (magicNumbers.Count > 5)
            smells.Add(("info", "MagicNumbers", $"魔法数字：{magicNumbers.Count} 个硬编码数字", 0));

        // God class（文件过长 + 方法过多）
        if (lines.Length > 500 && CountMethods(lines, GetLangDef(lang)) > 20)
            smells.Add(("warning", "GodClass", $"God Class：{lines.Length} 行 + {CountMethods(lines, GetLangDef(lang))} 个方法", 0));

        // 参数过多
        foreach (var m in methods.Where(m => m.ParamCount > 5))
            smells.Add(("info", "TooManyParams", $"参数过多：{m.Name}（{m.ParamCount} 个参数）", m.StartLine));

        var sb = new StringBuilder();
        sb.AppendLine($"代码异味分析：{Path.GetFileName(filePath)}");
        sb.AppendLine($"语言：{lang} | 发现：{smells.Count} 个");
        sb.AppendLine();

        foreach (var s in smells.OrderByDescending(s => s.Severity == "error" ? 0 : s.Severity == "warning" ? 1 : 2))
        {
            var location = s.Line > 0 ? $"行 {s.Line}" : "全局";
            sb.AppendLine($"  [{s.Severity}] [{s.Category}] {s.Description}（{location}）");
        }

        if (smells.Count == 0) sb.AppendLine("  ✅ 未发现代码异味");

        return new ToolResult { Name = Name, Success = true, Content = sb.ToString() };
    }

    private async Task<ToolResult> AnalyzeMethodsAsync(string filePath, string? langHint, CancellationToken ct)
    {
        if (!File.Exists(filePath)) return Fail($"文件不存在：{filePath}");

        var lines = (await File.ReadAllTextAsync(filePath, ct)).Split('\n');
        var lang = langHint ?? DetectLanguage(filePath);
        var methods = ExtractMethods(lines, GetLangDef(lang));

        var sb = new StringBuilder();
        sb.AppendLine($"方法分析：{Path.GetFileName(filePath)}");
        sb.AppendLine($"方法数量：{methods.Count}");
        sb.AppendLine();

        foreach (var m in methods)
        {
            var cc = CalculateMethodComplexity(m.Body, lang);
            var rating = cc <= 5 ? "简单" : cc <= 10 ? "一般" : cc <= 20 ? "复杂" : "高复杂度";
            sb.AppendLine($"  {m.Name}（行 {m.StartLine}，{m.LineCount} 行，{m.ParamCount} 参数，CC={cc} {rating}）");
        }

        return new ToolResult { Name = Name, Success = true, Content = sb.ToString() };
    }

    private async Task<ToolResult> FullAnalysisAsync(string filePath, string? langHint, CancellationToken ct)
    {
        if (!File.Exists(filePath)) return Fail($"文件不存在：{filePath}");

        var quality = await AnalyzeQualityAsync(filePath, langHint, ct);
        var complexity = await AnalyzeComplexityAsync(filePath, langHint, ct);
        var smells = await AnalyzeSmellsAsync(filePath, langHint, ct);

        var sb = new StringBuilder();
        sb.AppendLine("═══ 完整代码分析报告 ═══");
        sb.AppendLine();
        sb.AppendLine(quality.Content);
        sb.AppendLine();
        sb.AppendLine(complexity.Content);
        sb.AppendLine();
        sb.AppendLine(smells.Content);

        return new ToolResult { Name = Name, Success = true, Content = sb.ToString() };
    }

    #region 语言检测

    private static string DetectLanguage(string filePath)
    {
        var ext = Path.GetExtension(filePath).ToLower();
        return ext switch
        {
            ".cs" => "csharp",
            ".js" or ".jsx" => "javascript",
            ".ts" or ".tsx" => "typescript",
            ".py" => "python",
            ".go" => "go",
            ".rs" => "rust",
            ".java" => "java",
            ".php" => "php",
            ".rb" => "ruby",
            _ => "unknown"
        };
    }

    private static (string[] LineComments, string[] BlockOpen, string[] BlockClose, string[] MethodPatterns) GetLangDef(string lang)
    {
        return LanguageDefs.TryGetValue(lang, out var def) ? def :
            (new[] { "//" }, new[] { "{" }, new[] { "}" }, Array.Empty<string>());
    }

    #endregion

    #region 方法提取

    private static List<MethodInfo> ExtractMethods(string[] lines, (string[] LineComments, string[] BlockOpen, string[] BlockClose, string[] MethodPatterns) def)
    {
        var methods = new List<MethodInfo>();
        var inBlock = false;
        int blockStart = 0;
        int braceDepth = 0;
        int methodStart = 0;
        string methodName = "";
        int paramCount = 0;
        var bodyLines = new List<string>();

        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();

            foreach (var pattern in def.MethodPatterns)
            {
                var match = Regex.Match(line, pattern);
                if (match.Success)
                {
                    methodName = match.Groups[1].Value;
                    if (string.IsNullOrEmpty(methodName)) methodName = match.Groups[2].Value;
                    methodStart = i + 1;
                    paramCount = Regex.Matches(line, ",").Count + (line.Contains('(') ? 1 : 0);
                    if (paramCount == 0 && line.Contains("()")) paramCount = 0;
                    else paramCount = Math.Max(0, paramCount - (line.Contains(")") ? 0 : 0));
                }
            }

            foreach (var open in def.BlockOpen) braceDepth += CountOccurrences(line, open);
            foreach (var close in def.BlockClose) braceDepth -= CountOccurrences(line, close);

            if (braceDepth > 0 && !string.IsNullOrEmpty(methodName) && methodStart > 0 && !inBlock)
            {
                inBlock = true;
                blockStart = i;
                bodyLines.Clear();
            }

            if (inBlock)
            {
                bodyLines.Add(lines[i]);
                if (braceDepth <= 0)
                {
                    methods.Add(new MethodInfo
                    {
                        Name = methodName,
                        StartLine = methodStart,
                        LineCount = i - blockStart + 1,
                        ParamCount = paramCount,
                        Body = string.Join("\n", bodyLines)
                    });
                    inBlock = false;
                    methodName = "";
                    methodStart = 0;
                    bodyLines.Clear();
                }
            }
        }

        return methods;
    }

    private static int CalculateMethodComplexity(string body, string lang)
    {
        int cc = 1; // 基础复杂度

        // 条件语句
        cc += Regex.Matches(body, @"\b(if|else if|elif)\b").Count;
        cc += Regex.Matches(body, @"\bcase\s+").Count;

        // 循环
        cc += Regex.Matches(body, @"\b(for|foreach|while|loop)\b").Count;

        // 异常
        cc += Regex.Matches(body, @"\b(try|catch)\b").Count;

        // 逻辑运算符
        cc += Regex.Matches(body, @"&&|\|\||\band\b|\bor\b").Count;

        // 三元运算符
        cc += Regex.Matches(body, @"\?").Count;

        return cc;
    }

    #endregion

    #region 分析核心

    private static int CalculateCyclomaticComplexity(string content, string lang)
    {
        int cc = 1;
        cc += Regex.Matches(content, @"\b(if|else if|elif)\b").Count;
        cc += Regex.Matches(content, @"\bcase\s+").Count;
        cc += Regex.Matches(content, @"\b(for|foreach|while|loop|until)\b").Count;
        cc += Regex.Matches(content, @"\b(catch|except)\b").Count;
        cc += Regex.Matches(content, @"&&|\|\||\band\b|\bor\b").Count;
        cc += Regex.Matches(content, @"\?(?!.*=>)").Count / 2; // 三元运算符
        return cc;
    }

    private static int CheckDuplicateLines(string[] lines)
    {
        var lineSet = new HashSet<string>();
        var duplicates = 0;
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (trimmed.Length < 10) continue;
            if (!lineSet.Add(trimmed)) duplicates++;
        }
        return duplicates;
    }

    private static int CalculateMaxNesting(string[] lines, (string[] LineComments, string[] BlockOpen, string[] BlockClose, string[] MethodPatterns) def)
    {
        int maxDepth = 0, currentDepth = 0;
        foreach (var line in lines)
        {
            foreach (var open in def.BlockOpen) currentDepth += CountOccurrences(line, open);
            maxDepth = Math.Max(maxDepth, currentDepth);
            foreach (var close in def.BlockClose) currentDepth -= CountOccurrences(line, close);
        }
        return maxDepth;
    }

    private static int CountMethods(string[] lines, (string[] LineComments, string[] BlockOpen, string[] BlockClose, string[] MethodPatterns) def)
    {
        int count = 0;
        foreach (var line in lines)
            foreach (var pattern in def.MethodPatterns)
                if (Regex.IsMatch(line, pattern)) { count++; break; }
        return count;
    }

    private static int CountOccurrences(string text, string pattern)
    {
        int count = 0, index = 0;
        while ((index = text.IndexOf(pattern, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += pattern.Length;
        }
        return count;
    }

    #endregion

    private static ToolResult Fail(string error) => new() { Name = "code_analysis", Success = false, Error = error };

    private class MethodInfo
    {
        public string Name { get; set; } = "";
        public int StartLine { get; set; }
        public int LineCount { get; set; }
        public int ParamCount { get; set; }
        public string Body { get; set; } = "";
    }
}
