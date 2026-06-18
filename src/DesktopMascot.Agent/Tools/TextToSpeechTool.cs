using DesktopMascot.Core.Tools;
using System.Diagnostics;
using System.Text.Json;
using DesktopMascot.Agent.Models;
using DesktopMascot.Agent.Providers;

namespace DesktopMascot.Agent.Tools;

/// <summary>
/// 文本转语音工具
/// </summary>
public class TextToSpeechTool : ITool
{
    private readonly ITextToSpeechProvider _provider;

    public TextToSpeechTool(ITextToSpeechProvider provider)
    {
        _provider = provider;
    }

    public string Name => "text_to_speech";
    public string Description => "将文本转换为语音。支持多种语音风格和语速调节。";
    public string ParametersSchema => """
    {
        "type": "object",
        "properties": {
            "text": {
                "type": "string",
                "description": "要转换的文本"
            },
            "voice": {
                "type": "string",
                "description": "语音风格（alloy/echo/fable/onyx/nova/shimmer）",
                "enum": ["alloy", "echo", "fable", "onyx", "nova", "shimmer"]
            },
            "speed": {
                "type": "number",
                "description": "语速（0.25-4.0，默认1.0）"
            },
            "output_path": {
                "type": "string",
                "description": "输出文件路径（可选，默认自动生成）"
            }
        },
        "required": ["text"]
    }
    """;

    public async Task<ToolResult> ExecuteAsync(string arguments, CancellationToken ct = default)
    {
        try
        {
            var doc = JsonDocument.Parse(arguments);
            var root = doc.RootElement;

            if (!root.TryGetProperty("text", out var textElement))
            {
                return new ToolResult
                {
                    Name = Name,
                    Success = false,
                    Error = "缺少 text 参数"
                };
            }

            var text = textElement.GetString() ?? "";
            if (string.IsNullOrWhiteSpace(text))
            {
                return new ToolResult
                {
                    Name = Name,
                    Success = false,
                    Error = "文本不能为空"
                };
            }

            var voice = root.TryGetProperty("voice", out var voiceElement) ? voiceElement.GetString() ?? "alloy" : "alloy";
            var speed = root.TryGetProperty("speed", out var speedElement) ? (float)speedElement.GetDouble() : 1.0f;
            var outputPath = root.TryGetProperty("output_path", out var pathElement) ? pathElement.GetString() : null;

            speed = Math.Clamp(speed, 0.25f, 4.0f);

            TextToSpeechResult result;

            if (!string.IsNullOrEmpty(outputPath))
            {
                result = await _provider.SynthesizeToFileAsync(text, outputPath, voice, speed, ct);
            }
            else
            {
                var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
                var defaultPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "DesktopMascot", "tts", $"tts_{timestamp}.mp3");
                result = await _provider.SynthesizeToFileAsync(text, defaultPath, voice, speed, ct);
            }

            if (!result.Success)
            {
                return new ToolResult
                {
                    Name = Name,
                    Success = false,
                    Error = result.Error
                };
            }

            // 自动播放生成的音频
            PlayAudioFile(result.AudioFilePath);

            return new ToolResult
            {
                Name = Name,
                Success = true,
                Content = $"语音已生成：{result.AudioFilePath}\n语音：{result.Voice}\n语速：{result.Speed}x\n文本长度：{text.Length}字"
            };
        }
        catch (Exception ex)
        {
            return new ToolResult
            {
                Name = Name,
                Success = false,
                Error = $"文本转语音失败: {ex.Message}"
            };
        }
    }

    private static void PlayAudioFile(string? filePath)
    {
        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath)) return;

        try
        {
            // 使用 Process.Start 打开音频文件，系统默认播放器播放
            Process.Start(new ProcessStartInfo
            {
                FileName = filePath,
                UseShellExecute = true,
                CreateNoWindow = true
            });
        }
        catch
        {
            // 播放失败不影响返回结果
        }
    }
}
