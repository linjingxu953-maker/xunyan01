using DesktopMascot.UI.Services;
using DesktopMascot.UI.ViewModels;

namespace DesktopMascot.UI.Tests;

public sealed class ScreenScreenshotPreviewCardStateTests
{
    [Fact]
    public void From_AllowsCopyButNotExpandWhenScreenshotFileIsMissing()
    {
        var context = CreateContext(Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.png"));

        var state = ScreenScreenshotPreviewCardState.From(context, isExpanded: true);

        Assert.True(state.CanCopyPath);
        Assert.False(state.CanTogglePreview);
        Assert.False(state.ShowExpandedPreview);
        Assert.Equal("查看大图", state.ToggleText);
    }

    [Fact]
    public void From_ExpandsPreviewOnlyWhenScreenshotFileExists()
    {
        var screenshotPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.png");
        File.WriteAllBytes(screenshotPath, [137, 80, 78, 71]);

        try
        {
            var context = CreateContext(screenshotPath);

            var collapsed = ScreenScreenshotPreviewCardState.From(context, isExpanded: false);
            var expanded = ScreenScreenshotPreviewCardState.From(context, isExpanded: true);

            Assert.True(collapsed.CanTogglePreview);
            Assert.False(collapsed.ShowExpandedPreview);
            Assert.Equal("查看大图", collapsed.ToggleText);
            Assert.True(expanded.ShowExpandedPreview);
            Assert.Equal("收起大图", expanded.ToggleText);
        }
        finally
        {
            File.Delete(screenshotPath);
        }
    }

    private static ScreenSelectionContextState CreateContext(string screenshotPath)
    {
        return ScreenSelectionContextState.From(new ScreenSelectionResult
        {
            X = 12,
            Y = 24,
            Width = 320,
            Height = 180,
            IsConfirmed = true
        }).WithResult(
            success: true,
            content: $$"""
            {
              "identification": "这是一个表格区域",
              "screenshotPath": "{{screenshotPath.Replace("\\", "\\\\")}}"
            }
            """,
            error: null);
    }
}
