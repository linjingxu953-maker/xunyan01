using System.Text;
using System.Text.Json;
using DesktopMascot.Agent.Models;

namespace DesktopMascot.Agent.Tools;

/// <summary>
/// 日历操作工具 - 日期计算、时间管理、日程安排
/// </summary>
public class CalendarTool : ITool
{
    public string Name => "calendar";
    public string Description => "日历操作：日期计算、时间转换、日程安排、倒计时。";
    public string ParametersSchema => """
    {
        "type": "object",
        "properties": {
            "action": { "type": "string", "enum": ["now", "format", "diff", "add", "convert", "countdown", "schedule"], "description": "操作类型" },
            "date": { "type": "string", "description": "日期字符串（yyyy-MM-dd 或 yyyy-MM-dd HH:mm:ss）" },
            "days": { "type": "integer", "description": "天数（add模式）" },
            "hours": { "type": "integer", "description": "小时数（add模式）" },
            "timezone": { "type": "string", "description": "时区（convert模式）" },
            "format": { "type": "string", "description": "输出格式" }
        },
        "required": ["action"]
    }
    """;

    public async Task<ToolResult> ExecuteAsync(string arguments, CancellationToken ct = default)
    {
        try
        {
            var doc = JsonDocument.Parse(arguments);
            var root = doc.RootElement;
            var action = root.TryGetProperty("action", out var aEl) ? aEl.GetString() ?? "" : "";

            return action switch
            {
                "now" => GetNow(root),
                "format" => FormatDate(root),
                "diff" => CalculateDiff(root),
                "add" => AddTime(root),
                "convert" => ConvertTimezone(root),
                "countdown" => CalculateCountdown(root),
                "schedule" => GetSchedule(root),
                _ => Fail($"不支持的操作：{action}")
            };
        }
        catch (Exception ex)
        {
            return Fail($"日历操作失败：{ex.Message}");
        }
    }

