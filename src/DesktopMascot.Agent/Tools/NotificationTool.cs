using DesktopMascot.Core.Tools;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using DesktopMascot.Agent.Models;

namespace DesktopMascot.Agent.Tools;

/// <summary>
/// 通知工具增强版 — 持久化提醒 + 通知历史 + 重复提醒 + 优先级 + 静音
/// </summary>
public class NotificationTool : ITool
{
    private readonly string _dataDir;
    private readonly List<ReminderEntry> _reminders = new();
    private readonly List<NotificationHistoryEntry> _history = new();
    private readonly object _lock = new();
    private readonly Timer? _timer;

    public NotificationTool() : this(null)
    {
    }

    public string Name => "notification";
    public string Description => "通知工具：系统通知、定时提醒（支持重复）、通知历史、静音模式。";
    public string ParametersSchema => """
    {
        "type": "object",
        "properties": {
            "action": { "type": "string", "enum": ["notify", "toast", "reminder", "list_reminders", "cancel_reminder", "update_reminder", "history", "clear_history", "snooze", "sound"], "description": "操作类型" },
            "title": { "type": "string", "description": "通知标题" },
            "message": { "type": "string", "description": "通知内容" },
            "delay_seconds": { "type": "integer", "description": "延迟秒数" },
            "reminder_id": { "type": "string", "description": "提醒ID" },
            "priority": { "type": "string", "enum": ["low", "normal", "high", "urgent"], "description": "优先级" },
            "repeat": { "type": "integer", "description": "重复次数（0=不重复）" },
            "repeat_interval": { "type": "integer", "description": "重复间隔秒数" },
            "silent": { "type": "boolean", "description": "静音（不播放声音）" },
            "history_limit": { "type": "integer", "description": "历史记录数量限制" },
            "sound_file": { "type": "string", "description": "自定义提示音文件路径" }
        },
        "required": ["action"]
    }
    """;

