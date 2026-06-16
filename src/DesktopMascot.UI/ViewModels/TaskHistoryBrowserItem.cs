namespace DesktopMascot.UI.ViewModels;

public sealed class TaskHistoryBrowserItem
{
    public TaskHistoryBrowserItem(
        string id,
        string title,
        string type,
        string status,
        string createdAt,
        string duration,
        string input,
        string result,
        string error,
        string eventCount,
        string toolCallCount,
        string transcript)
    {
        Id = id;
        Title = title;
        Type = type;
        Status = status;
        CreatedAt = createdAt;
        Duration = duration;
        Input = input;
        Result = result;
        Error = error;
        EventCount = eventCount;
        ToolCallCount = toolCallCount;
        Transcript = transcript;
    }

    public string Id { get; }
    public string Title { get; }
    public string Type { get; }
    public string Status { get; }
    public string CreatedAt { get; }
    public string Duration { get; }
    public string Input { get; }
    public string Result { get; }
    public string Error { get; }
    public string EventCount { get; }
    public string ToolCallCount { get; }
    public string Transcript { get; }
    public bool HasError => !string.IsNullOrWhiteSpace(Error) && Error != "无";
}
