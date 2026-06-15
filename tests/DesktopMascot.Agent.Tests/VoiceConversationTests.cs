using DesktopMascot.Agent.Models;
using DesktopMascot.Agent.Providers;
using DesktopMascot.Core.Enums;
using DesktopMascot.Core.Interfaces;
using DesktopMascot.Core.Models;
using Microsoft.Extensions.Logging;
using Moq;

namespace DesktopMascot.Agent.Tests;

public class VoiceConversationTests
{
    [Fact]
    public async Task VoiceConversation_ShouldProcessAndReturnResult()
    {
        var mockSpeech = new Mock<ISpeechRecognitionProvider>();
        mockSpeech.Setup(x => x.RecognizeFromFileAsync(
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SpeechRecognitionResult
            {
                Success = true,
                Text = "你好",
                Language = "zh"
            });

        var mockTts = new Mock<ITextToSpeechProvider>();
        mockTts.Setup(x => x.SynthesizeAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<float>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TextToSpeechResult
            {
                Success = true,
                AudioFilePath = "/tmp/response.mp3",
                Voice = "zh-CN-XiaoxiaoNeural"
            });

        var mockAgent = new Mock<IAgentEngine>();
        mockAgent.Setup(x => x.ExecuteAsync(
                It.IsAny<AgentTask>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TaskResult
            {
                Success = true,
                Content = "你好！有什么可以帮助你的？"
            });

        var mockLogger = new Mock<ILogger<VoiceConversationMode>>();
        var mode = new VoiceConversationMode(
            mockSpeech.Object,
            mockTts.Object,
            mockAgent.Object,
            mockLogger.Object);

        var result = await mode.ProcessVoiceInputAsync("test.wav");

        Assert.True(result.Success);
        Assert.Equal("你好", result.RecognizedText);
        Assert.Contains("你好", result.ResponseText);
        Assert.Equal("/tmp/response.mp3", result.AudioFilePath);
    }

    [Fact]
    public async Task VoiceConversation_SpeechRecognitionFails_ShouldReturnError()
    {
        var mockSpeech = new Mock<ISpeechRecognitionProvider>();
        mockSpeech.Setup(x => x.RecognizeFromFileAsync(
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SpeechRecognitionResult
            {
                Success = false,
                Error = "文件不存在"
            });

        var mockTts = new Mock<ITextToSpeechProvider>();
        var mockAgent = new Mock<IAgentEngine>();

        var mockLogger3 = new Mock<ILogger<VoiceConversationMode>>();
        var mode = new VoiceConversationMode(
            mockSpeech.Object,
            mockTts.Object,
            mockAgent.Object,
            mockLogger3.Object);

        var result = await mode.ProcessVoiceInputAsync("test.wav");

        Assert.False(result.Success);
        Assert.Contains("语音识别失败", result.Error);
    }

    [Fact]
    public async Task VoiceConversation_AgentFails_ShouldReturnError()
    {
        var mockSpeech = new Mock<ISpeechRecognitionProvider>();
        mockSpeech.Setup(x => x.RecognizeFromFileAsync(
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SpeechRecognitionResult
            {
                Success = true,
                Text = "测试"
            });

        var mockTts = new Mock<ITextToSpeechProvider>();
        var mockAgent = new Mock<IAgentEngine>();
        mockAgent.Setup(x => x.ExecuteAsync(
                It.IsAny<AgentTask>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TaskResult
            {
                Success = false,
                Error = "处理失败"
            });

        var mockLogger = new Mock<ILogger<VoiceConversationMode>>();
        var mode = new VoiceConversationMode(
            mockSpeech.Object,
            mockTts.Object,
            mockAgent.Object,
            mockLogger.Object);

        var result = await mode.ProcessVoiceInputAsync("test.wav");

        Assert.False(result.Success);
        Assert.Contains("处理失败", result.Error);
    }

    [Fact]
    public async Task VoiceConversation_TtsFails_ShouldStillReturnText()
    {
        var mockSpeech = new Mock<ISpeechRecognitionProvider>();
        mockSpeech.Setup(x => x.RecognizeFromFileAsync(
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SpeechRecognitionResult
            {
                Success = true,
                Text = "测试"
            });

        var mockTts = new Mock<ITextToSpeechProvider>();
        mockTts.Setup(x => x.SynthesizeAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<float>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TextToSpeechResult
            {
                Success = false,
                Error = "TTS 失败"
            });

        var mockAgent = new Mock<IAgentEngine>();
        mockAgent.Setup(x => x.ExecuteAsync(
                It.IsAny<AgentTask>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TaskResult
            {
                Success = true,
                Content = "回复内容"
            });

        var mockLogger2 = new Mock<ILogger<VoiceConversationMode>>();
        var mode = new VoiceConversationMode(
            mockSpeech.Object,
            mockTts.Object,
            mockAgent.Object,
            mockLogger2.Object);

        var result = await mode.ProcessVoiceInputAsync("test.wav");

        Assert.True(result.Success);
        Assert.Equal("回复内容", result.ResponseText);
        Assert.Contains("TTS 失败", result.TtsError);
    }

    [Fact]
    public void VoiceConversationResult_ShouldContainAllFields()
    {
        var result = new VoiceConversationResult
        {
            Success = true,
            RecognizedText = "你好",
            RecognitionLanguage = "zh",
            ResponseText = "你好！",
            AudioFilePath = "/tmp/response.mp3"
        };

        Assert.True(result.Success);
        Assert.Equal("你好", result.RecognizedText);
        Assert.Equal("你好！", result.ResponseText);
    }
}
