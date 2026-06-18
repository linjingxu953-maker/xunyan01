using DesktopMascot.Agent.Models;
using DesktopMascot.Core.Enums;
using DesktopMascot.Core.Interfaces;
using DesktopMascot.Core.Models;
using Microsoft.Extensions.Logging;

namespace DesktopMascot.Agent.Providers;

/// <summary>
/// 语音对话模式 - 语音输入→处理→语音输出
/// </summary>
public class VoiceConversationMode
{
    private readonly ISpeechRecognitionProvider _speechRecognition;
    private readonly ITextToSpeechProvider _tts;
    private readonly IAgentEngine _agent;
    private readonly ILogger<VoiceConversationMode> _logger;

    public VoiceConversationMode(
        ISpeechRecognitionProvider speechRecognition,
        ITextToSpeechProvider tts,
        IAgentEngine agent,
        ILogger<VoiceConversationMode> logger)
    {
        _speechRecognition = speechRecognition;
        _tts = tts;
        _agent = agent;
        _logger = logger;
    }

    /// <summary>
    /// 执行语音对话：识别→处理→回复
    /// </summary>
    public async Task<VoiceConversationResult> ProcessVoiceInputAsync(
        string audioFilePath,
        string? language = null,
        string voice = "zh-CN-XiaoxiaoNeural",
        CancellationToken ct = default)
    {
        var result = new VoiceConversationResult();

        // 1. 语音识别
        _logger.LogInformation("开始语音识别...");
        var recognitionResult = await _speechRecognition.RecognizeFromFileAsync(audioFilePath, language, ct);

        if (!recognitionResult.Success)
        {
            result.Success = false;
            result.Error = $"语音识别失败: {recognitionResult.Error}";
            return result;
        }

        result.RecognizedText = recognitionResult.Text;
        result.RecognitionLanguage = recognitionResult.Language;
        _logger.LogInformation($"识别结果: {recognitionResult.Text}");

        // 2. 处理文本（调用 Agent）
        _logger.LogInformation("处理识别结果...");
        var agentTask = new AgentTask
        {
            Title = "语音输入处理",
            Input = recognitionResult.Text,
            Type = TaskType.Chat
        };

        var agentResult = await _agent.ExecuteAsync(agentTask, ct);

        if (!agentResult.Success)
        {
            result.Success = false;
            result.Error = $"处理失败: {agentResult.Error}";
            return result;
        }

        result.ResponseText = agentResult.Content;
        _logger.LogInformation($"处理结果: {agentResult.Content}");

        // 3. 语音合成
        _logger.LogInformation("生成语音回复...");
        var ttsResult = await _tts.SynthesizeAsync(agentResult.Content, voice, 1.0f, ct);

        if (!ttsResult.Success)
        {
            // 即使 TTS 失败，文本结果仍然有效
            result.Success = true;
            result.ResponseText = agentResult.Content;
            result.TtsError = ttsResult.Error;
            return result;
        }

        result.Success = true;
        result.AudioFilePath = ttsResult.AudioFilePath;
        result.AudioData = ttsResult.AudioData;

        // 自动播放语音回复
        PlayAudioFile(ttsResult.AudioFilePath);

        return result;
    }

    private static void PlayAudioFile(string? filePath)
    {
        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath)) return;

        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = filePath,
                UseShellExecute = true,
                CreateNoWindow = true
            });
        }
        catch { }
    }
}

/// <summary>
/// 语音对话结果
/// </summary>
public class VoiceConversationResult
{
    public bool Success { get; set; }
    public string RecognizedText { get; set; } = string.Empty;
    public string RecognitionLanguage { get; set; } = string.Empty;
    public string ResponseText { get; set; } = string.Empty;
    public string? AudioFilePath { get; set; }
    public byte[]? AudioData { get; set; }
    public string? Error { get; set; }
    public string? TtsError { get; set; }
}
