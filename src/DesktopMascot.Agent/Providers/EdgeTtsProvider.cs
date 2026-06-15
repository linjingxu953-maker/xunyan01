using System.Diagnostics;
using System.Text;
using System.Text.Json;
using DesktopMascot.Agent.Models;

namespace DesktopMascot.Agent.Providers;

/// <summary>
/// Edge TTS 提供者（免费，不需要 API Key）
/// 使用微软 Edge 浏览器的内置 TTS 引擎
/// </summary>
public class EdgeTtsProvider : ITextToSpeechProvider
{
    private readonly string _outputDirectory;

    public EdgeTtsProvider(string? outputDirectory = null)
    {
        _outputDirectory = outputDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "DesktopMascot", "tts");
        Directory.CreateDirectory(_outputDirectory);
    }

    public async Task<TextToSpeechResult> SynthesizeAsync(string text, string voice = "zh-CN-XiaoxiaoNeural", float speed = 1.0f, CancellationToken ct = default)
    {
        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
        var outputPath = Path.Combine(_outputDirectory, $"tts_{timestamp}.mp3");

        return await SynthesizeToFileAsync(text, outputPath, voice, speed, ct);
    }

    public async Task<TextToSpeechResult> SynthesizeToFileAsync(string text, string outputPath, string voice = "zh-CN-XiaoxiaoNeural", float speed = 1.0f, CancellationToken ct = default)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            var dir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            // 使用 edge-tts 命令行工具
            var arguments = $"--voice \"{voice}\" --rate \"{(int)((speed - 1.0) * 100)}%\" --text \"{EscapeText(text)}\" --write-media \"{outputPath}\"";

            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "edge-tts",
                    Arguments = arguments,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            await process.WaitForExitAsync(ct);

            stopwatch.Stop();

            if (process.ExitCode != 0)
            {
                var error = await process.StandardError.ReadToEndAsync();
                return new TextToSpeechResult
                {
                    Success = false,
                    Error = $"Edge TTS 失败: {error}",
                    Duration = stopwatch.Elapsed
                };
            }

            if (!File.Exists(outputPath))
            {
                return new TextToSpeechResult
                {
                    Success = false,
                    Error = "音频文件未生成",
                    Duration = stopwatch.Elapsed
                };
            }

            return new TextToSpeechResult
            {
                Success = true,
                AudioFilePath = outputPath,
                Voice = voice,
                Speed = speed,
                Duration = stopwatch.Elapsed
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return new TextToSpeechResult
            {
                Success = false,
                Error = $"Edge TTS 失败: {ex.Message}",
                Duration = stopwatch.Elapsed
            };
        }
    }

    public List<string> GetAvailableVoices()
    {
        return new List<string>
        {
            // 中文
            "zh-CN-XiaoxiaoNeural",
            "zh-CN-YunxiNeural",
            "zh-CN-YunjianNeural",
            "zh-CN-XiaoyiNeural",
            "zh-CN-YunyangNeural",
            // 英文
            "en-US-JennyNeural",
            "en-US-GuyNeural",
            "en-US-AriaNeural",
            "en-US-DavisNeural",
            // 日文
            "ja-JP-NanamiNeural",
            "ja-JP-KeitaNeural"
        };
    }

    /// <summary>
    /// 异步获取所有可用语音列表（从 edge-tts 命令）
    /// </summary>
    public async Task<List<string>> FetchAvailableVoicesAsync(CancellationToken ct = default)
    {
        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "edge-tts",
                    Arguments = "--list-voices",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            var output = await process.StandardOutput.ReadToEndAsync(ct);
            await process.WaitForExitAsync(ct);

            var voices = new List<string>();
            foreach (var line in output.Split('\n'))
            {
                if (line.Contains("Name:"))
                {
                    var name = line.Split(':').Last().Trim();
                    if (!string.IsNullOrEmpty(name))
                        voices.Add(name);
                }
            }

            return voices;
        }
        catch
        {
            return GetAvailableVoices();
        }
    }

    private static string EscapeText(string text)
    {
        return text
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\n", " ")
            .Replace("\r", "");
    }
}
