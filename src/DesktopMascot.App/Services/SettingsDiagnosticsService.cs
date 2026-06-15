using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using DesktopMascot.UI.Services;

namespace DesktopMascot.App.Services;

public sealed class SettingsDiagnosticsService : ISettingsDiagnosticsService
{
    private static readonly TimeSpan ModelTestTimeout = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan MimoVersionTimeout = TimeSpan.FromSeconds(5);

    public async Task<SettingsDiagnosticsResult> TestModelConnectionAsync(
        ModelConnectionTestRequest request,
        CancellationToken ct = default)
    {
        var providerName = NormalizeProviderName(request.ProviderName);
        var baseUrl = NormalizeBaseUrl(request.ApiEndpoint);
        var modelName = request.ModelName.Trim();
        var apiKey = request.ApiKey.Trim();

        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            return Failure("Base URL 不能为空。", "请填写 Provider 的 OpenAI-compatible Base URL。");
        }

        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var baseUri))
        {
            return Failure("Base URL 格式无效。", baseUrl);
        }

        if (string.IsNullOrWhiteSpace(modelName))
        {
            return Failure("模型名不能为空。", "请填写要测试的模型名。");
        }

        if (providerName != "local" && string.IsNullOrWhiteSpace(apiKey))
        {
            return Failure("API Key 为空。", "远程 Provider 需要用户自己的 API Key；应用不会内置或代管 Key。");
        }

        using var httpClient = new HttpClient { Timeout = ModelTestTimeout };
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, BuildChatCompletionsUri(baseUri));

        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        }

        var body = new
        {
            model = modelName,
            messages = new[]
            {
                new { role = "system", content = "You are a connection test endpoint." },
                new { role = "user", content = "Reply with OK." }
            },
            temperature = 0,
            max_tokens = 8
        };

        httpRequest.Content = new StringContent(
            JsonSerializer.Serialize(body),
            Encoding.UTF8,
            "application/json");

        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(ModelTestTimeout);

            using var response = await httpClient.SendAsync(httpRequest, timeoutCts.Token);
            var payload = await response.Content.ReadAsStringAsync(timeoutCts.Token);

            if (!response.IsSuccessStatusCode)
            {
                return Failure(
                    $"连接失败：{(int)response.StatusCode} {response.ReasonPhrase}",
                    Truncate(payload, 360));
            }

            return Success(
                $"连接成功：{request.ProviderName} / {modelName}",
                ExtractAssistantPreview(payload));
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return Failure("连接超时。", $"超过 {ModelTestTimeout.TotalSeconds:0} 秒未收到响应。");
        }
        catch (Exception ex)
        {
            return Failure("连接异常。", ex.Message);
        }
    }

    public async Task<SettingsDiagnosticsResult> TestMimoCodeAsync(
        MimoCodeConnectionTestRequest request,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.ExecutablePath))
        {
            return Failure("Mimo Code 可执行路径不能为空。", "请填写 mimo、mimo.exe 或完整路径。");
        }

        var resolvedExecutable = ResolveExecutablePath(request.ExecutablePath.Trim());
        if (resolvedExecutable is null)
        {
            return Failure("未找到 Mimo Code 可执行文件。", $"无法解析：{request.ExecutablePath}");
        }

        var workspace = string.IsNullOrWhiteSpace(request.WorkspacePath)
            ? Environment.CurrentDirectory
            : request.WorkspacePath.Trim();

        if (!Directory.Exists(workspace))
        {
            return Failure("工作目录不存在。", workspace);
        }

        if (request.ModelConfigMode != "MimoLocalConfig")
        {
            var providerName = NormalizeProviderName(request.ProviderName);
            if (providerName != "local" && string.IsNullOrWhiteSpace(request.ApiKey))
            {
                return Failure(
                    "App Provider 模式缺少 API Key。",
                    "请选择使用 Mimo Code 本机配置，或在模型页填写用户自己的 API Key。");
            }

            if (string.IsNullOrWhiteSpace(request.ModelName))
            {
                return Failure("App Provider 模式缺少模型名。", "请在模型页填写模型名。");
            }
        }

        var versionCheck = await TryReadMimoVersionAsync(resolvedExecutable, workspace, ct);
        if (!versionCheck.Success)
        {
            return Failure(versionCheck.Message, versionCheck.Detail);
        }

        return Success(
            request.IsEnabled ? "Mimo Code 检测通过，当前已启用。" : "Mimo Code 检测通过，当前未启用。",
            $"{resolvedExecutable}{Environment.NewLine}{versionCheck.Detail}");
    }

    private static async Task<SettingsDiagnosticsResult> TryReadMimoVersionAsync(
        string executablePath,
        string workspace,
        CancellationToken ct)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(MimoVersionTimeout);

        try
        {
            using var process = new Process
            {
                StartInfo = BuildVersionProcessStartInfo(executablePath, workspace)
            };

            process.Start();
            var outputTask = process.StandardOutput.ReadToEndAsync(timeoutCts.Token);
            var errorTask = process.StandardError.ReadToEndAsync(timeoutCts.Token);
            await process.WaitForExitAsync(timeoutCts.Token);

            var output = await outputTask;
            var error = await errorTask;
            var detail = Truncate(string.IsNullOrWhiteSpace(output) ? error : output, 240);

            if (process.ExitCode == 0)
            {
                return Success("Mimo Code 可启动。", string.IsNullOrWhiteSpace(detail) ? "版本命令无输出。" : detail);
            }

            return Success(
                "Mimo Code 可启动，但版本命令未返回 0。",
                string.IsNullOrWhiteSpace(detail) ? $"ExitCode={process.ExitCode}" : $"ExitCode={process.ExitCode}: {detail}");
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return Failure("Mimo Code 版本检测超时。", $"超过 {MimoVersionTimeout.TotalSeconds:0} 秒未返回。");
        }
        catch (Exception ex)
        {
            return Failure("Mimo Code 启动失败。", ex.Message);
        }
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

    private static ProcessStartInfo BuildVersionProcessStartInfo(string executablePath, string workspace)
    {
        var extension = Path.GetExtension(executablePath).ToLowerInvariant();
        var startInfo = new ProcessStartInfo
        {
            WorkingDirectory = workspace,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        if (extension is ".cmd" or ".bat")
        {
            startInfo.FileName = Environment.GetEnvironmentVariable("ComSpec") ?? "cmd.exe";
            startInfo.Arguments = $"/d /s /c \"\"{executablePath}\" --version\"";
            return startInfo;
        }

        startInfo.FileName = executablePath;
        startInfo.Arguments = "--version";
        return startInfo;
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

    private static Uri BuildChatCompletionsUri(Uri baseUri)
    {
        var baseText = baseUri.ToString().TrimEnd('/');
        if (baseText.EndsWith("/chat/completions", StringComparison.OrdinalIgnoreCase))
        {
            return new Uri(baseText);
        }

        return new Uri($"{baseText}/chat/completions");
    }

    private static string ExtractAssistantPreview(string payload)
    {
        try
        {
            using var document = JsonDocument.Parse(payload);
            var root = document.RootElement;
            var content = root
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString();

            return string.IsNullOrWhiteSpace(content)
                ? "Provider 返回成功，但内容为空。"
                : Truncate(content, 180);
        }
        catch
        {
            return "Provider 返回成功。";
        }
    }

    private static string NormalizeProviderName(string providerName)
    {
        return string.IsNullOrWhiteSpace(providerName)
            ? "openai"
            : providerName.Trim().ToLowerInvariant();
    }

    private static string NormalizeBaseUrl(string endpoint)
    {
        return endpoint.Trim().TrimEnd('/');
    }

    private static SettingsDiagnosticsResult Success(string message, string detail)
    {
        return new SettingsDiagnosticsResult(true, message, detail);
    }

    private static SettingsDiagnosticsResult Failure(string message, string detail)
    {
        return new SettingsDiagnosticsResult(false, message, detail);
    }

    private static string Truncate(string value, int maxLength)
    {
        var text = value.Trim();
        return text.Length <= maxLength ? text : $"{text[..maxLength]}...";
    }
}
