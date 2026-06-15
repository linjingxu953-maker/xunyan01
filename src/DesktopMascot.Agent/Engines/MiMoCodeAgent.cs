using System.Diagnostics;
using System.Text.Json;
using DesktopMascot.Core.Enums;
using DesktopMascot.Core.Interfaces;
using DesktopMascot.Core.Models;

namespace DesktopMascot.Agent.Engines;

/// <summary>
/// MiMo Code Agent 配置
/// </summary>
public class MiMoCodeConfig
{
    /// <summary>MiMo Code 可执行文件路径</summary>
    public string ExecutablePath { get; set; } = "mimo";

    /// <summary>默认模型（格式：provider/model）</summary>
    public string DefaultModel { get; set; } = "deepseek/deepseek-chat";

    /// <summary>模型服务商名称</summary>
    public string ProviderName { get; set; } = string.Empty;

    /// <summary>用户配置的 API Key（仅传递给子进程环境变量）</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>用户配置的 API 端点（仅传递给子进程环境变量）</summary>
    public string ApiEndpoint { get; set; } = string.Empty;

    /// <summary>模型配置来源：AppProvider 或 MimoLocalConfig</summary>
    public string ModelConfigMode { get; set; } = "AppProvider";

    /// <summary>工作目录</summary>
    public string? WorkingDirectory { get; set; }

    /// <summary>是否使用纯模式（禁用插件）</summary>
    public bool PureMode { get; set; } = true;

    /// <summary>超时时间（秒）</summary>
    public int TimeoutSeconds { get; set; } = 300;
}

/// <summary>
/// MiMo Code Agent - 通过 CLI 调用 MiMo Code
/// </summary>
public class MiMoCodeAgent : IAgentEngine
{
    private readonly MiMoCodeConfig _config;
    private readonly ITaskEventStream? _eventStream;

    public MiMoCodeAgent(
        MiMoCodeConfig config,
        ITaskEventStream? eventStream = null)
    {
        _config = config;
        _eventStream = eventStream;
    }

    public async Task<TaskResult> ExecuteAsync(AgentTask task, CancellationToken ct = default)
    {
        _eventStream?.Publish(TaskEvent.ProgressUpdated(
            task.Id, 10, "正在启动 MiMo Code..."));

        try
        {
            var arguments = BuildArguments(task);

            _eventStream?.Publish(TaskEvent.ProgressUpdated(
                task.Id, 20, $"执行命令: mimo {arguments}"));

            var output = await RunMiMoCodeAsync(arguments, ct);

            _eventStream?.Publish(TaskEvent.ProgressUpdated(
                task.Id, 90, "MiMo Code 执行完成"));

            return new TaskResult
            {
                TaskId = task.Id,
                Success = true,
                Content = output
            };
        }
        catch (OperationCanceledException)
        {
            return TaskResult.Failed(task.Id, "MiMo Code 执行超时");
        }
        catch (Exception ex)
        {
            return TaskResult.Failed(task.Id, $"MiMo Code 执行失败: {ex.Message}");
        }
    }

    public async IAsyncEnumerable<string> ExecuteStreamingAsync(AgentTask task, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        _eventStream?.Publish(TaskEvent.ProgressUpdated(
            task.Id, 10, "正在启动 MiMo Code（流式）..."));

        var arguments = BuildArguments(task) + " --stream";

        var process = new Process
        {
            StartInfo = BuildProcessStartInfo(arguments)
        };

        ApplyModelEnvironment(process.StartInfo);
        process.Start();

        using var reader = process.StandardOutput;
        while (!reader.EndOfStream)
        {
            ct.ThrowIfCancellationRequested();
            var line = await reader.ReadLineAsync(ct);
            if (!string.IsNullOrEmpty(line))
            {
                _eventStream?.Publish(TaskEvent.LlmStreamChunk(task.Id, line));
                yield return line;
            }
        }

        await process.WaitForExitAsync(ct);

        _eventStream?.Publish(TaskEvent.ProgressUpdated(
            task.Id, 90, "MiMo Code 流式执行完成"));
    }

    private string BuildArguments(AgentTask task)
    {
        var args = new List<string>();

        // 添加消息
        args.Add($"run \"{EscapeArgument(task.Input)}\"");

        // 添加模型。使用 MiMo 本机配置时不强制覆盖模型。
        if (!string.IsNullOrWhiteSpace(_config.DefaultModel))
        {
            args.Add($"--model {_config.DefaultModel}");
        }

        // 添加工作目录
        if (!string.IsNullOrEmpty(_config.WorkingDirectory))
        {
            args.Add($"--dir \"{_config.WorkingDirectory}\"");
        }

        // 纯模式
        if (_config.PureMode)
        {
            args.Add("--pure");
        }

        return string.Join(" ", args);
    }

    private async Task<string> RunMiMoCodeAsync(string arguments, CancellationToken ct)
    {
        var timeoutMs = _config.TimeoutSeconds * 1000;
        var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(timeoutMs);

        var process = new Process
        {
            StartInfo = BuildProcessStartInfo(arguments)
        };

        ApplyModelEnvironment(process.StartInfo);
        process.Start();

        var output = await process.StandardOutput.ReadToEndAsync(cts.Token);
        var error = await process.StandardError.ReadToEndAsync(cts.Token);

        await process.WaitForExitAsync(cts.Token);

        if (process.ExitCode != 0 && !string.IsNullOrEmpty(error))
        {
            throw new InvalidOperationException($"MiMo Code 错误 (exit {process.ExitCode}): {error}");
        }

        return output;
    }

