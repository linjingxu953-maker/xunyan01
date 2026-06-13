namespace DesktopMascot.Core.Memory;

/// <summary>
/// 记忆确认处理器接口
/// </summary>
public interface IMemoryConfirmationHandler
{
    /// <summary>请求用户确认记忆保存</summary>
    Task<bool> RequestConfirmationAsync(MemoryConfirmRequest request, CancellationToken ct = default);
}

/// <summary>
/// 记忆管理器
/// </summary>
public class MemoryManager
{
    private readonly IMemoryStore _store;
    private readonly IMemoryConfirmationHandler? _confirmationHandler;

    public MemoryManager(IMemoryStore store, IMemoryConfirmationHandler? confirmationHandler = null)
    {
        _store = store;
        _confirmationHandler = confirmationHandler;
    }

    /// <summary>保存记忆（需要确认）</summary>
    public async Task<MemoryEntry?> SaveWithConfirmationAsync(
        MemoryEntry proposed,
        string reason,
        CancellationToken ct = default)
    {
        // 需要用户确认的类型（User 类型总是需要确认）
        bool requiresConfirmation = proposed.Type == MemoryType.User;

        if (requiresConfirmation)
        {
            if (_confirmationHandler == null)
            {
                // 用户记忆必须确认，但无确认处理器 → 拒绝保存
                return null;
            }

            var request = new MemoryConfirmRequest
            {
                ProposedMemory = proposed,
                Reason = reason
            };

            var confirmed = await _confirmationHandler.RequestConfirmationAsync(request, ct);
            if (!confirmed)
                return null;

            proposed.IsConfirmed = true;
        }
        else
        {
            // 非用户类型（Project/Skill/History）自动确认
            proposed.IsConfirmed = true;
        }

        return await _store.SaveAsync(proposed, ct);
    }

    /// <summary>快速保存（自动确认）</summary>
    public async Task<MemoryEntry> SaveQuickAsync(
        string key,
        string content,
        MemoryType type,
        string? source = null,
        CancellationToken ct = default)
    {
        // 检查是否已存在
        var existing = await _store.GetByKeyAsync(key, type, ct);

        if (existing != null)
        {
            existing.Content = content;
            existing.Source = source;
            return await _store.SaveAsync(existing, ct);
        }

        var entry = new MemoryEntry
        {
            Type = type,
            Key = key,
            Content = content,
            Source = source,
            IsConfirmed = true
        };

        return await _store.SaveAsync(entry, ct);
    }

    /// <summary>搜索记忆</summary>
    public Task<MemorySearchResult> SearchAsync(string query, MemoryType? type = null, CancellationToken ct = default)
    {
        return _store.SearchAsync(query, type, ct: ct);
    }

    /// <summary>获取记忆</summary>
    public Task<MemoryEntry?> GetAsync(string key, MemoryType type, CancellationToken ct = default)
    {
        return _store.GetByKeyAsync(key, type, ct);
    }

    /// <summary>删除记忆</summary>
    public Task<bool> DeleteAsync(string id, CancellationToken ct = default)
    {
        return _store.DeleteAsync(id, ct);
    }

    /// <summary>获取统计</summary>
    public Task<MemoryStatistics> GetStatisticsAsync(CancellationToken ct = default)
    {
        return _store.GetStatisticsAsync(ct);
    }

    /// <summary>导出记忆</summary>
    public Task<string> ExportAsync(MemoryType? type = null, CancellationToken ct = default)
    {
        return _store.ExportAsync(type, ct);
    }

    /// <summary>导入记忆</summary>
    public Task<int> ImportAsync(string data, CancellationToken ct = default)
    {
        return _store.ImportAsync(data, ct);
    }
}
