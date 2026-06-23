namespace DesktopMascot.UI.Services;

public interface IAudioPlaybackService
{
    bool IsPlaying { get; }
    AudioPlaybackResult Play(string filePath);
    AudioPlaybackResult Stop();
}
