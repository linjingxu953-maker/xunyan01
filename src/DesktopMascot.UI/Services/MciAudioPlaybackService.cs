using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;

namespace DesktopMascot.UI.Services;

public sealed class MciAudioPlaybackService : IAudioPlaybackService
{
    private string? _alias;
    private object? _mediaPlayer;

    public bool IsPlaying { get; private set; }

    public AudioPlaybackResult Play(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            return new AudioPlaybackResult(false, "没有可播放的音频文件。", "没有可播放的音频文件。");

        if (!File.Exists(filePath))
            return new AudioPlaybackResult(false, $"音频文件不存在：{filePath}", $"音频文件不存在：{filePath}");

        if (!OperatingSystem.IsWindows())
            return new AudioPlaybackResult(false, "当前平台不支持内置音频播放。", "当前平台不支持内置音频播放。");

        Stop();

        var fullPath = Path.GetFullPath(filePath);
        if (!string.Equals(Path.GetExtension(fullPath), ".wav", StringComparison.OrdinalIgnoreCase))
        {
            var mediaPlayerResult = TryPlayWithWindowsMediaPlayer(fullPath);
            if (mediaPlayerResult.Success)
                return mediaPlayerResult;

            var fallbackMciResult = TryPlayWithMci(fullPath);
            if (fallbackMciResult.Success)
                return fallbackMciResult;

            return new AudioPlaybackResult(
                false,
                $"内置音频播放启动失败：{mediaPlayerResult.Error ?? fallbackMciResult.Error ?? mediaPlayerResult.Message}",
                mediaPlayerResult.Error ?? fallbackMciResult.Error ?? mediaPlayerResult.Message);
        }

        var mciResult = TryPlayWithMci(fullPath);
        if (mciResult.Success)
            return mciResult;

        return new AudioPlaybackResult(
            false,
            $"音频播放启动失败：{mciResult.Error ?? mciResult.Message}",
            mciResult.Error ?? mciResult.Message);
    }

    public AudioPlaybackResult Stop()
    {
        var stopped = StopWindowsMediaPlayer();
        stopped |= StopMciAlias();
        IsPlaying = false;

        return new AudioPlaybackResult(
            true,
            stopped ? "语音播放已停止。" : "当前没有正在播放的音频。");
    }

    private AudioPlaybackResult TryPlayWithMci(string filePath)
    {
        var alias = $"xunyan_audio_{Guid.NewGuid():N}";
        var openResult = Send(BuildOpenCommand(filePath, alias));
        if (!openResult.Success)
            return openResult;

        var playResult = Send($"play {alias} from 0");
        if (!playResult.Success)
        {
            Send($"close {alias}");
            return playResult;
        }

        _alias = alias;
        IsPlaying = true;
        return new AudioPlaybackResult(true, $"正在播放：{Path.GetFileName(filePath)}");
    }

    [SupportedOSPlatform("windows")]
    private AudioPlaybackResult TryPlayWithWindowsMediaPlayer(string filePath)
    {
        try
        {
            var playerType = Type.GetTypeFromProgID("WMPlayer.OCX");
            if (playerType is null)
                return new AudioPlaybackResult(false, "Windows Media Player COM 不可用。", "Windows Media Player COM 不可用。");

            var player = Activator.CreateInstance(playerType);
            if (player is null)
                return new AudioPlaybackResult(false, "无法创建内置音频播放器。", "无法创建内置音频播放器。");

            dynamic mediaPlayer = player;
            mediaPlayer.uiMode = "invisible";
            mediaPlayer.settings.autoStart = true;
            mediaPlayer.settings.volume = 100;
            mediaPlayer.URL = filePath;
            mediaPlayer.controls.play();

            _mediaPlayer = player;
            IsPlaying = true;
            return new AudioPlaybackResult(true, $"正在播放：{Path.GetFileName(filePath)}");
        }
        catch (Exception ex)
        {
            ReleaseWindowsMediaPlayer();
            IsPlaying = false;
            return new AudioPlaybackResult(false, $"内置音频播放器启动失败：{ex.Message}", ex.Message);
        }
    }

    private bool StopWindowsMediaPlayer()
    {
        if (_mediaPlayer is null)
            return false;

        try
        {
            dynamic player = _mediaPlayer;
            player.controls.stop();
            player.close();
        }
        catch
        {
        }
        finally
        {
            ReleaseWindowsMediaPlayer();
        }

        return true;
    }

    private void ReleaseWindowsMediaPlayer()
    {
        if (_mediaPlayer is not null && OperatingSystem.IsWindows() && Marshal.IsComObject(_mediaPlayer))
            Marshal.FinalReleaseComObject(_mediaPlayer);

        _mediaPlayer = null;
    }

    private bool StopMciAlias()
    {
        if (string.IsNullOrWhiteSpace(_alias))
            return false;

        var alias = _alias;
        Send($"stop {alias}");
        Send($"close {alias}");
        _alias = null;
        return true;
    }

    private static string BuildOpenCommand(string filePath, string alias)
    {
        var type = string.Equals(Path.GetExtension(filePath), ".wav", StringComparison.OrdinalIgnoreCase)
            ? "waveaudio"
            : "mpegvideo";
        return $"open \"{filePath}\" type {type} alias {alias}";
    }

    private static AudioPlaybackResult Send(string command)
    {
        var code = mciSendString(command, null, 0, IntPtr.Zero);
        if (code == 0)
            return new AudioPlaybackResult(true, string.Empty);

        var message = GetErrorMessage(code);
        return new AudioPlaybackResult(false, message, message);
    }

    private static string GetErrorMessage(int code)
    {
        var buffer = new StringBuilder(256);
        return mciGetErrorString(code, buffer, buffer.Capacity)
            ? buffer.ToString()
            : $"MCI 播放失败，错误码 {code}";
    }

    [DllImport("winmm.dll", CharSet = CharSet.Unicode)]
    private static extern int mciSendString(
        string command,
        StringBuilder? returnString,
        int returnLength,
        IntPtr hwndCallback);

    [DllImport("winmm.dll", CharSet = CharSet.Unicode)]
    private static extern bool mciGetErrorString(int errorCode, StringBuilder errorText, int errorTextSize);
}
