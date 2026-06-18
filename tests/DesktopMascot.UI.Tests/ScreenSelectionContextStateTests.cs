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

    [Fact]
    public void WithResult_UsesReadableScreenUnderstandJsonSummary()
    {
        var state = ScreenSelectionContextState.From(new ScreenSelectionResult
        {
            X = 12,
            Y = 24,
            Width = 320,
            Height = 180,
            IsConfirmed = true
        });

        var updated = state.WithResult(
            success: true,
            content: """
            {
              "identification": "这是一个 PowerShell 报错窗口",
              "understanding": "用户需要定位命令启动失败原因",
              "confidence": 0.86
            }
            """,
            error: null);

        Assert.True(updated.HasRegion);
        Assert.Equal("识别完成", updated.StatusText);
        Assert.Equal("识别：这是一个 PowerShell 报错窗口；理解：用户需要定位命令启动失败原因", updated.DetailText);
        Assert.Equal("屏幕坐标 12, 24", updated.RegionText);
        Assert.Equal("320 x 180", updated.SizeText);
    }

    [Fact]
    public void WithResult_ReportsFailureWithoutDroppingRegion()
    {
        var state = ScreenSelectionContextState.From(new ScreenSelectionResult
        {
            X = -100,
            Y = 40,
            Width = 200,
            Height = 120,
            IsConfirmed = true
        });

        var updated = state.WithResult(success: false, content: null, error: "视觉模型调用失败");

        Assert.True(updated.HasRegion);
        Assert.Equal("识别失败", updated.StatusText);
        Assert.Equal("视觉模型调用失败", updated.DetailText);
        Assert.Equal("屏幕坐标 -100, 40", updated.RegionText);
        Assert.Equal("200 x 120", updated.SizeText);
    }
}
