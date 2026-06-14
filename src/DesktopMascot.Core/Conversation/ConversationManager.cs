using System.Collections.Concurrent;

namespace DesktopMascot.Core.Conversation;

/// <summary>
/// 对话管理器 - 管理对话历史、上下文、摘要
/// 线程安全：支持 UI 线程和后台任务并发访问
/// </summary>
public class ConversationManager
{
    private readonly ConcurrentDictionary<string, ConversationContext> _conversations = new();
    private readonly ConcurrentDictionary<string, ConcurrentBag<string>> _userConversations = new(); // userId -> conversationIds
    private readonly object _activeIdLock = new();
    private string? _activeConversationId;

    /// <summary>当前活跃对话</summary>
    public ConversationContext? ActiveConversation
    {
        get
        {
            string? activeId;
            lock (_activeIdLock)
            {
                activeId = _activeConversationId;
            }
            return activeId != null && _conversations.TryGetValue(activeId, out var ctx)
                ? ctx : null;
        }
    }

    /// <summary>创建新对话</summary>
    public ConversationContext CreateConversation(string title = "", string? userId = null)
    {
        var conversation = new ConversationContext
        {
            Title = string.IsNullOrEmpty(title) ? $"对话 {DateTime.Now:MM-dd HH:mm}" : title
        };

        _conversations[conversation.Id] = conversation;
        lock (_activeIdLock)
        {
            _activeConversationId = conversation.Id;
        }

        if (!string.IsNullOrEmpty(userId))
        {
            var bag = _userConversations.GetOrAdd(userId, _ => new ConcurrentBag<string>());
            bag.Add(conversation.Id);
        }

        return conversation;
    }

    /// <summary>添加用户消息</summary>
    public ConversationMessage AddUserMessage(string content, Dictionary<string, string>? metadata = null)
    {
        ArgumentNullException.ThrowIfNull(content);

        var conversation = ActiveConversation;
        if (conversation == null)
        {
            conversation = CreateConversation();
        }

        var message = new ConversationMessage
        {
            Role = "user",
            Content = content,
            Metadata = metadata ?? new Dictionary<string, string>(),
            TokenCount = EstimateTokens(content)
        };

        conversation.AddMessage(message);
        return message;
    }

    /// <summary>添加助手消息</summary>
    public ConversationMessage AddAssistantMessage(string content, string? toolCalls = null, string? toolResults = null)
    {
        ArgumentNullException.ThrowIfNull(content);

        var conversation = ActiveConversation;
        if (conversation == null)
        {
            conversation = CreateConversation();
        }

        var message = new ConversationMessage
        {
            Role = "assistant",
            Content = content,
            ToolCalls = toolCalls,
            ToolResults = toolResults,
            TokenCount = EstimateTokens(content)
        };

        conversation.AddMessage(message);
        return message;
    }

    /// <summary>获取对话上下文用于LLM调用</summary>
    public List<ConversationMessage> GetContextForLLM(int maxMessages = 20)
    {
        var conversation = ActiveConversation;
        if (conversation == null) return new List<ConversationMessage>();

        return conversation.GetRecentMessages(maxMessages);
    }

    /// <summary>获取对话摘要</summary>
    public ConversationSummary? GetSummary()
    {
        var conversation = ActiveConversation;
        if (conversation == null) return null;

        return new ConversationSummary
        {
            ConversationId = conversation.Id,
            Summary = conversation.Summary,
            MessageCount = conversation.Messages.Count,
            KeyTopics = ExtractKeyTopics(conversation),
            GeneratedAt = DateTime.UtcNow
        };
    }

    /// <summary>更新对话摘要</summary>
    public void UpdateSummary(string summary)
    {
        var conversation = ActiveConversation;
        if (conversation != null)
        {
            conversation.Summary = summary;
        }
    }

    /// <summary>切换对话</summary>
    public bool SwitchConversation(string conversationId)
    {
        if (_conversations.ContainsKey(conversationId))
        {
            lock (_activeIdLock)
            {
                _activeConversationId = conversationId;
            }
            return true;
        }
        return false;
    }

    /// <summary>获取用户的所有对话</summary>
    public List<ConversationContext> GetUserConversations(string userId, int limit = 20)
    {
        if (!_userConversations.TryGetValue(userId, out var conversationIds))
            return new List<ConversationContext>();

        return conversationIds
            .TakeLast(limit)
            .Select(id => _conversations.TryGetValue(id, out var ctx) ? ctx : null)
            .Where(c => c != null)
            .Cast<ConversationContext>()
            .OrderByDescending(c => c.LastMessageAt)
            .ToList();
    }

    /// <summary>删除对话 — 同时清理活跃引用和用户关联</summary>
    public bool DeleteConversation(string conversationId)
    {
        if (!_conversations.TryRemove(conversationId, out _))
            return false;

        lock (_activeIdLock)
        {
            if (_activeConversationId == conversationId)
            {
                _activeConversationId = null;
            }
        }

        // 清理用户关联
        foreach (var kvp in _userConversations)
        {
            // ConcurrentBag 不支持 Remove，用重建方式
            var filtered = new ConcurrentBag<string>(kvp.Value.Where(id => id != conversationId));
            _userConversations.TryUpdate(kvp.Key, filtered, kvp.Value);
        }

        return true;
    }

    /// <summary>提取关键主题</summary>
    private List<string> ExtractKeyTopics(ConversationContext conversation)
    {
        var topics = new HashSet<string>();
        foreach (var msg in conversation.Messages.Where(m => m.Role == "user"))
        {
            // 简单的关键词提取
            var words = msg.Content.Split(new[] { ' ', '，', '。', '？', '！' },
                StringSplitOptions.RemoveEmptyEntries);
            foreach (var word in words.Where(w => w.Length > 2))
            {
                topics.Add(word);
            }
        }
        return topics.Take(10).ToList();
    }

    /// <summary>估算token数</summary>
    private static int EstimateTokens(string text)
    {
        // 简单估算：中文约1.5字/token，英文约4字符/token
        int chineseChars = text.Count(c => c > 0x4E00 && c < 0x9FFF);
        int otherChars = text.Length - chineseChars;
        return (int)(chineseChars / 1.5 + otherChars / 4.0);
    }
}
