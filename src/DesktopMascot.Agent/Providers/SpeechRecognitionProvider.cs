using System.Text.Json;
using DesktopMascot.Agent.Models;
using DesktopMascot.Core.Tools;

namespace DesktopMascot.Agent.Providers;

/// <summary>
/// 语音识别提供者接口
/// </summary>
public interface ISpeechRecognitionProvider
{
    /// <summary>
    /// 从音频文件识别语音
    /// </summary>
    Task<SpeechRecognitionResult> RecognizeFromFileAsync(string filePath, string? language = null, CancellationToken ct = default);

    /// <summary>
    /// 从音频流识别语音
    /// </summary>
    Task<SpeechRecognitionResult> RecognizeFromStreamAsync(Stream audioStream, string? language = null, CancellationToken ct = default);

    /// <summary>
    /// 从字节数组识别语音
    /// </summary>
    Task<SpeechRecognitionResult> RecognizeFromBytesAsync(byte[] audioData, string? language = null, CancellationToken ct = default);
}

/// <summary>
/// OpenAI Whisper 语音识别提供者
/// </summary>
public class WhisperSpeechProvider : ISpeechRecognitionProvider
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _baseUrl;

    public WhisperSpeechProvider(string apiKey, string? baseUrl = null)
    {
        _apiKey = apiKey;
        _baseUrl = baseUrl ?? "https://api.openai.com/v1";
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
    }

    public async Task<SpeechRecognitionResult> RecognizeFromFileAsync(string filePath, string? language = null, CancellationToken ct = default)
    {
        if (!File.Exists(filePath))
        {
            return new SpeechRecognitionResult
            {
                Success = false,
                Error = $"文件不存在: {filePath}"
            };
        }

        var audioData = await File.ReadAllBytesAsync(filePath, ct);
        return await RecognizeFromBytesAsync(audioData, language, ct);
    }

    public async Task<SpeechRecognitionResult> RecognizeFromStreamAsync(Stream audioStream, string? language = null, CancellationToken ct = default)
    {
        using var memoryStream = new MemoryStream();
        await audioStream.CopyToAsync(memoryStream, ct);
        var audioData = memoryStream.ToArray();
        return await RecognizeFromBytesAsync(audioData, language, ct);
    }

    public async Task<SpeechRecognitionResult> RecognizeFromBytesAsync(byte[] audioData, string? language = null, CancellationToken ct = default)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            using var content = new MultipartFormDataContent();
            using var audioContent = new ByteArrayContent(audioData);
            audioContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("audio/wav");
            content.Add(audioContent, "file", "audio.wav");

            if (!string.IsNullOrEmpty(language))
            {
                content.Add(new StringContent(language), "language");
            }

            content.Add(new StringContent("whisper-1"), "model");
            content.Add(new StringContent("json"), "response_format");

            var response = await _httpClient.PostAsync($"{_baseUrl}/audio/transcriptions", content, ct);
            var responseJson = await response.Content.ReadAsStringAsync(ct);

            stopwatch.Stop();

            if (!response.IsSuccessStatusCode)
            {
                return new SpeechRecognitionResult
                {
                    Success = false,
                    Error = $"API 错误: {response.StatusCode} - {responseJson}",
                    Duration = stopwatch.Elapsed
                };
            }

            var doc = JsonDocument.Parse(responseJson);
            var root = doc.RootElement;

            var result = new SpeechRecognitionResult
            {
                Success = true,
                Text = root.TryGetProperty("text", out var text) ? text.GetString() ?? "" : "",
                Language = language ?? "auto",
                Duration = stopwatch.Elapsed
            };

            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return new SpeechRecognitionResult
            {
                Success = false,
                Error = $"语音识别失败: {ex.Message}",
                Duration = stopwatch.Elapsed
            };
        }
    }
}

/// <summary>
/// 语音识别工具 - 集成到 Agent 工具系统
/// </summary>
public class SpeechRecognitionTool : ITool
{
    private readonly ISpeechRecognitionProvider _provider;

    public SpeechRecognitionTool(ISpeechRecognitionProvider provider)
    {
        _provider = provider;
    }

    public string Name => "speech_recognition";
    public string Description => "识别语音内容，支持音频文件或麦克风录音。返回识别的文本。";
    public string ParametersSchema => """
    {
        "type": "object",
        "properties": {
            "file_path": {
                "type": "string",
                "description": "音频文件路径（支持 wav, mp3, m4a 等）"
            },
            "language": {
                "type": "string",
                "description": "语言代码（可选，如 zh, en, ja）"
            }
        },
        "required": ["file_path"]
    }
    """;

    public async Task<ToolResult> ExecuteAsync(string arguments, CancellationToken ct = default)
    {
        try
        {
            var doc = JsonDocument.Parse(arguments);
            var root = doc.RootElement;

            if (!root.TryGetProperty("file_path", out var pathElement))
            {
                return new ToolResult
                {
                    Name = Name,
                    Success = false,
                    Error = "缺少 file_path 参数"
                };
            }

            var filePath = pathElement.GetString() ?? "";
            var language = root.TryGetProperty("language", out var langElement) ? langElement.GetString() : null;

            if (string.IsNullOrWhiteSpace(filePath))
            {
                return new ToolResult
                {
                    Name = Name,
                    Success = false,
                    Error = "文件路径不能为空"
                };
            }

            var result = await _provider.RecognizeFromFileAsync(filePath, language, ct);

            return new ToolResult
            {
                Name = Name,
                Success = result.Success,
                Content = result.Success ? $"识别结果：{result.Text}\n语言：{result.Language}\n耗时：{result.Duration.TotalSeconds:F1}秒" : null,
                Error = result.Error
            };
        }
        catch (Exception ex)
        {
            return new ToolResult
            {
                Name = Name,
                Success = false,
                Error = $"语音识别失败: {ex.Message}"
            };
        }
    }
}