    public NotificationTool(string? dataDirectory = null)
    {
        _dataDir = ResolveDataDirectory(dataDirectory);
        LoadReminders();
        LoadHistory();
        _timer = new Timer(CheckReminders, null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
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
                "notify" => await SendNotificationAsync(root, ct),
                "toast" => await SendNotificationAsync(root, ct),
                "reminder" => await SetReminderAsync(root, ct),
                "list_reminders" => ListReminders(),
                "cancel_reminder" => CancelReminder(root),
                "update_reminder" => UpdateReminder(root),
                "history" => GetHistory(root),
                "clear_history" => ClearHistory(),
                "snooze" => await SnoozeReminderAsync(root, ct),
                "sound" => await PlaySoundAsync(root, ct),
                _ => Fail($"不支持的操作：{action}")
            };
        }
        catch (Exception ex)
        {
            return Fail($"通知操作失败：{ex.Message}");
        }
    }

    #region 通知

    private async Task<ToolResult> SendNotificationAsync(JsonElement root, CancellationToken ct)
    {
        var title = GetRequiredString(root, "title");
        var message = root.TryGetProperty("message", out var mEl) ? mEl.GetString() ?? "" : "";
        var priority = root.TryGetProperty("priority", out var pEl) ? pEl.GetString() ?? "normal" : "normal";
        var silent = root.TryGetProperty("silent", out var sEl) && sEl.GetBoolean();

        if (title == null) return Fail("缺少 title 参数");

        ShowBalloonTip(title, message);

        if (!silent && priority is "high" or "urgent")
            PlayDefaultSound();

        AddHistoryEntry(title, message, priority);

        return new ToolResult
        {
            Name = Name,
            Success = true,
            Content = $"已发送通知\n标题：{title}\n内容：{message}\n优先级：{priority}"
        };
    }

    #endregion

    #region 提醒

    private async Task<ToolResult> SetReminderAsync(JsonElement root, CancellationToken ct)
    {
        var title = GetRequiredString(root, "title");
        var message = root.TryGetProperty("message", out var mEl) ? mEl.GetString() ?? "" : "";
        var delaySeconds = root.TryGetProperty("delay_seconds", out var dEl) ? dEl.GetInt32() : 60;
        var priority = root.TryGetProperty("priority", out var pEl) ? pEl.GetString() ?? "normal" : "normal";
        var repeat = root.TryGetProperty("repeat", out var rpEl) ? rpEl.GetInt32() : 0;
        var repeatInterval = root.TryGetProperty("repeat_interval", out var riEl) ? riEl.GetInt32() : 0;
        var silent = root.TryGetProperty("silent", out var sEl) && sEl.GetBoolean();

        if (title == null) return Fail("缺少 title 参数");

        var reminderId = Guid.NewGuid().ToString("N")[..8];
        var triggerTime = DateTime.Now.AddSeconds(delaySeconds);

        var entry = new ReminderEntry
        {
            Id = reminderId,
            Title = title,
            Message = message,
            TriggerTime = triggerTime,
            Priority = priority,
            RepeatCount = repeat,
            RepeatInterval = repeatInterval,
            RemainingRepeats = repeat,
            Silent = silent,
            CreatedAt = DateTime.Now,
            Status = "active"
        };

        lock (_lock)
        {
            _reminders.Add(entry);
            SaveReminders();
        }

        var sb = new StringBuilder();
        sb.AppendLine("已设置提醒");
        sb.AppendLine($"  ID：{reminderId}");
        sb.AppendLine($"  标题：{title}");
        sb.AppendLine($"  触发时间：{triggerTime:HH:mm:ss}");
        sb.AppendLine($"  优先级：{priority}");
        if (repeat > 0)
            sb.AppendLine($"  重复：{repeat} 次，间隔 {repeatInterval} 秒");

        return new ToolResult { Name = Name, Success = true, Content = sb.ToString() };
    }

    private ToolResult ListReminders()
    {
        lock (_lock)
        {
            var sb = new StringBuilder();
            sb.AppendLine("当前提醒列表");

            var active = _reminders.Where(r => r.Status == "active").ToList();
            if (active.Count == 0)
            {
                sb.AppendLine("  暂无提醒");
            }
            else
            {
                foreach (var r in active)
                {
                    var remaining = r.TriggerTime > DateTime.Now
                        ? $"还有 {(r.TriggerTime - DateTime.Now).TotalSeconds:F0} 秒"
                        : "即将触发";
                    sb.AppendLine($"  [{r.Id}] {r.Title} — {r.TriggerTime:HH:mm:ss}（{remaining}）优先级:{r.Priority}");
                }
            }

            return new ToolResult { Name = Name, Success = true, Content = sb.ToString() };
        }
    }

    private ToolResult CancelReminder(JsonElement root)
    {
        var reminderId = GetRequiredString(root, "reminder_id");
        if (reminderId == null) return Fail("缺少 reminder_id 参数");

        lock (_lock)
        {
            var entry = _reminders.FirstOrDefault(r => r.Id == reminderId && r.Status == "active");
            if (entry == null)
                return Fail($"提醒不存在：{reminderId}");

            entry.Status = "cancelled";
            SaveReminders();
            return new ToolResult { Name = Name, Success = true, Content = $"已取消提醒：{reminderId}（{entry.Title}）" };
        }
    }

    private ToolResult UpdateReminder(JsonElement root)
    {
        var reminderId = GetRequiredString(root, "reminder_id");
        if (reminderId == null) return Fail("缺少 reminder_id 参数");

        lock (_lock)
        {
            var entry = _reminders.FirstOrDefault(r => r.Id == reminderId && r.Status == "active");
            if (entry == null)
                return Fail($"提醒不存在：{reminderId}");

            if (root.TryGetProperty("title", out var tEl))
                entry.Title = tEl.GetString() ?? entry.Title;
            if (root.TryGetProperty("message", out var mEl))
                entry.Message = mEl.GetString() ?? entry.Message;
            if (root.TryGetProperty("priority", out var pEl))
                entry.Priority = pEl.GetString() ?? entry.Priority;

            SaveReminders();
            return new ToolResult { Name = Name, Success = true, Content = $"已更新提醒：{reminderId}" };
        }
    }

    private async Task<ToolResult> SnoozeReminderAsync(JsonElement root, CancellationToken ct)
    {
        var reminderId = GetRequiredString(root, "reminder_id");
        var delaySeconds = root.TryGetProperty("delay_seconds", out var dEl) ? dEl.GetInt32() : 300;

        if (reminderId == null) return Fail("缺少 reminder_id 参数");

        lock (_lock)
        {
            var entry = _reminders.FirstOrDefault(r => r.Id == reminderId && r.Status == "active");
            if (entry == null)
                return Fail($"提醒不存在：{reminderId}");

            entry.TriggerTime = DateTime.Now.AddSeconds(delaySeconds);
            entry.RemainingRepeats = entry.RepeatCount;
            SaveReminders();

            return new ToolResult
            {
                Name = Name,
                Success = true,
                Content = $"已推迟提醒：{reminderId}（{delaySeconds} 秒后）"
            };
        }
    }

    private void CheckReminders(object? state)
    {
        lock (_lock)
        {
            var now = DateTime.Now;
            var triggered = _reminders
                .Where(r => r.Status == "active" && r.TriggerTime <= now)
                .ToList();

            foreach (var entry in triggered)
            {
                ShowBalloonTip($"提醒：{entry.Title}", entry.Message);

                if (!entry.Silent && entry.Priority is "high" or "urgent")
                    PlayDefaultSound();

                AddHistoryEntry(entry.Title, entry.Message, entry.Priority);

                if (entry.RemainingRepeats > 0)
                {
                    entry.RemainingRepeats--;
                    entry.TriggerTime = now.AddSeconds(entry.RepeatInterval);
                }
                else
                {
                    entry.Status = "completed";
                }
            }

            SaveReminders();
        }
    }

    #endregion

    #region 历史

    private ToolResult GetHistory(JsonElement root)
    {
        var limit = root.TryGetProperty("history_limit", out var lEl) ? lEl.GetInt32() : 20;

        lock (_lock)
        {
            var entries = _history
                .OrderByDescending(h => h.Timestamp)
                .Take(limit)
                .ToList();

            var sb = new StringBuilder();
            sb.AppendLine($"通知历史（最近 {entries.Count} 条）");

            foreach (var h in entries)
            {
                sb.AppendLine($"  [{h.Timestamp:HH:mm:ss}] [{h.Priority}] {h.Title}");
                if (!string.IsNullOrEmpty(h.Message))
                    sb.AppendLine($"    {h.Message}");
            }

            if (entries.Count == 0) sb.AppendLine("  暂无历史");

            return new ToolResult { Name = Name, Success = true, Content = sb.ToString() };
        }
    }

    private ToolResult ClearHistory()
    {
        lock (_lock)
        {
            _history.Clear();
            SaveHistory();
        }
        return new ToolResult { Name = Name, Success = true, Content = "通知历史已清空" };
    }

    #endregion

    #region 声音

    private async Task<ToolResult> PlaySoundAsync(JsonElement root, CancellationToken ct)
    {
        var soundFile = root.TryGetProperty("sound_file", out var sfEl) ? sfEl.GetString() : null;

        if (!string.IsNullOrEmpty(soundFile) && File.Exists(soundFile))
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = soundFile,
                    UseShellExecute = true,
                    CreateNoWindow = true
                };
                Process.Start(psi);
                return new ToolResult { Name = Name, Success = true, Content = $"已播放提示音：{soundFile}" };
            }
            catch (Exception ex)
            {
                return Fail($"播放失败：{ex.Message}");
            }
        }

        PlayDefaultSound();
        return new ToolResult { Name = Name, Success = true, Content = "已播放默认提示音" };
    }

    private static void PlayDefaultSound()
    {
        try
        {
            Console.Beep(1000, 200);
            Console.Beep(1500, 200);
        }
        catch { }
    }

    #endregion

    #region 持久化

    private void LoadReminders()
    {
        var path = Path.Combine(_dataDir, "reminders.json");
        if (!File.Exists(path)) return;

        try
        {
            var json = File.ReadAllText(path);
            var entries = JsonSerializer.Deserialize<List<ReminderEntry>>(json) ?? new();
            _reminders.AddRange(entries);
        }
        catch { }
    }

    private void SaveReminders()
    {
        var path = Path.Combine(_dataDir, "reminders.json");
        var json = JsonSerializer.Serialize(_reminders, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(path, json);
    }

    private void LoadHistory()
    {
        var path = Path.Combine(_dataDir, "history.json");
        if (!File.Exists(path)) return;

        try
        {
            var json = File.ReadAllText(path);
            var entries = JsonSerializer.Deserialize<List<NotificationHistoryEntry>>(json) ?? new();
            _history.AddRange(entries);
        }
        catch { }
    }

    private void SaveHistory()
    {
        var path = Path.Combine(_dataDir, "history.json");
        // 只保留最近 100 条
        var toSave = _history.OrderByDescending(h => h.Timestamp).Take(100).ToList();
        var json = JsonSerializer.Serialize(toSave, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(path, json);
    }

    private void AddHistoryEntry(string title, string message, string priority)
    {
        lock (_lock)
        {
            _history.Add(new NotificationHistoryEntry
            {
                Title = title,
                Message = message,
                Priority = priority,
                Timestamp = DateTime.Now
            });
            SaveHistory();
        }
    }

    #endregion

    #region 通知发送

    private static void ShowBalloonTip(string title, string message)
    {
        try
        {
            var escapedTitle = title.Replace("'", "''");
            var escapedMessage = message.Replace("'", "''");
            var script = $@"
                [Windows.UI.Notifications.ToastNotificationManager, Windows.UI.Notifications, ContentType = WindowsRuntime] | Out-Null
                [Windows.Data.Xml.Dom.XmlDocument, Windows.Data.Xml.Dom, ContentType = WindowsRuntime] | Out-Null
                $template = '<toast><visual><binding template=""ToastText02""><text id=""1"">{escapedTitle}</text><text id=""2"">{escapedMessage}</text></binding></visual></toast>'
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

    #endregion

    #region Helpers

    private static string? GetRequiredString(JsonElement root, string name)
    {
        return root.TryGetProperty(name, out var el) ? el.GetString() : null;
    }

    private static string ResolveDataDirectory(string? dataDirectory)
    {
        var preferred = dataDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "DesktopMascot", "notifications");

        try
        {
            Directory.CreateDirectory(preferred);
            return preferred;
        }
        catch (UnauthorizedAccessException)
        {
            return CreateFallbackDataDirectory();
        }
        catch (IOException)
        {
            return CreateFallbackDataDirectory();
        }
    }

    private static string CreateFallbackDataDirectory()
    {
        var fallback = Path.Combine(Path.GetTempPath(), "DesktopMascot", "notifications");
        Directory.CreateDirectory(fallback);
        return fallback;
    }

    private static ToolResult Fail(string error) => new() { Name = "notification", Success = false, Error = error };

    #endregion
}

internal class ReminderEntry
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public string Message { get; set; } = "";
    public DateTime TriggerTime { get; set; }
    public string Priority { get; set; } = "normal";
    public int RepeatCount { get; set; }
    public int RepeatInterval { get; set; }
    public int RemainingRepeats { get; set; }
    public bool Silent { get; set; }
    public DateTime CreatedAt { get; set; }
    public string Status { get; set; } = "active";
}

internal class NotificationHistoryEntry
{
    public string Title { get; set; } = "";
    public string Message { get; set; } = "";
    public string Priority { get; set; } = "normal";
    public DateTime Timestamp { get; set; }
}
