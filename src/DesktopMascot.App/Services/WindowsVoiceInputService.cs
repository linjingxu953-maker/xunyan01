using System.Runtime.InteropServices;
using System.Text;
using DesktopMascot.Agent.Providers;
using DesktopMascot.Core.Configuration;
using DesktopMascot.UI.Services;

namespace DesktopMascot.App.Services;

public sealed class WindowsVoiceInputService : IVoiceInputService
{
    private readonly IConfigurationManager _configurationManager;
    private readonly IApiKeyStore? _apiKeyStore;
    private readonly string _recordingDirectory;
    private string? _alias;
    private string? _recordingPath;

    public WindowsVoiceInputService(IConfigurationManager configurationManager, IApiKeyStore? apiKeyStore = null)
    {
        _configurationManager = configurationManager;
        _apiKeyStore = apiKeyStore;
        _recordingDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "DesktopMascot",
            "voice-input");
        Directory.CreateDirectory(_recordingDirectory);
    }

    public bool IsRecording => !string.IsNullOrWhiteSpace(_alias);

    public VoiceInputStartResult StartRecording(string language)
    {
        if (!OperatingSystem.IsWindows())
            return new VoiceInputStartResult(false, "当前平台不支持内置录音。", "当前平台不支持内置录音。");

        if (IsRecording)
            return new VoiceInputStartResult(false, "录音已经在进行中。", "录音已经在进行中。");

        var alias = $"xunyan_voice_{Guid.NewGuid():N}";
        var outputPath = Path.Combine(_recordingDirectory, $"voice_{DateTime.UtcNow:yyyyMMdd_HHmmss_fff}.wav");

        var openResult = Send($"open new type waveaudio alias {alias}");
        if (!openResult.Success)
            return new VoiceInputStartResult(false, openResult.Message, openResult.Error);

        Send($"set {alias} bitspersample 16 samplespersec 16000 channels 1");
        var recordResult = Send($"record {alias}");
        if (!recordResult.Success)
        {
            Send($"close {alias}");
            return new VoiceInputStartResult(false, recordResult.Message, recordResult.Error);
        }

        _alias = alias;
        _recordingPath = outputPath;
        return new VoiceInputStartResult(true, $"正在录音：{language}");
    }

    public async Task<VoiceInputRecognitionResult> StopAndRecognizeAsync(string language, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_alias) || string.IsNullOrWhiteSpace(_recordingPath))
        {
            return new VoiceInputRecognitionResult(false, string.Empty, null, "当前没有正在录制的语音。", "当前没有正在录制的语音。");
        }

        var alias = _alias;
        var recordingPath = _recordingPath;
        _alias = null;
        _recordingPath = null;

        var stopResult = Send($"stop {alias}");
        var saveResult = stopResult.Success ? Send($"save {alias} \"{recordingPath}\"") : stopResult;
        Send($"close {alias}");

        if (!saveResult.Success)
            return new VoiceInputRecognitionResult(false, string.Empty, recordingPath, saveResult.Message, saveResult.Error);

        if (!File.Exists(recordingPath) || new FileInfo(recordingPath).Length == 0)
        {
            return new VoiceInputRecognitionResult(false, string.Empty, recordingPath, "录音文件为空。", "录音文件为空，请检查麦克风权限。");
        }

        var settings = await _configurationManager.GetAppSettingsAsync(ct);
        var providerName = NormalizeProviderName(settings.ProviderName);
        var apiKey = _apiKeyStore != null
            ? await _apiKeyStore.GetApiKeyAsync(providerName, ct) ?? string.Empty
            : settings.ApiKey;

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return new VoiceInputRecognitionResult(
                false,
                string.Empty,
                recordingPath,
                "缺少语音识别 API Key。",
                "请先在设置中心配置 Provider/API Key 后再使用语音输入。");
        }

        var provider = new WhisperSpeechProvider(apiKey.Trim(), NormalizeBaseUrl(settings.ApiEndpoint));
        var result = await provider.RecognizeFromFileAsync(recordingPath, NormalizeLanguage(language), ct);
        if (!result.Success || string.IsNullOrWhiteSpace(result.Text))
        {
            var error = result.Error ?? "语音识别没有返回文本。";
            return new VoiceInputRecognitionResult(false, string.Empty, recordingPath, error, error);
        }

        return new VoiceInputRecognitionResult(true, result.Text.Trim(), recordingPath, "语音已识别。");
    }

    public VoiceInputStartResult Cancel()
    {
        if (string.IsNullOrWhiteSpace(_alias))
            return new VoiceInputStartResult(true, "当前没有正在录制的语音。");

        var alias = _alias;
        _alias = null;
        _recordingPath = null;
        Send($"stop {alias}");
        Send($"close {alias}");
        return new VoiceInputStartResult(true, "录音已取消。");
    }

    private static string NormalizeLanguage(string? language)
    {
        if (string.IsNullOrWhiteSpace(language))
            return "zh";

        var normalized = language.Trim();
        var separatorIndex = normalized.IndexOf('-');
        return separatorIndex > 0
            ? normalized[..separatorIndex].ToLowerInvariant()
            : normalized.ToLowerInvariant();
    }

    private static string NormalizeBaseUrl(string? baseUrl) =>
        string.IsNullOrWhiteSpace(baseUrl) ? "https://api.openai.com/v1" : baseUrl.Trim().TrimEnd('/');

    private static string NormalizeProviderName(string providerName)
    {
        if (string.IsNullOrWhiteSpace(providerName))
            return "openai";

        return providerName.Trim().ToLowerInvariant() switch
        {
            "moonshot" => "kimi",
            "glm" => "zhipu",
            "qwen" => "tongyi",
            "stepfun ai" => "stepfun",
            var value => value
        };
    }

    private static VoiceInputStartResult Send(string command)
    {
        var code = mciSendString(command, null, 0, IntPtr.Zero);
        if (code == 0)
            return new VoiceInputStartResult(true, string.Empty);

        var message = GetErrorMessage(code);
        return new VoiceInputStartResult(false, message, message);
    }

    private static string GetErrorMessage(int code)
    {
        var buffer = new StringBuilder(256);
        return mciGetErrorString(code, buffer, buffer.Capacity)
            ? buffer.ToString()
            : $"录音设备返回错误码 {code}";
    }

    [DllImport("winmm.dll", CharSet = CharSet.Unicode)]
    private static extern int mciSendString(
        string command,
        StringBuilder? returnString,
        int returnLength,
        IntPtr hwndCallback);

    [DllImport("winmm.dll", CharSet = CharSet.Unicode)]
    private static extern bool mciGetErrorString(int errorCode, StringBuilder errorText, int errorTextSize);
}
