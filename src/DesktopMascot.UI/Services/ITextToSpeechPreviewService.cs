namespace DesktopMascot.UI.Services;

public interface ITextToSpeechPreviewService
{
    Task<TextToSpeechPreviewResult> SynthesizePreviewAsync(
        string text,
        string voice,
        CancellationToken ct = default);
}
