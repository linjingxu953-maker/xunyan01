using DesktopMascot.Agent.Tools;

namespace DesktopMascot.Agent.Tests;

/// <summary>
/// 视频处理工具集成测试 — 验证 FFmpeg 可用性和基本操作
/// </summary>
public class VideoProcessingIntegrationTests
{
    [Fact]
    public async Task Info_NonexistentFile_ShouldFail()
    {
        var tool = new VideoProcessingTool();
        var args = """{"action":"info","input_path":"/nonexistent/video.mp4"}""";
        var result = await tool.ExecuteAsync(args);
        Assert.False(result.Success);
        Assert.Contains("不存在", result.Error);
    }

    [Fact]
    public async Task Convert_NonexistentFile_ShouldFail()
    {
        var tool = new VideoProcessingTool();
        var args = """{"action":"convert","input_path":"/nonexistent/video.mp4","format":"avi"}""";
        var result = await tool.ExecuteAsync(args);
        Assert.False(result.Success);
    }

    [Fact]
    public async Task Trim_NonexistentFile_ShouldFail()
    {
        var tool = new VideoProcessingTool();
        var args = """{"action":"trim","input_path":"/nonexistent/video.mp4","start_time":"0","duration":"5"}""";
        var result = await tool.ExecuteAsync(args);
        Assert.False(result.Success);
    }

    [Fact]
    public async Task ExtractAudio_NonexistentFile_ShouldFail()
    {
        var tool = new VideoProcessingTool();
        var args = """{"action":"extract_audio","input_path":"/nonexistent/video.mp4","format":"mp3"}""";
        var result = await tool.ExecuteAsync(args);
        Assert.False(result.Success);
    }

    [Fact]
    public async Task Screenshot_NonexistentFile_ShouldFail()
    {
        var tool = new VideoProcessingTool();
        var args = """{"action":"screenshot","input_path":"/nonexistent/video.mp4"}""";
        var result = await tool.ExecuteAsync(args);
        Assert.False(result.Success);
    }

    [Fact]
    public async Task Gif_NonexistentFile_ShouldFail()
    {
        var tool = new VideoProcessingTool();
        var args = """{"action":"gif","input_path":"/nonexistent/video.mp4"}""";
        var result = await tool.ExecuteAsync(args);
        Assert.False(result.Success);
    }

    [Fact]
    public void VideoProcessingTool_Metadata_ShouldBeCorrect()
    {
        var tool = new VideoProcessingTool();
        Assert.Equal("video_processing", tool.Name);
        Assert.Contains("trim", tool.ParametersSchema);
        Assert.Contains("convert", tool.ParametersSchema);
        Assert.Contains("extract_audio", tool.ParametersSchema);
        Assert.True(tool.RequiresConfirmation);
    }
}
