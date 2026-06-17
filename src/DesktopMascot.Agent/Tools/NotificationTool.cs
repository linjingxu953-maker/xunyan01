using DesktopMascot.Core.Tools;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using DesktopMascot.Agent.Models;

namespace DesktopMascot.Agent.Tools;

/// <summary>
/// 通知工具 - 系统通知、桌面提醒、定时提醒
/// </summary>
public class NotificationTool : ITool
{
    private static readonly Dictionary<string, DateTime> _reminders = new();

    public string Name => "notification";
    public string Description => "通知工具：发送系统通知、设置定时提醒、管理提醒列表。";
    public string ParametersSchema => """
    {
        "type": "object",
        "properties": {
            "action": { "type": "string", "enum": ["notify", "reminder", "list_reminders", "cancel_reminder", "toast"], "description": "操作类型" },
            "title": { "type": "string", "description": "通知标题" },
            "message": { "type": "string", "description": "通知内容" },
            "delay_seconds": { "type": "integer", "description": "延迟秒数（reminder模式）" },
            "reminder_id": { "type": "string", "description": "提醒ID（cancel模式）" }
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
                "notify" => await SendNotificationAsync(root, ct),
                "reminder" => await SetReminderAsync(root, ct),
                "list_reminders" => ListReminders(),
                "cancel_reminder" => CancelReminder(root),
                "toast" => await ShowToastAsync(root, ct),
                _ => Fail($"不支持的操作：{action}")
            };
        }
        catch (Exception ex)
        {
            return Fail($"通知操作失败：{ex.Message}");
        }
    }

    private async Task<ToolResult> SendNotificationAsync(JsonElement root, CancellationToken ct)
    {
        var title = root.TryGetProperty("title", out var tEl) ? tEl.GetString() ?? "" : "";
        var message = root.TryGetProperty("message", out var mEl) ? mEl.GetString() ?? "" : "";

        if (string.IsNullOrEmpty(title)) return Fail("缺少 title 参数");

        // Windows 托盘通知
        ShowBalloonTip(title, message);

        return new ToolResult
        {
            Name = Name,
            Success = true,
            Content = $"已发送通知\n标题：{title}\n内容：{message}"
        };
    }

    private async Task<ToolResult> SetReminderAsync(JsonElement root, CancellationToken ct)
    {
        var title = root.TryGetProperty("title", out var tEl) ? tEl.GetString() ?? "" : "";
        var message = root.TryGetProperty("message", out var mEl) ? mEl.GetString() ?? "" : "";
        var delaySeconds = root.TryGetProperty("delay_seconds", out var dEl) ? dEl.GetInt32() : 60;

        if (string.IsNullOrEmpty(title)) return Fail("缺少 title 参数");

        var reminderId = Guid.NewGuid().ToString("N")[..8];
        var triggerTime = DateTime.Now.AddSeconds(delaySeconds);

        _reminders[reminderId] = triggerTime;

        // 启动定时器
        _ = Task.Run(async () =>
        {
            await Task.Delay(delaySeconds * 1000, ct);
            if (_reminders.ContainsKey(reminderId))
            {
                ShowBalloonTip($"提醒：{title}", message);
                _reminders.Remove(reminderId);
            }
        }, ct);

        return new ToolResult
        {
            Name = Name,
            Success = true,
            Content = $"已设置提醒\nID：{reminderId}\n标题：{title}\n触发时间：{triggerTime:HH:mm:ss}\n延迟：{delaySeconds} 秒"
        };
    }

    private ToolResult ListReminders()
    {
        var sb = new StringBuilder();
        sb.AppendLine("当前提醒列表");

        if (_reminders.Count == 0)
        {
            sb.AppendLine("暂无提醒");
        }
        else
        {
            foreach (var kvp in _reminders)
            {
                sb.AppendLine($"  {kvp.Key} - {kvp.Value:HH:mm:ss}");
            }
        }

        return new ToolResult { Name = Name, Success = true, Content = sb.ToString() };
    }

    private ToolResult CancelReminder(JsonElement root)
    {
        var reminderId = root.TryGetProperty("reminder_id", out var rEl) ? rEl.GetString() ?? "" : "";
        if (string.IsNullOrEmpty(reminderId)) return Fail("缺少 reminder_id 参数");

        if (_reminders.Remove(reminderId))
        {
            return new ToolResult { Name = Name, Success = true, Content = $"已取消提醒：{reminderId}" };
        }

        return Fail($"提醒不存在：{reminderId}");
    }

    private async Task<ToolResult> ShowToastAsync(JsonElement root, CancellationToken ct)
    {
        var title = root.TryGetProperty("title", out var tEl) ? tEl.GetString() ?? "" : "";
        var message = root.TryGetProperty("message", out var mEl) ? mEl.GetString() ?? "" : "";

        if (string.IsNullOrEmpty(title)) return Fail("缺少 title 参数");

        ShowBalloonTip(title, message);

        return new ToolResult
        {
            Name = Name,
            Success = true,
            Content = $"已显示 Toast：{title}"
        };
    }

    private static void ShowBalloonTip(string title, string message)
    {
        try
        {
            // 使用 Process 启动 PowerShell 显示通知
            var script = $@"
                [Windows.UI.Notifications.ToastNotificationManager, Windows.UI.Notifications, ContentType = WindowsRuntime] | Out-Null
                [Windows.Data.Xml.Dom.XmlDocument, Windows.Data.Xml.Dom, ContentType = WindowsRuntime] | Out-Null
                $template = '<toast><visual><binding template=""ToastText02""><text id=""1"">{title}</text><text id=""2"">{message}</text></binding></visual></toast>'
                $xml = New-Object Windows.Data.Xml.Dom.XmlDocument
                $xml.LoadXml($template)
                $toast = [Windows.UI.Notifications.ToastNotification]::new($xml)
                [Windows.UI.Notifications.ToastNotificationManager]::CreateToastNotifier('DesktopMascot').Show($toast)
            ";

            var startInfo = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -Command \"{script}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            var process = Process.Start(startInfo);
            process?.WaitForExit(5000);
        }
        catch
        {
            // 降级到简单消息
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "msg.exe",
                    Arguments = $"* \"{title}: {message}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true
                });
            }
            catch { }
        }
    }

    private static ToolResult Fail(string error) => new() { Name = "notification", Success = false, Error = error };
}
