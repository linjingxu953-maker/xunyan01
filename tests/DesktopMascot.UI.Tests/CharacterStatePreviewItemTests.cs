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
            new CharacterImageResult { FilePath = @"C:\assets\characters\yan\idle.png" },
            usesConfiguredImage: true);

        Assert.Equal("配置：idle.png；实际：idle.png", item.DetailText);
    }

    [Fact]
    public void DetailText_UsesReadableFallbackWhenFileIsMissing()
    {
        var item = new CharacterStatePreviewItem(
            "Error",
            "出错",
            "",
            new CharacterImageResult(),
            usesConfiguredImage: false);

        Assert.Equal("配置：未配置；实际：未解析", item.DetailText);
    }
}
