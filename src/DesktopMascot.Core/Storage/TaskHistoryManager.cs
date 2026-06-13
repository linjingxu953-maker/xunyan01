using DesktopMascot.Core.Enums;

namespace DesktopMascot.Core.Storage;

/// <summary>
/// 任务历史管理器
/// </summary>
public class TaskHistoryManager
{
    private readonly ITaskHistoryStore _store;

    public TaskHistoryManager(ITaskHistoryStore store)
    {
        _store = store;
    }

    /// <summary>创建新任务</summary>
    public async Task<TaskHistoryRecord> CreateTaskAsync(
        string title,
        string input,
        TaskType type,
        CancellationToken ct = default)
    {
        var record = new TaskHistoryRecord
        {
            Id = Guid.NewGuid().ToString("N"),
            Title = title,
            Input = input,
            Type = type,
            Status = AppTaskStatus.Created
        };

        return await _store.SaveTaskAsync(record, ct);
    }

    /// <summary>开始任务</summary>
    public async Task<TaskHistoryRecord?> StartTaskAsync(string taskId, CancellationToken ct = default)
    {
        var record = await _store.GetTaskAsync(taskId, ct);
        if (record == null) return null;

        record.Status = AppTaskStatus.Running;
        return await _store.UpdateTaskAsync(record, ct);
    }

    /// <summary>完成任务</summary>
    public async Task<TaskHistoryRecord?> CompleteTaskAsync(
        string taskId,
        string result,
        CancellationToken ct = default)
    {
        var record = await _store.GetTaskAsync(taskId, ct);
        if (record == null) return null;

        record.Status = AppTaskStatus.Completed;
        record.Result = result;
        record.CompletedAt = DateTime.UtcNow;
        return await _store.UpdateTaskAsync(record, ct);
    }

    /// <summary>标记任务失败</summary>
    public async Task<TaskHistoryRecord?> FailTaskAsync(
        string taskId,
        string error,
        CancellationToken ct = default)
    {
        var record = await _store.GetTaskAsync(taskId, ct);
        if (record == null) return null;

        record.Status = AppTaskStatus.Failed;
        record.Error = error;
        record.CompletedAt = DateTime.UtcNow;
        return await _store.UpdateTaskAsync(record, ct);
    }

    /// <summary>添加任务事件</summary>
    public async Task<TaskHistoryRecord?> AddEventAsync(
        string taskId,
        string state,
        string message,
        int progress = -1,
        CancellationToken ct = default)
    {
        var record = await _store.GetTaskAsync(taskId, ct);
        if (record == null) return null;

        record.Events.Add(new TaskEventRecord
        {
            TaskId = taskId,
            State = state,
            Message = message,
            Progress = progress
        });

        return await _store.UpdateTaskAsync(record, ct);
    }

    /// <summary>记录工具调用</summary>
    public async Task<ToolCallRecord> LogToolCallAsync(
        string taskId,
        string toolName,
        string arguments,
        string? result,
        bool success,
        string? error = null,
        TimeSpan? duration = null,
        CancellationToken ct = default)
    {
        var record = new ToolCallRecord
        {
            TaskId = taskId,
            ToolName = toolName,
            Arguments = arguments,
            Result = result,
            Success = success,
            Error = error,
            Duration = duration
        };

        // 同时更新任务记录
        var task = await _store.GetTaskAsync(taskId, ct);
        if (task != null)
        {
            task.ToolCalls.Add(record);
            await _store.UpdateTaskAsync(task, ct);
        }

        return await _store.SaveToolCallAsync(record, ct);
    }

    /// <summary>获取任务详情</summary>
    public Task<TaskHistoryRecord?> GetTaskAsync(string taskId, CancellationToken ct = default)
    {
        return _store.GetTaskAsync(taskId, ct);
    }

    /// <summary>获取最近任务</summary>
    public Task<List<TaskHistoryRecord>> GetRecentTasksAsync(int limit = 20, CancellationToken ct = default)
    {
        return _store.GetRecentTasksAsync(limit, ct);
    }

    /// <summary>搜索任务</summary>
    public Task<TaskHistorySearchResult> SearchAsync(string query, CancellationToken ct = default)
    {
        return _store.SearchTasksAsync(query, ct: ct);
    }

    /// <summary>获取统计</summary>
    public Task<TaskHistoryStatistics> GetStatisticsAsync(CancellationToken ct = default)
    {
        return _store.GetStatisticsAsync(ct);
    }

    /// <summary>清理旧记录</summary>
    public Task<int> CleanupAsync(int keepDays = 30, CancellationToken ct = default)
    {
        return _store.CleanupAsync(keepDays, ct);
    }
}
