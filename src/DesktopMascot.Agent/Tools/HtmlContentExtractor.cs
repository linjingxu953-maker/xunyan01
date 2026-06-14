using System.Text;
using System.Text.RegularExpressions;

namespace DesktopMascot.Agent.Tools;

/// <summary>
/// HTML 正文提取器 - 从 HTML 中提取可读文本
/// </summary>
public static class HtmlContentExtractor
{
    private static readonly Regex TagRegex = new(@"<[^>]+>", RegexOptions.Compiled);
    private static readonly Regex ScriptRegex = new(@"<script[^>]*>.*?</script>", RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.IgnoreCase);
    private static readonly Regex StyleRegex = new(@"<style[^>]*>.*?</style>", RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.IgnoreCase);
    private static readonly Regex CommentRegex = new(@"<!--.*?-->", RegexOptions.Compiled | RegexOptions.Singleline);
    private static readonly Regex WhitespaceRegex = new(@"\s+", RegexOptions.Compiled);
    private static readonly Regex BlankLineRegex = new(@"\n\s*\n", RegexOptions.Compiled);

    /// <summary>
    /// 从 HTML 提取纯文本
    /// </summary>
    public static string ExtractText(string html, int maxLength = 10000)
    {
        if (string.IsNullOrWhiteSpace(html))
            return string.Empty;

        var text = html;

        // 移除脚本和样式
        text = ScriptRegex.Replace(text, "");
        text = StyleRegex.Replace(text, "");
        text = CommentRegex.Replace(text, "");

        // 保留标题和重要标签的内容
        var sb = new StringBuilder();
        var lines = Regex.Split(text, @"(?=<[^>]+>)|(?<=>)");

        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed)) continue;

            // 跳过标签本身
            if (trimmed.StartsWith("<")) continue;

            // 清理 HTML 实体
            trimmed = HtmlDecode(trimmed);

            // 跳过纯空白
            if (string.IsNullOrWhiteSpace(trimmed)) continue;

            sb.AppendLine(trimmed);
        }

        text = sb.ToString();

        // 清理多余空白
        text = WhitespaceRegex.Replace(text, " ");
        text = BlankLineRegex.Replace(text, "\n");
        text = text.Trim();

        // 截断
        if (text.Length > maxLength)
        {
            text = text[..maxLength] + "\n...(内容已截断)";
        }

        return text;
    }

    /// <summary>
    /// 提取关键内容（标题、段落、列表）
    /// </summary>
    public static string ExtractKeyContent(string html, int maxLength = 5000)
    {
        if (string.IsNullOrWhiteSpace(html))
            return string.Empty;

        var sb = new StringBuilder();

        // 提取标题
        var titleMatch = Regex.Match(html, @"<title[^>]*>(.*?)</title>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        if (titleMatch.Success)
        {
            sb.AppendLine($"标题：{HtmlDecode(titleMatch.Groups[1].Value.Trim())}");
            sb.AppendLine();
        }

        // 提取 meta description
        var metaMatch = Regex.Match(html, @"<meta[^>]*name=""description""[^>]*content=""([^""]*)""", RegexOptions.IgnoreCase);
        if (metaMatch.Success && metaMatch.Groups[1].Value.Length > 10)
        {
            sb.AppendLine($"摘要：{HtmlDecode(metaMatch.Groups[1].Value.Trim())}");
            sb.AppendLine();
        }

        // 提取 h1-h3 标题
        for (int i = 1; i <= 3; i++)
        {
            var hMatches = Regex.Matches(html, $@"<h{i}[^>]*>(.*?)</h{i}>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            foreach (Match m in hMatches)
            {
                var heading = HtmlDecode(Regex.Replace(m.Groups[1].Value, "<[^>]+>", "").Trim());
                if (!string.IsNullOrWhiteSpace(heading))
                    sb.AppendLine($"{new string('#', i)} {heading}");
            }
        }

        // 提取段落
        var pMatches = Regex.Matches(html, @"<p[^>]*>(.*?)</p>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        foreach (Match m in pMatches)
        {
            var paragraph = HtmlDecode(Regex.Replace(m.Groups[1].Value, "<[^>]+>", "").Trim());
            if (paragraph.Length > 20 && paragraph.Length < 500)
            {
                sb.AppendLine();
                sb.AppendLine(paragraph);
            }
        }

        // 提取列表项
        var liMatches = Regex.Matches(html, @"<li[^>]*>(.*?)</li>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        foreach (Match m in liMatches)
        {
            var item = HtmlDecode(Regex.Replace(m.Groups[1].Value, "<[^>]+>", "").Trim());
            if (!string.IsNullOrWhiteSpace(item) && item.Length < 200)
            {
                sb.AppendLine($"• {item}");
            }
        }

        var result = sb.ToString().Trim();
        return result.Length > maxLength ? result[..maxLength] + "\n...(内容已截断)" : result;
    }

    private static string HtmlDecode(string text)
    {
        return text
            .Replace("&amp;", "&")
            .Replace("&lt;", "<")
            .Replace("&gt;", ">")
            .Replace("&quot;", "\"")
            .Replace("&#39;", "'")
            .Replace("&nbsp;", " ")
            .Replace("&#x27;", "'")
            .Replace("&#x2F;", "/")
            .Trim();
    }
}
