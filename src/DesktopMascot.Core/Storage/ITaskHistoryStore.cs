namespace DesktopMascot.Core.Storage;

/// <summary>
/// 任务历史存储接口
/// </summary>
public interface ITaskHistoryStore
{
    /// <summary>保存任务记录</summary>
    Task<TaskHistoryRecord> SaveTaskAsync(TaskHistoryRecord record, CancellationToken ct = default);
    
    /// <summary>更新任务记录</summary>
    Task<TaskHistoryRecord?> UpdateTaskAsync(TaskHistoryRecord record, CancellationToken ct = default);
    
    /// <summary>获取任务记录</summary>
    Task<TaskHistoryRecord?> GetTaskAsync(string taskId, CancellationToken ct = default);
    
    /// <summary>删除任务记录</summary>
    Task<bool> DeleteTaskAsync(string taskId, CancellationToken ct = default);
    
    /// <summary>搜索任务</summary>
    Task<TaskHistorySearchResult> SearchTasksAsync(string query, int limit = 50, CancellationToken ct = default);
    
    /// <summary>获取最近任务</summary>
    Task<List<TaskHistoryRecord>> GetRecentTasksAsync(int limit = 20, CancellationToken ct = default);
    
    /// <summary>记录工具调用</summary>
    Task<ToolCallRecord> SaveToolCallAsync(ToolCallRecord record, CancellationToken ct = default);
    
    /// <summary>获取任务的工具调用</summary>
    Task<List<ToolCallRecord>> GetToolCallsAsync(string taskId, CancellationToken ct = default);
    
    /// <summary>获取统计</summary>
    Task<TaskHistoryStatistics> GetStatisticsAsync(CancellationToken ct = default);
    
    /// <summary>导出历史</summary>
    Task<string> ExportAsync(DateTime? from = null, DateTime? to = null, CancellationToken ct = default);
    
    /// <summary>清理旧记录</summary>
    Task<int> CleanupAsync(int keepDays = 30, CancellationToken ct = default);
}
