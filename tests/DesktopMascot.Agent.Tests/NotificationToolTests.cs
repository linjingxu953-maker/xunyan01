using System.Text.Json;
using DesktopMascot.Agent.Tools;

namespace DesktopMascot.Agent.Tests;

public class NotificationToolTests
{
    [Fact]
    public async Task Notify_MissingTitle_ShouldFail()
    {
        var tool = new NotificationTool();
        var args = JsonSerializer.Serialize(new { action = "notify" });
        var result = await tool.ExecuteAsync(args);

        Assert.False(result.Success);
        Assert.Contains("缺少 title", result.Error);
    }

    [Fact]
    public async Task ListReminders_ShouldReturnEmpty()
    {
        var tool = new NotificationTool();
        var args = JsonSerializer.Serialize(new { action = "list_reminders" });
        var result = await tool.ExecuteAsync(args);

        Assert.True(result.Success);
        Assert.Contains("暂无提醒", result.Content);
    }

    [Fact]
    public async Task CancelReminder_Nonexistent_ShouldFail()
    {
        var tool = new NotificationTool();
        var args = JsonSerializer.Serialize(new { action = "cancel_reminder", reminder_id = "nonexistent" });
        var result = await tool.ExecuteAsync(args);

        Assert.False(result.Success);
        Assert.Contains("不存在", result.Error);
    }

    [Fact]
    public async Task Toast_MissingTitle_ShouldFail()
    {
        var tool = new NotificationTool();
        var args = JsonSerializer.Serialize(new { action = "toast" });
        var result = await tool.ExecuteAsync(args);

        Assert.False(result.Success);
        Assert.Contains("缺少 title", result.Error);
    }

    [Fact]
    public void NotificationTool_Metadata_ShouldBeCorrect()
    {
        var tool = new NotificationTool();
        Assert.Equal("notification", tool.Name);
        Assert.Contains("notify", tool.ParametersSchema);
        Assert.Contains("reminder", tool.ParametersSchema);
    }
}
