namespace DesktopMascot.Core.Conversation;

/// <summary>
/// 对话消息
/// </summary>
public class ConversationMessage
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Role { get; set; } = string.Empty; // "user" | "assistant" | "system"
    public string Content { get; set; } = string.Empty;
    public string? ToolCalls { get; set; }
    public string? ToolResults { get; set; }
    public Dictionary<string, string> Metadata { get; set; } = new();
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public int TokenCount { get; set; }
}

/// <summary>
/// 对话上下文
/// </summary>
public class ConversationContext
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Title { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public List<ConversationMessage> Messages { get; set; } = new();
    public Dictionary<string, object> SharedState { get; set; } = new();
    public List<string> ReferencedFiles { get; set; } = new();
    public List<string> ReferencedUrls { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastMessageAt { get; set; } = DateTime.UtcNow;
    public int TotalTokens { get; set; }

    /// <summary>获取最近N条消息</summary>
    public List<ConversationMessage> GetRecentMessages(int count = 10)
    {
        return Messages.TakeLast(count).ToList();
    }

    /// <summary>添加消息</summary>
    public void AddMessage(ConversationMessage message)
    {
        Messages.Add(message);
        LastMessageAt = DateTime.UtcNow;
        TotalTokens += message.TokenCount;
    }

    /// <summary>获取对话摘要（用于注入prompt）</summary>
    public string GetContextSummary(int maxMessages = 20)
    {
        var recent = GetRecentMessages(maxMessages);
        if (recent.Count == 0) return string.Empty;

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"对话标题: {Title}");
        
        if (!string.IsNullOrEmpty(Summary))
        {
            sb.AppendLine($"摘要: {Summary}");
        }

        if (ReferencedFiles.Count > 0)
        {
            sb.AppendLine($"相关文件: {string.Join(", ", ReferencedFiles.Take(5))}");
        }

        sb.AppendLine();
        sb.AppendLine("最近对话:");
        foreach (var msg in recent.TakeLast(5))
        {
            var prefix = msg.Role == "user" ? "用户" : "助手";
            var content = msg.Content.Length > 200 ? msg.Content[..200] + "..." : msg.Content;
            sb.AppendLine($"[{prefix}]: {content}");
        }

        return sb.ToString();
    }
}

/// <summary>
/// 对话摘要
/// </summary>
public class ConversationSummary
{
    public string ConversationId { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public List<string> KeyTopics { get; set; } = new();
    public List<string> ActionItems { get; set; } = new();
    public List<string> DecisionsMade { get; set; } = new();
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    public int MessageCount { get; set; }
}
