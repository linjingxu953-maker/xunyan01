using DesktopMascot.Core.Tools;
using System.Text.Json;
using System.Text.RegularExpressions;
using DesktopMascot.Agent.Context;
using DesktopMascot.Agent.Models;

namespace DesktopMascot.Agent.Tools;

/// <summary>
/// 文件编辑工具 - 支持多种编辑模式（需权限确认）
/// </summary>
public class EditFileTool : ITool
{
    private readonly IContextProvider _contextProvider;

    public EditFileTool(IContextProvider contextProvider)
    {
        _contextProvider = contextProvider;
    }

    public string Name => "edit_file";
    public string Description => "编辑文件内容。支持：替换文本、在指定位置插入、删除行、在文件开头/结尾追加。需要用户确认权限。";
    public bool RequiresConfirmation => true;
    public string ConfirmationMessage => "AI 想要编辑文件，是否允许？";
    public string ParametersSchema => """
    {
        "type": "object",
        "properties": {
            "path": {
                "type": "string",
                "description": "文件路径"
            },
            "mode": {
                "type": "string",
                "enum": ["replace", "insert", "delete", "append", "prepend"],
                "description": "编辑模式"
            },
            "old_text": {
                "type": "string",
                "description": "要替换/删除的文本（replace/delete 模式）"
            },
            "new_text": {
                "type": "string",
                "description": "替换后的新文本（replace/insert/append/prepend 模式）"
            },
            "line_number": {
                "type": "integer",
                "description": "插入/删除的行号（insert/delete 模式）"
            },
            "use_regex": {
                "type": "boolean",
                "description": "是否使用正则表达式（replace 模式）"
            }
        },
        "required": ["path", "mode"]
    }
    """;

    public async Task<ToolResult> ExecuteAsync(string arguments, CancellationToken ct = default)
    {
        try
        {
            var doc = JsonDocument.Parse(arguments);
            var root = doc.RootElement;

            if (!root.TryGetProperty("path", out var pathElement))
                return Fail("缺少 path 参数");

            if (!root.TryGetProperty("mode", out var modeElement))
                return Fail("缺少 mode 参数");

            var filePath = pathElement.GetString() ?? "";
            var mode = modeElement.GetString() ?? "";

            if (string.IsNullOrWhiteSpace(filePath))
                return Fail("文件路径不能为空");

            if (!File.Exists(filePath))
                return Fail($"文件不存在：{filePath}");

            var content = await File.ReadAllTextAsync(filePath, ct);
            var originalLength = content.Length;

            string resultMessage;

            switch (mode)
            {
                case "replace":
                    resultMessage = await ReplaceAsync(content, root, filePath, ct);
                    break;
                case "insert":
                    resultMessage = InsertAtLine(content, root, filePath, ct);
                    break;
                case "delete":
                    resultMessage = DeleteContent(content, root, filePath, ct);
                    break;
                case "append":
                    resultMessage = await AppendAsync(content, root, filePath, ct);
                    break;
                case "prepend":
                    resultMessage = await PrependAsync(content, root, filePath, ct);
                    break;
                default:
                    return Fail($"不支持的编辑模式：{mode}");
            }

            if (resultMessage.StartsWith("[错误"))
                return Fail(resultMessage);

            var newContent = await File.ReadAllTextAsync(filePath, ct);
            var changed = newContent.Length - originalLength;

            return new ToolResult
            {
                Name = Name,
                Success = true,
                Content = $"文件已编辑：{filePath}\n操作：{mode}\n变化：{(changed >= 0 ? "+" : "")}{changed} 字符\n{resultMessage}"
            };
        }
        catch (Exception ex)
        {
            return Fail($"编辑失败：{ex.Message}");
        }
    }

