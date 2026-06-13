namespace DesktopMascot.UI.ViewModels;

public sealed class TaskToolCallItem
{
    public TaskToolCallItem(string toolName, string statusText, string detail, DateTime createdAt)
    {
        ToolName = toolName;
        StatusText = statusText;
        Detail = detail;
        TimeText = createdAt.ToLocalTime().ToString("HH:mm:ss");
    }

    public string ToolName { get; }
    public string StatusText { get; }
    public string Detail { get; }
    public string TimeText { get; }
}
