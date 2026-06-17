using DesktopMascot.Core.Tools;
using System.Diagnostics;
using System.Text.Json;
using DesktopMascot.Agent.Models;

namespace DesktopMascot.Agent.Tools;

/// <summary>
/// 命令执行工具 - 在系统上执行命令（需权限确认）
/// </summary>
public class RunCommandTool : ITool
{
    public string Name => "run_command";
    public string Description => "执行系统命令（如 npm install、git status 等）。需要用户确认权限。危险命令（rm -rf、DROP TABLE 等）会被拒绝。";
    public bool RequiresConfirmation => true;
    public string ConfirmationMessage => "AI 想要执行命令，是否允许？";
    public string ParametersSchema => """
    {
        "type": "object",
        "properties": {
            "command": {
                "type": "string",
                "description": "要执行的命令"
            },
            "working_directory": {
                "type": "string",
                "description": "工作目录（可选）"
            },
            "timeout_seconds": {
                "type": "integer",
                "description": "超时时间秒数（默认 30）"
            }
        },
        "required": ["command"]
    }
    """;

    private static readonly string[] DangerousCommands = new[]
    {
        "rm -rf", "rm -r", "rmdir /s", "del /f", "del /s",
        "DROP TABLE", "DROP DATABASE", "TRUNCATE",
        "FORMAT", "fdisk", "mkfs",
        "shutdown", "reboot", "halt",
        "chmod 777", "chown -R",
        "git push --force", "git reset --hard"
    };

    public async Task<ToolResult> ExecuteAsync(string arguments, CancellationToken ct = default)
    {
        try
        {
            var doc = JsonDocument.Parse(arguments);
            var root = doc.RootElement;

            if (!root.TryGetProperty("command", out var commandElement))
            {
                return new ToolResult
                {
                    Name = Name,
                    Success = false,
                    Error = "缺少 command 参数"
                };
            }

            var command = commandElement.GetString() ?? "";
            if (string.IsNullOrWhiteSpace(command))
            {
                return new ToolResult
                {
                    Name = Name,
                    Success = false,
                    Error = "命令不能为空"
                };
            }

            if (IsDangerousCommand(command))
            {
                return new ToolResult
                {
                    Name = Name,
                    Success = false,
                    Error = $"危险命令被拒绝：{command}\n此命令可能导致不可逆的数据丢失。"
                };
            }

            var workingDir = root.TryGetProperty("working_directory", out var wdElement)
                ? wdElement.GetString()
                : null;

            var timeoutSeconds = root.TryGetProperty("timeout_seconds", out var timeoutElement)
                ? timeoutElement.GetInt32()
                : 30;

            var result = await ExecuteCommandAsync(command, workingDir, timeoutSeconds, ct);

            return new ToolResult
            {
                Name = Name,
                Success = result.ExitCode == 0,
                Content = result.ExitCode == 0
                    ? $"命令执行成功（退出码 0）\n输出：\n{result.Output}"
                    : $"命令执行完成（退出码 {result.ExitCode}）\n输出：\n{result.Output}\n错误：\n{result.Error}"
            };
        }
        catch (Exception ex)
        {
            return new ToolResult
            {
                Name = Name,
                Success = false,
                Error = $"执行命令失败：{ex.Message}"
            };
        }
    }

    private static bool IsDangerousCommand(string command)
    {
        var upper = command.ToUpperInvariant();
        return DangerousCommands.Any(d => upper.Contains(d.ToUpperInvariant()));
    }

    private static async Task<CommandResult> ExecuteCommandAsync(
        string command, string? workingDirectory, int timeoutSeconds, CancellationToken ct)
    {
        var result = new CommandResult();

        try
        {
            var isWindows = OperatingSystem.IsWindows();
            var fileName = isWindows ? "cmd.exe" : "/bin/bash";
            var arguments = isWindows ? $"/c {command}" : $"-c \"{command.Replace("\"", "\\\"")}\"";

            var startInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            if (!string.IsNullOrEmpty(workingDirectory) && Directory.Exists(workingDirectory))
            {
                startInfo.WorkingDirectory = workingDirectory;
            }

            using var process = new Process { StartInfo = startInfo };
            process.Start();

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

            try
            {
                result.Output = await process.StandardOutput.ReadToEndAsync(cts.Token);
                result.Error = await process.StandardError.ReadToEndAsync(cts.Token);
                await process.WaitForExitAsync(cts.Token);
                result.ExitCode = process.ExitCode;
            }
            catch (OperationCanceledException)
            {
                try { process.Kill(); } catch { }
                result.ExitCode = -1;
                result.Error = $"命令超时（{timeoutSeconds}秒）";
            }
        }
        catch (Exception ex)
        {
            result.ExitCode = -1;
            result.Error = ex.Message;
        }

        return result;
    }

    private class CommandResult
    {
        public int ExitCode { get; set; }
        public string Output { get; set; } = string.Empty;
        public string Error { get; set; } = string.Empty;
    }
}
