namespace DesktopMascot.UI.ViewModels;

public sealed class PendingMemoryReviewItem
{
    public PendingMemoryReviewItem(
        string id,
        string type,
        string key,
        string source,
        string reason,
        string requestedAt,
        string status,
        string content,
        string tags,
        string expiresAt)
    {
        Id = id;
        Type = type;
        Key = key;
        Source = source;
        Reason = reason;
        RequestedAt = requestedAt;
        Status = status;
        Content = content;
        Tags = tags;
        ExpiresAt = expiresAt;
    }

    public string Id { get; }
    public string Type { get; }
    public string Key { get; }
    public string Source { get; }
    public string Reason { get; }
    public string RequestedAt { get; }
    public string Status { get; }
    public string Content { get; }
    public string Tags { get; }
    public string ExpiresAt { get; }
}
