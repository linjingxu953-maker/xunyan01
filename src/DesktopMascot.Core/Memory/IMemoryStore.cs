namespace DesktopMascot.Core.Memory;

/// <summary>
/// 记忆存储接口
/// </summary>
public interface IMemoryStore
{
    /// <summary>保存记忆</summary>
    Task<MemoryEntry> SaveAsync(MemoryEntry entry, CancellationToken ct = default);
    
    /// <summary>获取记忆</summary>
    Task<MemoryEntry?> GetByIdAsync(string id, CancellationToken ct = default);
    
    /// <summary>按键获取记忆</summary>
    Task<MemoryEntry?> GetByKeyAsync(string key, MemoryType type, CancellationToken ct = default);
    
    /// <summary>删除记忆</summary>
    Task<bool> DeleteAsync(string id, CancellationToken ct = default);
    
    /// <summary>搜索记忆</summary>
    Task<MemorySearchResult> SearchAsync(string query, MemoryType? type = null, int limit = 50, CancellationToken ct = default);
    
    /// <summary>获取指定类型的所有记忆</summary>
    Task<List<MemoryEntry>> GetByTypeAsync(MemoryType type, int limit = 100, CancellationToken ct = default);
    
    /// <summary>确认记忆</summary>
    Task<bool> ConfirmAsync(string id, CancellationToken ct = default);
    
    /// <summary>获取统计信息</summary>
    Task<MemoryStatistics> GetStatisticsAsync(CancellationToken ct = default);
    
    /// <summary>导出记忆</summary>
    Task<string> ExportAsync(MemoryType? type = null, CancellationToken ct = default);
    
    /// <summary>导入记忆</summary>
    Task<int> ImportAsync(string data, CancellationToken ct = default);
}
