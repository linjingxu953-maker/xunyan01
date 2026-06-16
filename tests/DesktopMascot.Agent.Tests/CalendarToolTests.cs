using DesktopMascot.Agent.Tools;

namespace DesktopMascot.Agent.Tests;

public class CalendarToolTests
{
    [Fact]
    public async Task GetNow_ShouldReturnCurrentTime()
    {
        var tool = new CalendarTool();
        var args = JsonSerializer.Serialize(new { action = "now" });
        var result = await tool.ExecuteAsync(args);

        Assert.True(result.Success);
        Assert.Contains("当前时间", result.Content);
        Assert.Contains("星期", result.Content);
    }

    [Fact]
    public async Task FormatDate_ShouldFormatDate()
    {
        var tool = new CalendarTool();
        var args = JsonSerializer.Serialize(new { action = "format", date = "2026-06-14", format = "yyyy/MM/dd" });
        var result = await tool.ExecuteAsync(args);

        Assert.True(result.Success);
        Assert.Contains("2026/06/14", result.Content);
    }

    [Fact]
    public async Task FormatDate_InvalidDate_ShouldFail()
    {
        var tool = new CalendarTool();
        var args = JsonSerializer.Serialize(new { action = "format", date = "invalid-date" });
        var result = await tool.ExecuteAsync(args);

        Assert.False(result.Success);
        Assert.Contains("无效", result.Error);
    }

    [Fact]
    public async Task CalculateDiff_ShouldReturnDifference()
    {
        var tool = new CalendarTool();
        var args = JsonSerializer.Serialize(new { action = "diff", date = "2026-06-01", date2 = "2026-06-14" });
        var result = await tool.ExecuteAsync(args);

        Assert.True(result.Success);
        Assert.Contains("差异", result.Content);
        Assert.Contains("13 天", result.Content);
    }

    [Fact]
    public async Task AddTime_ShouldAddDays()
    {
        var tool = new CalendarTool();
        var args = JsonSerializer.Serialize(new { action = "add", date = "2026-06-14", days = 7 });
        var result = await tool.ExecuteAsync(args);

        Assert.True(result.Success);
        Assert.Contains("2026-06-21", result.Content);
    }

    [Fact]
    public async Task AddTime_ShouldAddHours()
    {
        var tool = new CalendarTool();
        var args = JsonSerializer.Serialize(new { action = "add", date = "2026-06-14 12:00:00", hours = 3 });
        var result = await tool.ExecuteAsync(args);

        Assert.True(result.Success);
        Assert.Contains("15:00:00", result.Content);
    }

    [Fact]
    public async Task CalculateCountdown_ShouldReturnRemaining()
    {
        var tool = new CalendarTool();
        var futureDate = DateTime.Now.AddDays(10).ToString("yyyy-MM-dd");
        var args = JsonSerializer.Serialize(new { action = "countdown", date = futureDate });
        var result = await tool.ExecuteAsync(args);

        Assert.True(result.Success);
        Assert.Contains("倒计时", result.Content);
    }

    [Fact]
    public async Task CalculateCountdown_PastDate_ShouldShowExpired()
    {
        var tool = new CalendarTool();
        var pastDate = DateTime.Now.AddDays(-5).ToString("yyyy-MM-dd");
        var args = JsonSerializer.Serialize(new { action = "countdown", date = pastDate });
        var result = await tool.ExecuteAsync(args);

        Assert.True(result.Success);
        Assert.Contains("已过期", result.Content);
    }

    [Fact]
    public async Task GetSchedule_ShouldReturnSchedule()
    {
        var tool = new CalendarTool();
        var args = JsonSerializer.Serialize(new { action = "schedule" });
        var result = await tool.ExecuteAsync(args);

        Assert.True(result.Success);
        Assert.Contains("今日日程", result.Content);
    }

    [Fact]
    public void CalendarTool_Metadata_ShouldBeCorrect()
    {
        var tool = new CalendarTool();
        Assert.Equal("calendar", tool.Name);
        Assert.Contains("now", tool.ParametersSchema);
        Assert.Contains("format", tool.ParametersSchema);
        Assert.Contains("diff", tool.ParametersSchema);
    }
}
