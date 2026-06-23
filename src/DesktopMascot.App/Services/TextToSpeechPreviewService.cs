using DesktopMascot.Agent.Providers;
using DesktopMascot.UI.Services;

namespace DesktopMascot.App.Services;

public sealed class TextToSpeechPreviewService : ITextToSpeechPreviewService
{
    private readonly ITextToSpeechProvider _provider;
    private readonly string _outputDirectory;

    public TextToSpeechPreviewService(ITextToSpeechProvider provider)
    {
        _provider = provider;
        _outputDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "DesktopMascot",
            "tts");
        Directory.CreateDirectory(_outputDirectory);
    }

    public async Task<TextToSpeechPreviewResult> SynthesizePreviewAsync(
        string text,
        string voice,
        CancellationToken ct = default)
    {
        var safeText = string.IsNullOrWhiteSpace(text)
            ? "你好，我是寻研01。"
            : text.Trim();
        var mappedVoice = MapVoice(voice);
        var outputPath = Path.Combine(_outputDirectory, $"preview_{DateTime.UtcNow:yyyyMMdd_HHmmss_fff}.mp3");

        var result = await _provider.SynthesizeToFileAsync(safeText, outputPath, mappedVoice, 1.0f, ct);
        if (!result.Success || string.IsNullOrWhiteSpace(result.AudioFilePath))
        {
            var error = string.IsNullOrWhiteSpace(result.Error) ? "TTS 预览生成失败。" : result.Error;
            return new TextToSpeechPreviewResult(false, null, error, error);
        }

        var validation = TtsAudioFileValidator.Validate(result.AudioFilePath);
        if (!validation.IsValid)
        {
            var error = validation.Error ?? "TTS 预览音频异常，无法播放。";
            return new TextToSpeechPreviewResult(false, result.AudioFilePath, error, error);
        }

        return new TextToSpeechPreviewResult(true, result.AudioFilePath, $"已生成试听音频：{Path.GetFileName(result.AudioFilePath)}");
    }

    private static string MapVoice(string voice) => voice switch
    {
        "温柔女声" => "zh-CN-XiaoxiaoNeural",
        "清晰男声" => "zh-CN-YunyangNeural",
        "沉稳旁白" => "zh-CN-YunjianNeural",
        "活泼少女" => "zh-CN-XiaoyiNeural",
        _ => "zh-CN-XiaoxiaoNeural"
    };
}
