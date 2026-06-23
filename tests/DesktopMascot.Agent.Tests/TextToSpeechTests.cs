using DesktopMascot.Agent.Models;
using DesktopMascot.Agent.Providers;
using DesktopMascot.Agent.Tools;
using Moq;

namespace DesktopMascot.Agent.Tests;

public class TextToSpeechTests
{
    [Fact]
    public void TtsResult_ShouldContainRequiredFields()
    {
        var result = new TextToSpeechResult
        {
            Success = true,
            AudioFilePath = "/tmp/test.mp3",
            Voice = "alloy",
            Speed = 1.0f,
            Duration = TimeSpan.FromSeconds(3.5)
        };

        Assert.True(result.Success);
        Assert.Equal("/tmp/test.mp3", result.AudioFilePath);
        Assert.Equal("alloy", result.Voice);
    }

    [Fact]
    public async Task TextToSpeechTool_MissingText_ShouldReturnError()
    {
        var mockProvider = new Mock<ITextToSpeechProvider>();
        var tool = new TextToSpeechTool(mockProvider.Object);

        var result = await tool.ExecuteAsync("""{"voice": "alloy"}""");

        Assert.False(result.Success);
        Assert.Contains("缺少 text", result.Error);
    }

    [Fact]
    public async Task TextToSpeechTool_EmptyText_ShouldReturnError()
    {
        var mockProvider = new Mock<ITextToSpeechProvider>();
        var tool = new TextToSpeechTool(mockProvider.Object);

        var result = await tool.ExecuteAsync("""{"text": ""}""");

        Assert.False(result.Success);
        Assert.Contains("不能为空", result.Error);
    }

    [Fact]
    public async Task TextToSpeechTool_Success_ShouldReturnFilePath()
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
        var result = await tool.ExecuteAsync("""{"text": "你好世界"}""");

        Assert.True(result.Success);
        Assert.Contains("语音已生成", result.Content);
        Assert.Contains("alloy", result.Content);
    }

    [Fact]
    public async Task TextToSpeechTool_WithCustomVoice_ShouldPassVoice()
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
                Voice = "nova",
                Speed = 1.5f
            });

        var tool = new TextToSpeechTool(mockProvider.Object);
        var result = await tool.ExecuteAsync("""{"text": "测试", "voice": "nova", "speed": 1.5}""");

        Assert.True(result.Success);
        Assert.Contains("nova", result.Content);
        Assert.Contains("1.5", result.Content);
    }

    [Fact]
    public async Task TextToSpeechTool_SpeedClamping_ShouldWork()
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
        // Speed should be clamped to 4.0
        var result = await tool.ExecuteAsync("""{"text": "测试", "speed": 10.0}""");

        Assert.True(result.Success);
        mockProvider.Verify(x => x.SynthesizeToFileAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.Is<float>(s => s <= 4.0f),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void TextToSpeechTool_Metadata_ShouldBeCorrect()
    {
        var mockProvider = new Mock<ITextToSpeechProvider>();
        var tool = new TextToSpeechTool(mockProvider.Object);

        Assert.Equal("text_to_speech", tool.Name);
        Assert.Contains("text", tool.ParametersSchema);
        Assert.Contains("voice", tool.ParametersSchema);
        Assert.Contains("speed", tool.ParametersSchema);
    }

    [Fact]
    public void TextToSpeechTool_ShouldNotLaunchExternalPlayer()
    {
        var sourcePath = FindTextToSpeechToolSourcePath();
        var source = File.ReadAllText(sourcePath);

        Assert.DoesNotContain("Process.Start", source);
        Assert.DoesNotContain("tts_play_", source);
    }

    [Fact]
    public void TtsAudioFileValidator_RejectsTinyMp3File()
    {
        var path = Path.Combine(Path.GetTempPath(), $"xunyan_tiny_{Guid.NewGuid():N}.mp3");
        try
        {
            File.WriteAllBytes(path, [0x49, 0x44, 0x33]);

            var result = TtsAudioFileValidator.Validate(path);

            Assert.False(result.IsValid);
            Assert.Contains("音频文件异常", result.Error);
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    [Fact]
    public void TtsAudioFileValidator_AcceptsMp3FrameHeader()
    {
        var path = Path.Combine(Path.GetTempPath(), $"xunyan_mp3_{Guid.NewGuid():N}.mp3");
        try
        {
            var bytes = new byte[1024];
            bytes[0] = 0xFF;
            bytes[1] = 0xFB;
            File.WriteAllBytes(path, bytes);

            var result = TtsAudioFileValidator.Validate(path);

            Assert.True(result.IsValid);
            Assert.Null(result.Error);
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    private static string FindTextToSpeechToolSourcePath()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(
                directory.FullName,
                "src",
                "DesktopMascot.Agent",
                "Tools",
                "TextToSpeechTool.cs");
            if (File.Exists(candidate))
                return candidate;

            directory = directory.Parent;
        }

        throw new FileNotFoundException("无法定位 TextToSpeechTool.cs");
    }
}
