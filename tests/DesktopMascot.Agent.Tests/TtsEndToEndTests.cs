using DesktopMascot.Agent.Models;
using DesktopMascot.Agent.Providers;
using DesktopMascot.Agent.Tools;
using Moq;

namespace DesktopMascot.Agent.Tests;

/// <summary>
/// 语音合成端到端测试
/// </summary>
public class TtsEndToEndTests
{
    [Fact]
    public async Task TtsTool_FullFlow_ShouldWork()
    {
        var mockProvider = new Mock<ITextToSpeechProvider>();
        mockProvider.Setup(x => x.SynthesizeToFileAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<float>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((string text, string path, string voice, float speed, CancellationToken ct) =>
            {
                // 模拟生成音频文件
                File.WriteAllText(path, "fake audio data");
                return new TextToSpeechResult
                {
                    Success = true,
                    AudioFilePath = path,
                    Voice = voice,
                    Speed = speed,
                    Duration = TimeSpan.FromSeconds(text.Length * 0.1)
                };
            });

        var tool = new TextToSpeechTool(mockProvider.Object);
        var tempFile = Path.Combine(Path.GetTempPath(), $"tts_test_{Guid.NewGuid():N}.mp3");

        try
        {
            var result = await tool.ExecuteAsync($$"""
                {
                    "text": "你好，这是一个测试。",
                    "voice": "nova",
                    "speed": 1.2,
                    "output_path": "{{tempFile.Replace("\\", "\\\\")}}"
                }
                """);

            Assert.True(result.Success);
            Assert.Contains("语音已生成", result.Content);
            Assert.Contains("nova", result.Content);
            Assert.Contains("1.2", result.Content);
            Assert.True(File.Exists(tempFile));
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task TtsTool_VoiceOptions_ShouldAllWork()
    {
        var voices = new[] { "alloy", "echo", "fable", "onyx", "nova", "shimmer" };

        foreach (var voice in voices)
        {
            var mockProvider = new Mock<ITextToSpeechProvider>();
            mockProvider.Setup(x => x.SynthesizeToFileAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<float>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new TextToSpeechResult
                {
                    Success = true,
                    AudioFilePath = $"/tmp/{voice}.mp3",
                    Voice = voice,
                    Speed = 1.0f
                });

            var tool = new TextToSpeechTool(mockProvider.Object);
            var result = await tool.ExecuteAsync($$"""{"text": "测试", "voice": "{{voice}}"}""");

            Assert.True(result.Success);
            Assert.Contains(voice, result.Content);
        }
    }

    [Fact]
    public async Task TtsTool_SpeedRange_ShouldClamp()
    {
        var mockProvider = new Mock<ITextToSpeechProvider>();
        mockProvider.Setup(x => x.SynthesizeToFileAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<float>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TextToSpeechResult
            {
                Success = true,
                AudioFilePath = "/tmp/test.mp3",
                Voice = "alloy",
                Speed = 1.0f
            });

        var tool = new TextToSpeechTool(mockProvider.Object);

        // Test speed too high - should clamp to 4.0
        var result1 = await tool.ExecuteAsync("""{"text": "测试", "speed": 10.0}""");
        Assert.True(result1.Success);
        mockProvider.Verify(x => x.SynthesizeToFileAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.Is<float>(s => s <= 4.0f && s >= 0.25f),
            It.IsAny<CancellationToken>()), Times.Once);

        // Test speed too low - should clamp to 0.25
        mockProvider.Invocations.Clear();
        var result2 = await tool.ExecuteAsync("""{"text": "测试", "speed": 0.1}""");
        Assert.True(result2.Success);
        mockProvider.Verify(x => x.SynthesizeToFileAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.Is<float>(s => s >= 0.25f && s <= 4.0f),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void TtsProvider_GetAvailableVoices_ShouldReturnSixVoices()
    {
        var provider = new OpenAiTtsProvider("test-key");
        var voices = provider.GetAvailableVoices();

        Assert.Equal(6, voices.Count);
        Assert.Contains("alloy", voices);
        Assert.Contains("nova", voices);
        Assert.Contains("shimmer", voices);
    }

    [Fact]
    public async Task TtsTool_DefaultOutputPath_ShouldGenerateTimestamp()
    {
        var mockProvider = new Mock<ITextToSpeechProvider>();
        mockProvider.Setup(x => x.SynthesizeToFileAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<float>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TextToSpeechResult
            {
                Success = true,
                AudioFilePath = "/tmp/tts_test.mp3",
                Voice = "alloy",
                Speed = 1.0f
            });

        var tool = new TextToSpeechTool(mockProvider.Object);
        var result = await tool.ExecuteAsync("""{"text": "测试"}""");

        Assert.True(result.Success);
        // Verify that a default path was generated
        mockProvider.Verify(x => x.SynthesizeToFileAsync(
            It.IsAny<string>(),
            It.Is<string>(p => p.Contains("tts_")),
            It.IsAny<string>(),
            It.IsAny<float>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SpeechAndTts_Combined_ShouldWork()
    {
        // Test that speech recognition and TTS can work together
        var mockSpeechProvider = new Mock<ISpeechRecognitionProvider>();
        mockSpeechProvider.Setup(x => x.RecognizeFromFileAsync(
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SpeechRecognitionResult
            {
                Success = true,
                Text = "这是语音识别的结果",
                Language = "zh"
            });

        var mockTtsProvider = new Mock<ITextToSpeechProvider>();
        mockTtsProvider.Setup(x => x.SynthesizeToFileAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<float>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TextToSpeechResult
            {
                Success = true,
                AudioFilePath = "/tmp/response.mp3",
                Voice = "nova",
                Speed = 1.0f
            });

        // Simulate: speech -> process -> TTS
        var speechTool = new SpeechRecognitionTool(mockSpeechProvider.Object);
        var ttsTool = new TextToSpeechTool(mockTtsProvider.Object);

        var speechResult = await speechTool.ExecuteAsync("""{"file_path": "input.wav"}""");
        Assert.True(speechResult.Success);

        var ttsResult = await ttsTool.ExecuteAsync("""{"text": "收到您的语音输入", "voice": "nova"}""");
        Assert.True(ttsResult.Success);
    }
}
