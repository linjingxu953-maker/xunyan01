namespace DesktopMascot.UI.Services;

public sealed class UnavailableTextToSpeechPreviewService : ITextToSpeechPreviewService
{
    public Task<TextToSpeechPreviewResult> SynthesizePreviewAsync(
        string text,
        string voice,
        CancellationToken ct = default)
    {
        return Task.FromResult(new TextToSpeechPreviewResult(
            false,
            null,
            "TTS 预览服务未接入。",
            "TTS 预览服务未接入。"));
    }
}
