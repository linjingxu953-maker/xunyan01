using DesktopMascot.Core.Tools;
using System.Net;
using System.Net.Mail;
using System.Text;
using System.Text.Json;
using DesktopMascot.Agent.Models;

namespace DesktopMascot.Agent.Tools;

/// <summary>
/// 邮件操作工具 - 发送邮件、读取邮件、管理邮箱
/// </summary>
public class EmailTool : ITool
{
    private string _smtpHost = "";
    private int _smtpPort = 587;
    private string _smtpUser = "";
    private string _smtpPassword = "";

    public string Name => "email";
    public string Description => "邮件操作：发送邮件、读取邮件、管理邮箱。";
    public string ParametersSchema => """
    {
        "type": "object",
        "properties": {
            "action": { "type": "string", "enum": ["configure", "send", "send_html", "test_connection"], "description": "操作类型" },
            "smtp_host": { "type": "string", "description": "SMTP 服务器" },
            "smtp_port": { "type": "integer", "description": "SMTP 端口" },
            "username": { "type": "string", "description": "用户名" },
            "password": { "type": "string", "description": "密码" },
            "to": { "type": "string", "description": "收件人" },
            "subject": { "type": "string", "description": "主题" },
            "body": { "type": "string", "description": "正文" }
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
                "configure" => Configure(root),
                "send" => await SendEmailAsync(root, false, ct),
                "send_html" => await SendEmailAsync(root, true, ct),
                "test_connection" => await TestConnectionAsync(ct),
                _ => Fail($"不支持的操作：{action}")
            };
        }
        catch (Exception ex)
        {
            return Fail($"邮件操作失败：{ex.Message}");
        }
    }

    private ToolResult Configure(JsonElement root)
    {
        _smtpHost = root.TryGetProperty("smtp_host", out var hEl) ? hEl.GetString() ?? "" : "";
        _smtpPort = root.TryGetProperty("smtp_port", out var pEl) ? pEl.GetInt32() : 587;
        _smtpUser = root.TryGetProperty("username", out var uEl) ? uEl.GetString() ?? "" : "";
        _smtpPassword = root.TryGetProperty("password", out var pwEl) ? pwEl.GetString() ?? "" : "";

        if (string.IsNullOrEmpty(_smtpHost))
            return Fail("缺少 smtp_host 参数");

        return new ToolResult
        {
            Name = Name,
            Success = true,
            Content = $"已配置邮件服务器\nSMTP: {_smtpHost}:{_smtpPort}\n用户: {_smtpUser}"
        };
    }

    private async Task<ToolResult> SendEmailAsync(JsonElement root, bool isHtml, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(_smtpHost))
            return Fail("请先配置邮件服务器");

        var to = root.TryGetProperty("to", out var toEl) ? toEl.GetString() ?? "" : "";
        var subject = root.TryGetProperty("subject", out var sEl) ? sEl.GetString() ?? "" : "";
        var body = root.TryGetProperty("body", out var bEl) ? bEl.GetString() ?? "" : "";

        if (string.IsNullOrEmpty(to)) return Fail("缺少 to 参数");
        if (string.IsNullOrEmpty(subject)) return Fail("缺少 subject 参数");

        using var client = new SmtpClient(_smtpHost, _smtpPort)
        {
            EnableSsl = true,
            Credentials = new NetworkCredential(_smtpUser, _smtpPassword)
        };

        using var message = new MailMessage(_smtpUser, to, subject, body)
        {
            IsBodyHtml = isHtml
        };

        await client.SendMailAsync(message, ct);

        return new ToolResult
        {
            Name = Name,
            Success = true,
            Content = $"邮件已发送\n收件人：{to}\n主题：{subject}\n格式：{(isHtml ? "HTML" : "纯文本")}"
        };
    }

    private async Task<ToolResult> TestConnectionAsync(CancellationToken ct)
    {
        if (string.IsNullOrEmpty(_smtpHost))
            return Fail("请先配置邮件服务器");

        try
        {
            using var client = new SmtpClient(_smtpHost, _smtpPort)
            {
                EnableSsl = true,
                Credentials = new NetworkCredential(_smtpUser, _smtpPassword),
                Timeout = 10000
            };

            await client.SendMailAsync(new MailMessage(_smtpUser, _smtpUser, "测试连接", "这是一封测试邮件"), ct);

            return new ToolResult
            {
                Name = Name,
                Success = true,
                Content = "邮件服务器连接成功"
            };
        }
        catch (Exception ex)
        {
            return Fail($"连接失败：{ex.Message}");
        }
    }

    private static ToolResult Fail(string error) => new() { Name = "email", Success = false, Error = error };
}
