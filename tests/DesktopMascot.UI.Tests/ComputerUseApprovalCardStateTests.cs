using Avalonia.Media;
using DesktopMascot.UI.ViewModels;

namespace DesktopMascot.UI.Tests;

public sealed class ComputerUseApprovalCardStateTests
{
    [Fact]
    public void From_UsesReadableDefaultsForEmptyContent()
    {
        var state = ComputerUseApprovalCardState.From(" ", null, "");

        Assert.Equal("等待用户确认", state.Title);
        Assert.Equal("请确认后继续执行。", state.Description);
        Assert.Equal("待确认", state.RiskText);
    }

    [Theory]
    [InlineData("高风险命令执行", "#F87171")]
    [InlineData("文件写入", "#FBBF24")]
    [InlineData("低风险", "#60A5FA")]
    public void RiskBrush_MapsRiskTextToAttentionColor(string riskText, string expectedColor)
    {
        var state = ComputerUseApprovalCardState.From("等待权限确认", "确认桌面操作", riskText);

        var brush = Assert.IsType<SolidColorBrush>(state.RiskBrush);
        Assert.Equal(Color.Parse(expectedColor), brush.Color);
    }
}
