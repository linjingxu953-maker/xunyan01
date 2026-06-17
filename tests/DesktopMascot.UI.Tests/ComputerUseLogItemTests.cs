using Avalonia.Media;
using DesktopMascot.UI.ViewModels;

namespace DesktopMascot.UI.Tests;

public sealed class ComputerUseLogItemTests
{
    [Theory]
    [InlineData("失败", "#F87171")]
    [InlineData("待确认", "#FBBF24")]
    [InlineData("已完成", "#34D399")]
    [InlineData("进行中", "#60A5FA")]
    public void DotBrush_MapsStatusToTimelineColor(string statusText, string expectedColor)
    {
        var item = new ComputerUseLogItem("动作", "详情", statusText, new DateTime(2026, 6, 17, 10, 20, 30, DateTimeKind.Utc));

        var brush = Assert.IsType<SolidColorBrush>(item.DotBrush);
        Assert.Equal(Color.Parse(expectedColor), brush.Color);
    }

    [Fact]
    public void Constructor_FormatsLocalTime()
    {
        var createdAt = new DateTime(2026, 6, 17, 10, 20, 30, DateTimeKind.Local);
        var item = new ComputerUseLogItem("动作", "详情", "进行中", createdAt);

        Assert.Equal("10:20:30", item.TimeText);
    }
}