    private ToolResult GetNow(JsonElement root)
    {
        var timezone = root.TryGetProperty("timezone", out var tzEl) ? tzEl.GetString() : null;
        var format = root.TryGetProperty("format", out var fEl) ? fEl.GetString() : "yyyy-MM-dd HH:mm:ss";

        var now = DateTime.Now;
        var sb = new StringBuilder();
        sb.AppendLine("当前时间");
        sb.AppendLine($"本地时间：{now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"UTC 时间：{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"星期：{now:dddd}");
        sb.AppendLine($"是一年中的第 {now.DayOfYear} 天");

        return new ToolResult { Name = Name, Success = true, Content = sb.ToString() };
    }

    private ToolResult FormatDate(JsonElement root)
    {
        var dateStr = root.TryGetProperty("date", out var dEl) ? dEl.GetString() ?? "" : "";
        var format = root.TryGetProperty("format", out var fEl) ? fEl.GetString() ?? "yyyy-MM-dd" : "yyyy-MM-dd";

        if (string.IsNullOrEmpty(dateStr))
            return Fail("缺少 date 参数");

        if (!DateTime.TryParse(dateStr, out var date))
            return Fail($"无效的日期格式：{dateStr}");

        var sb = new StringBuilder();
        sb.AppendLine("日期格式化");
        sb.AppendLine($"原始：{dateStr}");
        sb.AppendLine($"格式化：{date.ToString(format)}");
        sb.AppendLine($"星期：{date:dddd}");

        return new ToolResult { Name = Name, Success = true, Content = sb.ToString() };
    }

    private ToolResult CalculateDiff(JsonElement root)
    {
        var date1Str = root.TryGetProperty("date", out var d1El) ? d1El.GetString() ?? "" : "";
        var date2Str = root.TryGetProperty("date2", out var d2El) ? d2El.GetString() : null;

        if (string.IsNullOrEmpty(date1Str))
            return Fail("缺少 date 参数");

        if (!DateTime.TryParse(date1Str, out var date1))
            return Fail($"无效的日期格式：{date1Str}");

        var date2 = string.IsNullOrEmpty(date2Str) ? DateTime.Now : DateTime.Parse(date2Str);
        var diff = date2 - date1;

        var sb = new StringBuilder();
        sb.AppendLine("日期差异");
        sb.AppendLine($"日期1：{date1:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"日期2：{date2:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"差异：{diff.Days} 天 {diff.Hours} 小时 {diff.Minutes} 分钟");
        sb.AppendLine($"总秒数：{diff.TotalSeconds:F0} 秒");

        return new ToolResult { Name = Name, Success = true, Content = sb.ToString() };
    }

    private ToolResult AddTime(JsonElement root)
    {
        var dateStr = root.TryGetProperty("date", out var dEl) ? dEl.GetString() ?? "" : DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        var days = root.TryGetProperty("days", out var dayEl) ? dayEl.GetInt32() : 0;
        var hours = root.TryGetProperty("hours", out var hourEl) ? hourEl.GetInt32() : 0;

        if (!DateTime.TryParse(dateStr, out var date))
            return Fail($"无效的日期格式：{dateStr}");

        var result = date.AddDays(days).AddHours(hours);

        var sb = new StringBuilder();
        sb.AppendLine("日期计算");
        sb.AppendLine($"原始：{date:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"+{days} 天 +{hours} 小时");
        sb.AppendLine($"结果：{result:yyyy-MM-dd HH:mm:ss}");

        return new ToolResult { Name = Name, Success = true, Content = sb.ToString() };
    }

    private ToolResult ConvertTimezone(JsonElement root)
    {
        var dateStr = root.TryGetProperty("date", out var dEl) ? dEl.GetString() ?? "" : DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        var timezone = root.TryGetProperty("timezone", out var tzEl) ? tzEl.GetString() ?? "" : "";

        if (!DateTime.TryParse(dateStr, out var date))
            return Fail($"无效的日期格式：{dateStr}");

        var sb = new StringBuilder();
        sb.AppendLine("时区转换");
        sb.AppendLine($"原始：{date:yyyy-MM-dd HH:mm:ss} (本地)");
        sb.AppendLine($"目标时区：{timezone}");
        sb.AppendLine($"UTC：{date.ToUniversalTime():yyyy-MM-dd HH:mm:ss}");

        return new ToolResult { Name = Name, Success = true, Content = sb.ToString() };
    }

    private ToolResult CalculateCountdown(JsonElement root)
    {
        var dateStr = root.TryGetProperty("date", out var dEl) ? dEl.GetString() ?? "" : "";
        if (string.IsNullOrEmpty(dateStr))
            return Fail("缺少 date 参数");

        if (!DateTime.TryParse(dateStr, out var targetDate))
            return Fail($"无效的日期格式：{dateStr}");

        var now = DateTime.Now;
        var remaining = targetDate - now;

        var sb = new StringBuilder();
        sb.AppendLine("倒计时");
        sb.AppendLine($"目标：{targetDate:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"当前：{now:yyyy-MM-dd HH:mm:ss}");

        if (remaining.TotalSeconds > 0)
        {
            sb.AppendLine($"剩余：{remaining.Days} 天 {remaining.Hours} 小时 {remaining.Minutes} 分钟");
            sb.AppendLine($"状态：进行中");
        }
        else
        {
            sb.AppendLine($"已过期：{Math.Abs(remaining.Days)} 天");
            sb.AppendLine($"状态：已结束");
        }

        return new ToolResult { Name = Name, Success = true, Content = sb.ToString() };
    }

    private ToolResult GetSchedule(JsonElement root)
    {
        var sb = new StringBuilder();
        sb.AppendLine("今日日程");
        sb.AppendLine($"日期：{DateTime.Now:yyyy-MM-dd}");
        sb.AppendLine($"星期：{DateTime.Now:dddd}");
        sb.AppendLine();

        // 这里可以集成实际的日历 API
        sb.AppendLine("（需要集成日历 API 才能获取实际日程）");

        return new ToolResult { Name = Name, Success = true, Content = sb.ToString() };
    }

    private static ToolResult Fail(string error) => new() { Name = "calendar", Success = false, Error = error };
}
