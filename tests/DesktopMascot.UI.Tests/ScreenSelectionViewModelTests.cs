using Avalonia;
using DesktopMascot.UI.Services;
using DesktopMascot.UI.ViewModels;

namespace DesktopMascot.UI.Tests;

public sealed class ScreenSelectionViewModelTests
{
    [Fact]
    public void Update_ReportsPhysicalScreenCoordinatesWithDpiAndNegativeScreenOrigin()
    {
        var viewModel = new ScreenSelectionViewModel();
        viewModel.SetScreenTransform(new PixelRect(-1920, 120, 1920, 1080), 1.5);

        viewModel.Begin(new Point(100, 80));
        viewModel.Update(new Point(20, 10));

        Assert.True(viewModel.HasValidSelection);
        Assert.True(viewModel.HasSelectionRegionSummary);
        Assert.Equal("屏幕坐标 -1890, 135 · 120 x 105", viewModel.SelectionRegionSummary);

        var result = viewModel.Complete(new Point(20, 10));

        Assert.NotNull(result);
        Assert.Equal(-1890, result.X);
        Assert.Equal(135, result.Y);
        Assert.Equal(120, result.Width);
        Assert.Equal(105, result.Height);
    }

    [Fact]
    public void Update_KeepsInvalidSelectionSummaryActionable()
    {
        var viewModel = new ScreenSelectionViewModel();

        viewModel.Begin(new Point(0, 0));
        viewModel.Update(new Point(6, 6));

        Assert.False(viewModel.HasValidSelection);
        Assert.False(viewModel.HasSelectionRegionSummary);
        Assert.Equal("区域至少需要 12 x 12", viewModel.SelectionRegionSummary);
    }

    [Fact]
    public void OverlayOwnerPolicy_DoesNotUseHiddenOwnerWindow()
    {
        Assert.False(ScreenSelectionOverlayService.ShouldUseOwnerForOverlay(hasOwner: true, ownerIsVisible: false));
        Assert.False(ScreenSelectionOverlayService.ShouldUseOwnerForOverlay(hasOwner: false, ownerIsVisible: true));
        Assert.True(ScreenSelectionOverlayService.ShouldUseOwnerForOverlay(hasOwner: true, ownerIsVisible: true));
    }
}
