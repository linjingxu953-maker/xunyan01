namespace DesktopMascot.UI.ViewModels;

public sealed class MemoryBrowserItem
{
    public MemoryBrowserItem(
        string id,
        string type,
        string key,
        string source,
        string status,
        string content,
        string tags,
        string updatedAt,
        string expiresAt)
    {
        Id = id;
        Type = type;
        Key = key;
        Source = source;
        Status = status;
        Content = content;
        Tags = tags;
        UpdatedAt = updatedAt;
        ExpiresAt = expiresAt;
    }

    public string Id { get; }
    public string Type { get; }
    public string Key { get; }
    public string Source { get; }
    public string Status { get; }
    public string Content { get; }
    public string Tags { get; }
    public string UpdatedAt { get; }
    public string ExpiresAt { get; }
    public bool IsConfirmed => Status == "已确认";
}
