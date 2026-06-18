using DesktopMascot.Core.Tools;

namespace DesktopMascot.Core.Tests;

public class ToolChainTemplatesTests
{
    [Fact]
    public void ShortVideoProduction_ShouldHaveSteps()
    {
        var chain = ToolChainTemplates.ShortVideoProduction("test", "hello", "out.mp4");
        Assert.Equal(4, chain.Steps.Count);
        Assert.Equal("short_video_maker", chain.Steps[0].ToolName);
    }

    [Fact]
    public void SecurityScanAndFix_ShouldHaveSteps()
    {
        var chain = ToolChainTemplates.SecurityScanAndFix("/test");
        Assert.Equal(2, chain.Steps.Count);
        Assert.Equal("security_scan", chain.Steps[0].ToolName);
        Assert.Equal("code_analysis", chain.Steps[1].ToolName);
    }

    [Fact]
    public void VideoToGif_ShouldHaveSteps()
    {
        var chain = ToolChainTemplates.VideoToGif("input.mp4", "out.gif");
        Assert.Equal(2, chain.Steps.Count);
        Assert.Equal("video_processing", chain.Steps[0].ToolName);
    }

    [Fact]
    public void ProjectHealthCheck_ShouldHaveSteps()
    {
        var chain = ToolChainTemplates.ProjectHealthCheck("/project");
        Assert.Equal(4, chain.Steps.Count);
    }

    [Fact]
    public void BackupFiles_ShouldHaveSteps()
    {
        var chain = ToolChainTemplates.BackupFiles("/src", "/backup");
        Assert.Equal(2, chain.Steps.Count);
        Assert.Equal("batch_file_processor", chain.Steps[0].ToolName);
    }

    [Fact]
    public void DatabaseMaintenance_ShouldHaveSteps()
    {
        var chain = ToolChainTemplates.DatabaseMaintenance("test.db");
        Assert.Equal(3, chain.Steps.Count);
        Assert.Equal("database", chain.Steps[0].ToolName);
    }

    [Fact]
    public void ImageBatchProcess_ShouldHaveSteps()
    {
        var chain = ToolChainTemplates.ImageBatchProcess("/in", "/out", "jpg", 80);
        Assert.Equal(2, chain.Steps.Count);
    }

    [Fact]
    public void AllTemplates_ShouldHaveNames()
    {
        var templates = new[]
        {
            ToolChainTemplates.ShortVideoProduction("t", "v", "o"),
            ToolChainTemplates.SecurityScanAndFix("/"),
            ToolChainTemplates.VideoToGif("i", "o"),
            ToolChainTemplates.ProjectHealthCheck("/"),
            ToolChainTemplates.BackupFiles("/", "/"),
            ToolChainTemplates.DatabaseMaintenance("db"),
            ToolChainTemplates.ImageBatchProcess("/", "/", "jpg"),
            ToolChainTemplates.WebContentAnalysis("https://example.com")
        };

        foreach (var chain in templates)
        {
            Assert.False(string.IsNullOrEmpty(chain.Name));
            Assert.NotEmpty(chain.Steps);
        }
    }

    [Fact]
    public void Templates_ShouldHaveVariables()
    {
        var chain = ToolChainTemplates.ShortVideoProduction("my video", "narration", "output.mp4");
        Assert.True(chain.Variables.ContainsKey("title"));
        Assert.True(chain.Variables.ContainsKey("voice_text"));
        Assert.True(chain.Variables.ContainsKey("output_path"));
    }
}
