using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using DesktopMascot.Agent.Models;

namespace DesktopMascot.Agent.Providers;

/// <summary>
/// 文本转语音接口
/// </summary>
public interface ITextToSpeechProvider
{
    /// <summary>
    /// 文本转语音（返回音频数据）
    /// </summary>
    Task<TextToSpeechResult> SynthesizeAsync(string text, string voice = "alloy", float speed = 1.0f, CancellationToken ct = default);

    /// <summary>
    /// 文本转语音并保存到文件
    /// </summary>
    Task<TextToSpeechResult> SynthesizeToFileAsync(string text, string outputPath, string voice = "alloy", float speed = 1.0f, CancellationToken ct = default);

    /// <summary>
    /// 获取可用语音列表
    /// </summary>
    List<string> GetAvailableVoices();
}

/// <summary>
/// OpenAI TTS 提供者
/// </summary>
public class OpenAiTtsProvider : ITextToSpeechProvider
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _baseUrl;
    private readonly string _outputDirectory;

    public OpenAiTtsProvider(string apiKey, string? baseUrl = null, string? outputDirectory = null)
    {
        _apiKey = apiKey;
        _baseUrl = baseUrl ?? "https://api.openai.com/v1";
        _outputDirectory = outputDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "DesktopMascot", "tts");
        Directory.CreateDirectory(_outputDirectory);
        _httpClient = new HttpClient();
    }

    public async Task<TextToSpeechResult> SynthesizeAsync(string text, string voice = "alloy", float speed = 1.0f, CancellationToken ct = default)
    {
        try
        {
            var audioData = await CallTtsApiAsync(text, voice, speed, ct);

            return new TextToSpeechResult
            {
                Success = true,
                AudioData = audioData,
                Voice = voice,
                Speed = speed
            };
        }
        catch (Exception ex)
        {
            return new TextToSpeechResult
            {
                Success = false,
                Error = $"TTS 失败: {ex.Message}"
            };
        }
    }

    public async Task<TextToSpeechResult> SynthesizeToFileAsync(string text, string outputPath, string voice = "alloy", float speed = 1.0f, CancellationToken ct = default)
    {
        try
        {
            var audioData = await CallTtsApiAsync(text, voice, speed, ct);

            var dir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            await File.WriteAllBytesAsync(outputPath, audioData, ct);

            return new TextToSpeechResult
            {
                Success = true,
                AudioFilePath = outputPath,
                AudioData = audioData,
                Voice = voice,
                Speed = speed
            };
        }
        catch (Exception ex)
        {
            return new TextToSpeechResult
            {
                Success = false,
                Error = $"TTS 失败: {ex.Message}"
            };
        }
    }

    public List<string> GetAvailableVoices()
    {
        return new List<string> { "alloy", "echo", "fable", "onyx", "nova", "shimmer" };
    }

    private async Task<byte[]> CallTtsApiAsync(string text, string voice, float speed, CancellationToken ct)
    {
        var requestBody = new
        {
            model = "tts-1",
            input = text,
            voice = voice,
            speed = speed,
            response_format = "mp3"
        };

        var json = System.Text.Json.JsonSerializer.Serialize(requestBody);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        _httpClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _apiKey);

        var response = await _httpClient.PostAsync($"{_baseUrl}/audio/speech", content, ct);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadAsByteArrayAsync(ct);
    }
}
