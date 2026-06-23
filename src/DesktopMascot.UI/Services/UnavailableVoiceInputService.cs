namespace DesktopMascot.UI.Services;

public sealed class UnavailableVoiceInputService : IVoiceInputService
{
    public bool IsRecording => false;

    public VoiceInputStartResult StartRecording(string language) =>
        new(false, "Voice input service is not connected.", "语音输入服务尚未接入。");

    public Task<VoiceInputRecognitionResult> StopAndRecognizeAsync(string language, CancellationToken ct = default) =>
        Task.FromResult(new VoiceInputRecognitionResult(
            false,
            string.Empty,
            null,
            "Voice input service is not connected.",
            "语音输入服务尚未接入。"));

    public VoiceInputStartResult Cancel() =>
        new(true, "Voice input is idle.");
}
