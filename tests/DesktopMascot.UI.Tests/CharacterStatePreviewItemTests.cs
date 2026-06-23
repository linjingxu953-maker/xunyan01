using Avalonia;
using Avalonia.Media;
using DesktopMascot.UI.Services;
using DesktopMascot.UI.ViewModels;

namespace DesktopMascot.UI.Tests;

public sealed class CharacterStatePreviewItemTests
{
    [Fact]
    public void DetailText_IncludesConfiguredAndResolvedFileNames()
    {
        var item = new CharacterStatePreviewItem(
            "Idle",
            "空闲 / 默认",
            "idle.png",
            new CharacterImageResult { Image = new TestImage(), FilePath = @"C:\assets\characters\yan\idle.png" },
            usesConfiguredImage: true);

        Assert.Equal("配置：idle.png；实际：idle.png", item.DetailText);
        Assert.Equal("可用", item.StatusText);
    }

    [Fact]
    public void DetailText_UsesReadableFallbackWhenFileIsMissing()
    {
        var item = new CharacterStatePreviewItem(
            "Error",
            "错误",
            "",
            new CharacterImageResult(),
            usesConfiguredImage: false);

        Assert.Equal("配置：未配置；实际：未解析", item.DetailText);
        Assert.Equal("缺失", item.StatusText);
    }
}

file sealed class TestImage : IImage
{
    public Size Size => new(1, 1);

    public void Draw(DrawingContext context, Rect sourceRect, Rect destRect)
    {
    }
}
