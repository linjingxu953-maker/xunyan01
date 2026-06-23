namespace DesktopMascot.UI.Services;

public interface IVoiceInputService
{
    bool IsRecording { get; }
    VoiceInputStartResult StartRecording(string language);
    Task<VoiceInputRecognitionResult> StopAndRecognizeAsync(string language, CancellationToken ct = default);
    VoiceInputStartResult Cancel();
}
