using DesktopMascot.UI.Services;
using DesktopMascot.UI.ViewModels;

namespace DesktopMascot.UI.Tests;

public sealed class ScreenSelectionContextStateTests
{
    [Fact]
    public void From_FormatsConfirmedRegionForTaskContextCard()
    {
        var result = new ScreenSelectionResult
        {
            X = -1890,
            Y = 135,
            Width = 120,
            Height = 105,
            IsConfirmed = true
        };

        var state = ScreenSelectionContextState.From(result, "等待视觉理解");

        Assert.True(state.HasRegion);
        Assert.Equal("屏幕圈选区域", state.Title);
        Assert.Equal("屏幕坐标 -1890, 135", state.RegionText);
        Assert.Equal("120 x 105", state.SizeText);
        Assert.Equal("等待视觉理解", state.StatusText);
        Assert.Equal("将把该屏幕区域交给视觉理解：(-1890, 135) 120x105", state.DetailText);
    }

    [Fact]
    public void From_ReturnsEmptyStateForUnconfirmedRegion()
    {
        var result = new ScreenSelectionResult
        {
            X = 20,
            Y = 30,
            Width = 120,
            Height = 105,
            IsConfirmed = false
        };

        var state = ScreenSelectionContextState.From(result, "等待视觉理解");

        Assert.False(state.HasRegion);
        Assert.Equal("屏幕圈选", state.Title);
        Assert.Equal("暂无圈选区域", state.RegionText);
        Assert.Equal("等待 Ctrl+Shift+S 或点击圈选", state.SizeText);
    }
}