    private async Task<string> ReplaceAsync(string content, JsonElement root, string filePath, CancellationToken ct)
    {
        if (!root.TryGetProperty("old_text", out var oldElement))
            return "[错误] 缺少 old_text 参数";

        var oldText = oldElement.GetString() ?? "";
        var newText = root.TryGetProperty("new_text", out var newElement) ? newElement.GetString() ?? "" : "";
        var useRegex = root.TryGetProperty("use_regex", out var regexElement) && regexElement.GetBoolean();

        if (string.IsNullOrEmpty(oldText))
            return "[错误] old_text 不能为空";

        string newContent;
        int count;

        if (useRegex)
        {
            var regex = new Regex(oldText, RegexOptions.Multiline);
            newContent = regex.Replace(content, newText);
            count = regex.Matches(content).Count;
        }
        else
        {
            count = content.Split(oldText).Length - 1;
            newContent = content.Replace(oldText, newText);
        }

        if (count == 0)
            return "[错误] 未找到匹配文本";

        await File.WriteAllTextAsync(filePath, newContent, ct);
        return $"替换了 {count} 处匹配";
    }

    private string InsertAtLine(string content, JsonElement root, string filePath, CancellationToken ct)
    {
        if (!root.TryGetProperty("line_number", out var lineElement))
            return "[错误] 缺少 line_number 参数";

        if (!root.TryGetProperty("new_text", out var newElement))
            return "[错误] 缺少 new_text 参数";

        var lineNumber = lineElement.GetInt32();
        var newText = newElement.GetString() ?? "";
        var lines = content.Split('\n');

        if (lineNumber < 1 || lineNumber > lines.Length + 1)
            return $"[错误] 行号超出范围（1-{lines.Length + 1}）";

        var insertIndex = lineNumber - 1;
        var newLines = new List<string>(lines);
        newLines.Insert(insertIndex, newText);

        File.WriteAllText(filePath, string.Join('\n', newLines));
        return $"在第 {lineNumber} 行插入了内容";
    }

    private string DeleteContent(string content, JsonElement root, string filePath, CancellationToken ct)
    {
        if (root.TryGetProperty("old_text", out var oldElement))
        {
            var oldText = oldElement.GetString() ?? "";
            if (string.IsNullOrEmpty(oldText))
                return "[错误] old_text 不能为空";

            var count = content.Split(oldText).Length - 1;
            if (count == 0)
                return "[错误] 未找到匹配文本";

            var newContent = content.Replace(oldText, "");
            File.WriteAllText(filePath, newContent);
            return $"删除了 {count} 处匹配";
        }

        if (root.TryGetProperty("line_number", out var lineElement))
        {
            var lineNumber = lineElement.GetInt32();
            var lines = content.Split('\n');

            if (lineNumber < 1 || lineNumber > lines.Length)
                return $"[错误] 行号超出范围（1-{lines.Length}）";

            var deletedLine = lines[lineNumber - 1];
            var newLines = new List<string>(lines);
            newLines.RemoveAt(lineNumber - 1);

            File.WriteAllText(filePath, string.Join('\n', newLines));
            return $"删除了第 {lineNumber} 行：{deletedLine.Trim()[..Math.Min(50, deletedLine.Trim().Length)]}";
        }

        return "[错误] 需要 old_text 或 line_number 参数";
    }

    private async Task<string> AppendAsync(string content, JsonElement root, string filePath, CancellationToken ct)
    {
        if (!root.TryGetProperty("new_text", out var newElement))
            return "[错误] 缺少 new_text 参数";

        var newText = newElement.GetString() ?? "";
        var newContent = content + newText;
        await File.WriteAllTextAsync(filePath, newContent, ct);
        return $"在文件末尾追加了 {newText.Length} 字符";
    }

    private async Task<string> PrependAsync(string content, JsonElement root, string filePath, CancellationToken ct)
    {
        if (!root.TryGetProperty("new_text", out var newElement))
            return "[错误] 缺少 new_text 参数";

        var newText = newElement.GetString() ?? "";
        var newContent = newText + content;
        await File.WriteAllTextAsync(filePath, newContent, ct);
        return $"在文件开头插入了 {newText.Length} 字符";
    }

    private static ToolResult Fail(string error) => new() { Name = "edit_file", Success = false, Error = error };
}
