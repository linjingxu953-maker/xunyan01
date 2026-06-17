using System.Text.Json;
using DesktopMascot.Agent.Tools;

namespace DesktopMascot.Agent.Tests;

public class VideoProcessingIntegrationTests : IDisposable
{
    private readonly string _tempDir;

    public VideoProcessingIntegrationTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"vid_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            try { Directory.Delete(_tempDir, true); } catch { }
    }

    /// <summary>检查 ffmpeg 是否可用</summary>
    private static bool IsFfmpegAvailable()
    {
        try
        {
            using var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "ffmpeg",
                Arguments = "-hide_banner -version",
                UseShellExecute = false,
                CreateNoWindow = true
            });
            if (process is null) return false;
            if (!process.WaitForExit(3000))
            {
                try { process.Kill(entireProcessTree: true); } catch { }
                return false;
            }

            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    [Fact]
    public async Task CreateTestVideo()
    {
        if (!IsFfmpegAvailable()) return; // 没有 ffmpeg 跳过，不卡测试

        var outputPath = Path.Combine(_tempDir, "test.mp4");
        await RunFfmpegForTestAsync(
            $"-hide_banner -loglevel error -y -f lavfi -i \"color=c=red:s=320x240:d=3,drawtext=text='Hello':fontcolor=white:fontsize=40:x=(w-text_w)/2:y=(h-text_h)/2\" -f lavfi -i \"sine=frequency=440:duration=3\" -c:v libx264 -c:a aac -shortest \"{outputPath}\"");

        Assert.True(File.Exists(outputPath));
        Assert.True(new FileInfo(outputPath).Length > 0);
    }

    [Fact]
    public async Task Info_ShouldReturnVideoInfo()
    {
        if (!IsFfmpegAvailable()) return;

        var testVideo = await CreateTestVideoAndGetPath();
        var tool = new VideoProcessingTool();
        var args = JsonSerializer.Serialize(new { action = "info", input_path = testVideo });

        var result = await tool.ExecuteAsync(args);

        Assert.True(result.Success, result.Error);
        Assert.Contains("视频信息", result.Content);
        Assert.Contains("320x240", result.Content);
    }

    [Fact]
    public async Task Trim_ShouldCreateShorterVideo()
    {
        if (!IsFfmpegAvailable()) return;

        var testVideo = await CreateTestVideoAndGetPath();
        var outputPath = Path.Combine(_tempDir, "trimmed.mp4");
        var tool = new VideoProcessingTool();
        var args = JsonSerializer.Serialize(new
        {
            action = "trim",
            input_path = testVideo,
            output_path = outputPath,
            start_time = "0",
            duration = "1.5"
        });

        var result = await tool.ExecuteAsync(args);

        Assert.True(result.Success, result.Error);
        Assert.True(File.Exists(outputPath));
    }

    [Fact]
    public async Task ExtractAudio_ShouldCreateMp3()
    {
        if (!IsFfmpegAvailable()) return;

        var testVideo = await CreateTestVideoAndGetPath();
        var outputPath = Path.Combine(_tempDir, "audio.mp3");
        var tool = new VideoProcessingTool();
        var args = JsonSerializer.Serialize(new
        {
            action = "extract_audio",
            input_path = testVideo,
            output_path = outputPath,
            format = "mp3"
        });

        var result = await tool.ExecuteAsync(args);

        Assert.True(result.Success, result.Error);
        Assert.True(File.Exists(outputPath));
        Assert.True(new FileInfo(outputPath).Length > 0);
    }

    [Fact]
    public async Task Screenshot_ShouldCreateImage()
    {
        if (!IsFfmpegAvailable()) return;

        var testVideo = await CreateTestVideoAndGetPath();
        var outputPath = Path.Combine(_tempDir, "shot.jpg");
        var tool = new VideoProcessingTool();
        var args = JsonSerializer.Serialize(new
        {
            action = "screenshot",
            input_path = testVideo,
            output_path = outputPath,
            time_point = "00:00:01"
        });

        var result = await tool.ExecuteAsync(args);

        Assert.True(result.Success, result.Error);
        Assert.True(File.Exists(outputPath));
    }

    [Fact]
    public async Task Convert_ShouldChangeFormat()
    {
        if (!IsFfmpegAvailable()) return;

        var testVideo = await CreateTestVideoAndGetPath();
        var outputPath = Path.Combine(_tempDir, "converted.avi");
        var tool = new VideoProcessingTool();
        var args = JsonSerializer.Serialize(new
        {
            action = "convert",
            input_path = testVideo,
            output_path = outputPath,
            format = "avi"
        });

        var result = await tool.ExecuteAsync(args);

        Assert.True(result.Success, result.Error);
        Assert.True(File.Exists(outputPath));
    }

    private async Task<string> CreateTestVideoAndGetPath()
    {
        var path = Path.Combine(_tempDir, "source.mp4");
        await RunFfmpegForTestAsync(
            $"-hide_banner -loglevel error -y -f lavfi -i \"color=c=blue:s=320x240:d=3\" -f lavfi -i \"sine=frequency=440:duration=3\" -c:v libx264 -c:a aac -shortest \"{path}\"");

        return path;
    }

    private static async Task RunFfmpegForTestAsync(string arguments)
    {
        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName = "ffmpeg",
            Arguments = arguments,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = System.Diagnostics.Process.Start(psi) ?? throw new InvalidOperationException("无法启动 ffmpeg");
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        try
        {
            await process.WaitForExitAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            try { process.Kill(entireProcessTree: true); } catch { }
            throw new TimeoutException("测试 ffmpeg 命令超过 20 秒未结束。");
        }

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"测试 ffmpeg 命令失败，ExitCode={process.ExitCode}");
        }
    }
}
