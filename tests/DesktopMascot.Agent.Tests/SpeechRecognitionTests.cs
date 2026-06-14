using DesktopMascot.Agent.Models;
using DesktopMascot.Agent.Providers;
using DesktopMascot.Agent.Tools;
using Moq;

namespace DesktopMascot.Agent.Tests;

public class SpeechRecognitionTests
{
    [Fact]
    public void SpeechRecognitionResult_ShouldContainRequiredFields()
    {
        var result = new SpeechRecognitionResult
        {
            Success = true,
            Text = "你好世界",
            Language = "zh",
            Confidence = 0.95f,
            Duration = TimeSpan.FromSeconds(2.5)
        };

        Assert.True(result.Success);
        Assert.Equal("你好世界", result.Text);
        Assert.Equal("zh", result.Language);
        Assert.Equal(0.95f, result.Confidence);
    }

    [Fact]
    public void SpeechSegment_ShouldContainTimeRange()
    {
        var segment = new SpeechSegment
        {
            Text = "第一段",
            StartSeconds = 0.0f,
            EndSeconds = 2.5f,
            Confidence = 0.9f
        };

        Assert.Equal(0.0f, segment.StartSeconds);
        Assert.Equal(2.5f, segment.EndSeconds);
    }

    [Fact]
    public async Task SpeechRecognitionTool_MissingFilePath_ShouldReturnError()
    {
        var mockProvider = new Mock<ISpeechRecognitionProvider>();
        var tool = new SpeechRecognitionTool(mockProvider.Object);

        var result = await tool.ExecuteAsync("""{"language": "zh"}""");

        Assert.False(result.Success);
        Assert.Contains("缺少 file_path", result.Error);
    }

    [Fact]
    public async Task SpeechRecognitionTool_EmptyFilePath_ShouldReturnError()
    {
        var mockProvider = new Mock<ISpeechRecognitionProvider>();
        var tool = new SpeechRecognitionTool(mockProvider.Object);

        var result = await tool.ExecuteAsync("""{"file_path": ""}""");

        Assert.False(result.Success);
        Assert.Contains("不能为空", result.Error);
    }

    [Fact]
    public async Task SpeechRecognitionTool_NonexistentFile_ShouldReturnError()
    {
        var mockProvider = new Mock<ISpeechRecognitionProvider>();
        mockProvider.Setup(x => x.RecognizeFromFileAsync(
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SpeechRecognitionResult
            {
                Success = false,
                Error = "文件不存在"
            });

        var tool = new SpeechRecognitionTool(mockProvider.Object);
        var result = await tool.ExecuteAsync("""{"file_path": "nonexistent.wav"}""");

        Assert.False(result.Success);
    }

    [Fact]
    public async Task SpeechRecognitionTool_Success_ShouldReturnText()
    {
        var mockProvider = new Mock<ISpeechRecognitionProvider>();
        mockProvider.Setup(x => x.RecognizeFromFileAsync(
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SpeechRecognitionResult
            {
                Success = true,
                Text = "你好世界",
                Language = "zh",
                Duration = TimeSpan.FromSeconds(1.5)
            });

        var tool = new SpeechRecognitionTool(mockProvider.Object);
        var result = await tool.ExecuteAsync("""{"file_path": "test.wav"}""");

        Assert.True(result.Success);
        Assert.Contains("你好世界", result.Content);
        Assert.Contains("zh", result.Content);
    }

    [Fact]
    public void SpeechRecognitionTool_Metadata_ShouldBeCorrect()
    {
        var mockProvider = new Mock<ISpeechRecognitionProvider>();
        var tool = new SpeechRecognitionTool(mockProvider.Object);

        Assert.Equal("speech_recognition", tool.Name);
        Assert.Contains("file_path", tool.ParametersSchema);
        Assert.Contains("language", tool.ParametersSchema);
    }
}