    private ProcessStartInfo BuildProcessStartInfo(string arguments)
    {
        var executablePath = ResolveExecutablePath(_config.ExecutablePath) ?? _config.ExecutablePath;
        var workingDirectory = _config.WorkingDirectory ?? Environment.CurrentDirectory;
        var extension = Path.GetExtension(executablePath).ToLowerInvariant();

        var startInfo = new ProcessStartInfo
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = workingDirectory
        };

        if (extension is ".cmd" or ".bat")
        {
            startInfo.FileName = Environment.GetEnvironmentVariable("ComSpec") ?? "cmd.exe";
            startInfo.Arguments = $"/d /s /c \"\"{executablePath}\" {arguments}\"";
            return startInfo;
        }

        startInfo.FileName = executablePath;
        startInfo.Arguments = arguments;
        return startInfo;
    }

    private static string? ResolveExecutablePath(string executablePath)
    {
        executablePath = executablePath.Trim().Trim('"');

        if (HasDirectoryPart(executablePath))
        {
            var fullPath = Path.GetFullPath(executablePath);
            if (!string.IsNullOrWhiteSpace(Path.GetExtension(fullPath)))
                return File.Exists(fullPath) ? fullPath : null;

            var directory = Path.GetDirectoryName(fullPath);
            var fileName = Path.GetFileName(fullPath);
            return string.IsNullOrWhiteSpace(directory) || string.IsNullOrWhiteSpace(fileName)
                ? null
                : ResolveFromDirectory(directory, fileName);
        }

        var pathValues = (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var directory in pathValues)
        {
            var resolved = ResolveFromDirectory(directory, executablePath);
            if (resolved is not null)
                return resolved;
        }

        return null;
    }

    private static string? ResolveFromDirectory(string directory, string executableName)
    {
        foreach (var extension in GetExecutableExtensions(executableName))
        {
            var candidate = Path.Combine(directory, executableName + extension);
            if (File.Exists(candidate))
                return candidate;
        }

        return null;
    }

    private static bool HasDirectoryPart(string value)
    {
        return Path.IsPathRooted(value) ||
               value.Contains(Path.DirectorySeparatorChar) ||
               value.Contains(Path.AltDirectorySeparatorChar);
    }

    private static IEnumerable<string> GetExecutableExtensions(string executablePath)
    {
        if (!string.IsNullOrWhiteSpace(Path.GetExtension(executablePath)))
        {
            yield return string.Empty;
            yield break;
        }

        if (!OperatingSystem.IsWindows())
        {
            yield return string.Empty;
            yield break;
        }

        var pathExt = Environment.GetEnvironmentVariable("PATHEXT") ?? ".COM;.EXE;.BAT;.CMD";
        var emitted = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var extension in pathExt.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var normalized = extension.StartsWith('.') ? extension : $".{extension}";
            if (normalized.Equals(".PS1", StringComparison.OrdinalIgnoreCase))
                continue;

            if (emitted.Add(normalized))
                yield return normalized;
        }

        yield return string.Empty;
    }

    private static string EscapeArgument(string arg)
    {
        // 转义双引号
        return arg.Replace("\"", "\\\"");
    }

    private void ApplyModelEnvironment(ProcessStartInfo startInfo)
    {
        if (_config.ModelConfigMode == "MimoLocalConfig")
            return;

        if (!string.IsNullOrWhiteSpace(_config.ApiKey))
        {
            startInfo.Environment["OPENAI_API_KEY"] = _config.ApiKey;
            startInfo.Environment["MIMO_API_KEY"] = _config.ApiKey;

            var providerKeyName = GetProviderApiKeyEnvironmentName(_config.ProviderName);
            if (!string.IsNullOrWhiteSpace(providerKeyName))
            {
                startInfo.Environment[providerKeyName] = _config.ApiKey;
            }
        }

        if (!string.IsNullOrWhiteSpace(_config.ApiEndpoint))
        {
            startInfo.Environment["OPENAI_BASE_URL"] = _config.ApiEndpoint;
            startInfo.Environment["OPENAI_API_BASE"] = _config.ApiEndpoint;
            startInfo.Environment["MIMO_BASE_URL"] = _config.ApiEndpoint;
        }
    }

    private static string GetProviderApiKeyEnvironmentName(string providerName)
    {
        return providerName.ToLowerInvariant() switch
        {
            "deepseek" => "DEEPSEEK_API_KEY",
            "kimi" or "moonshot" => "MOONSHOT_API_KEY",
            "zhipu" or "glm" => "ZHIPU_API_KEY",
            "baichuan" => "BAICHUAN_API_KEY",
            "tongyi" or "qwen" => "DASHSCOPE_API_KEY",
            "doubao" => "ARK_API_KEY",
            "yi" => "YI_API_KEY",
            "minimax" => "MINIMAX_API_KEY",
            "stepfun" => "STEPFUN_API_KEY",
            "xunfei" => "XUNFEI_API_KEY",
            _ => string.Empty
        };
    }
}
