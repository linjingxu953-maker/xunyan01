using System.Text.Json;
using DesktopMascot.Agent.Tools;

namespace DesktopMascot.Agent.Tests;

public class ShortVideoMakerIntegrationTests
{
    [Fact]
    public async Task GenerateScript_Tutorial_ShouldReturnContent()
    {
        var tool = new ShortVideoMakerTool();
        var args = JsonSerializer.Serialize(new
        {
            action = "generate_script",
            title = "C#入门教程",
            description = "学习C#基础知识",
            duration = 30,
            style = "tutorial"
        });

        var result = await tool.ExecuteAsync(args);

        Assert.True(result.Success, result.Error);
        Assert.Contains("C#入门教程", result.Content);
        Assert.Contains("脚本", result.Content);
        Assert.Contains("旁白", result.Content);
    }

    [Fact]
    public async Task GenerateScript_AllStyles_ShouldWork()
    {
        var tool = new ShortVideoMakerTool();
        var styles = new[] { "tutorial", "showcase", "narration", "promo", "demo" };

        foreach (var style in styles)
        {
            var args = JsonSerializer.Serialize(new
            {
                action = "generate_script",
                title = $"测试{style}",
                duration = 20,
                style = style
            });

            var result = await tool.ExecuteAsync(args);
            Assert.True(result.Success, $"风格 {style} 失败：{result.Error}");
            Assert.Contains("视频脚本", result.Content);
        }
    }

    [Fact]
    public async Task GenerateComposition_ShouldCreateHtml()
    {
        var tool = new ShortVideoMakerTool();
        var tempDir = Path.Combine(Path.GetTempPath(), $"comp_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var args = JsonSerializer.Serialize(new
            {
                action = "generate_composition",
                title = "产品展示",
                description = "展示新功能",
                duration = 15,
                style = "showcase",
                composition_dir = tempDir,
                accent_color = "#e74c3c",
                transition = "fade"
            });

            var result = await tool.ExecuteAsync(args);

            Assert.True(result.Success, result.Error);
            Assert.Contains("动画生成完成", result.Content);

            var indexHtml = Path.Combine(tempDir, "index.html");
            Assert.True(File.Exists(indexHtml));

            var html = await File.ReadAllTextAsync(indexHtml);
            Assert.Contains("data-composition-id", html);
            Assert.Contains("gsap", html);
            Assert.Contains("#e74c3c", html);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    [Fact]
    public async Task GenerateComposition_WipeTransition_ShouldWork()
    {
        var tool = new ShortVideoMakerTool();
        var tempDir = Path.Combine(Path.GetTempPath(), $"comp_wipe_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var args = JsonSerializer.Serialize(new
            {
                action = "generate_composition",
                title = "测试Wipe转场",
                duration = 20,
                style = "promo",
                composition_dir = tempDir,
                transition = "wipe"
            });

            var result = await tool.ExecuteAsync(args);

            Assert.True(result.Success, result.Error);

            var html = await File.ReadAllTextAsync(Path.Combine(tempDir, "index.html"));
            Assert.Contains("clipPath", html);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    [Fact]
    public async Task ScriptSegments_ShouldHaveCorrectTimeRanges()
    {
        var tool = new ShortVideoMakerTool();
        var args = JsonSerializer.Serialize(new
        {
            action = "generate_script",
            title = "时间测试",
            duration = 60,
            style = "tutorial"
        });

        var result = await tool.ExecuteAsync(args);

        Assert.True(result.Success);
        Assert.Contains("00:00", result.Content);
        Assert.Contains("01:00", result.Content);
    }
}
