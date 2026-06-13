using System.Linq;
using System.Text.Json;
using DesktopMascot.Core.Enums;

namespace DesktopMascot.Core.Storage;

/// <summary>
/// 文件任务历史存储
/// </summary>
public class FileTaskHistoryStore : ITaskHistoryStore
{
    private readonly string _storageDirectory;
    private readonly object _lock = new();
    private const string TasksFileName = "tasks.json";
    private const string ToolCallsFileName = "tool_calls.json";

    public FileTaskHistoryStore(string? storageDirectory = null)
    {
        _storageDirectory = storageDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "DesktopMascot",
            "history");
        Directory.CreateDirectory(_storageDirectory);
    }

    private string GetTasksFilePath() => Path.Combine(_storageDirectory, TasksFileName);
    private string GetToolCallsFilePath() => Path.Combine(_storageDirectory, ToolCallsFileName);

    private List<TaskHistoryRecord> LoadAllTasks()
    {
        var filePath = GetTasksFilePath();

        if (!File.Exists(filePath))
            return new List<TaskHistoryRecord>();

        lock (_lock)
        {
            var json = File.ReadAllText(filePath);
            return JsonSerializer.Deserialize<List<TaskHistoryRecord>>(json) ?? new List<TaskHistoryRecord>();
        }
    }

    private Task SaveAllTasksAsync(List<TaskHistoryRecord> records)
    {
        var filePath = GetTasksFilePath();
        var json = JsonSerializer.Serialize(records, new JsonSerializerOptions { WriteIndented = true });

        lock (_lock)
        {
            File.WriteAllText(filePath, json);
        }

        return Task.CompletedTask;
    }

    private List<ToolCallRecord> LoadAllToolCalls()
    {
        var filePath = GetToolCallsFilePath();
        
        if (!File.Exists(filePath))
            return new List<ToolCallRecord>();
        
        lock (_lock)
        {
            var json = File.ReadAllText(filePath);
            return JsonSerializer.Deserialize<List<ToolCallRecord>>(json) ?? new List<ToolCallRecord>();
        }
    }

    private async Task SaveAllToolCallsAsync(List<ToolCallRecord> records)
    {
        var filePath = GetToolCallsFilePath();
        var json = JsonSerializer.Serialize(records, new JsonSerializerOptions { WriteIndented = true });
        
        lock (_lock)
        {
            File.WriteAllText(filePath, json);
        }
        
        await Task.CompletedTask;
    }

    public async Task<TaskHistoryRecord> SaveTaskAsync(TaskHistoryRecord record, CancellationToken ct = default)
    {
        var records = LoadAllTasks();
        
        var existingIndex = records.FindIndex(r => r.Id == record.Id);
        if (existingIndex >= 0)
        {
            records[existingIndex] = record;
        }
        else
        {
            records.Add(record);
        }
        
        await SaveAllTasksAsync(records);
        return record;
    }

    public async Task<TaskHistoryRecord?> UpdateTaskAsync(TaskHistoryRecord record, CancellationToken ct = default)
    {
        var records = LoadAllTasks();
        var index = records.FindIndex(r => r.Id == record.Id);
        
        if (index < 0)
            return null;
        
        records[index] = record;
        await SaveAllTasksAsync(records);
        return record;
    }

    public Task<TaskHistoryRecord?> GetTaskAsync(string taskId, CancellationToken ct = default)
    {
        var records = LoadAllTasks();
        return Task.FromResult(records.FirstOrDefault(r => r.Id == taskId));
    }

    public async Task<bool> DeleteTaskAsync(string taskId, CancellationToken ct = default)
    {
        var records = LoadAllTasks();
        var removed = records.RemoveAll(r => r.Id == taskId);
        
        if (removed > 0)
        {
            await SaveAllTasksAsync(records);
            return true;
        }
        
        return false;
    }

    public Task<TaskHistorySearchResult> SearchTasksAsync(string query, int limit = 50, CancellationToken ct = default)
    {
        var records = LoadAllTasks();
        var queryLower = query.ToLower();

        var results = records
            .Where(r =>
                r.Title.ToLower().Contains(queryLower) ||
                r.Input.ToLower().Contains(queryLower) ||
                (r.Result != null && r.Result.ToLower().Contains(queryLower)))
            .OrderByDescending(r => r.CreatedAt)
            .Take(limit)
            .ToList();

        return Task.FromResult(new TaskHistorySearchResult
        {
            Records = results,
            TotalCount = results.Count,
            Query = query
        });
    }

    public Task<List<TaskHistoryRecord>> GetRecentTasksAsync(int limit = 20, CancellationToken ct = default)
    {
        var records = LoadAllTasks();
        return Task.FromResult(records
            .OrderByDescending(r => r.CreatedAt)
            .Take(limit)
            .ToList());
    }

    public async Task<ToolCallRecord> SaveToolCallAsync(ToolCallRecord record, CancellationToken ct = default)
    {
        var records = LoadAllToolCalls();
        records.Add(record);
        await SaveAllToolCallsAsync(records);
        return record;
    }

    public Task<List<ToolCallRecord>> GetToolCallsAsync(string taskId, CancellationToken ct = default)
    {
        var records = LoadAllToolCalls();
        return Task.FromResult(records
            .Where(r => r.TaskId == taskId)
            .OrderBy(r => r.Timestamp)
            .ToList());
    }

    public Task<TaskHistoryStatistics> GetStatisticsAsync(CancellationToken ct = default)
    {
        var tasks = LoadAllTasks();
        var toolCalls = LoadAllToolCalls();

        var completedTasks = tasks.Where(t => t.Status == AppTaskStatus.Completed).ToList();
        var failedTasks = tasks.Where(t => t.Status == AppTaskStatus.Failed).ToList();
        var runningTasks = tasks.Where(t => t.Status == AppTaskStatus.Running).ToList();

        var avgDuration = completedTasks
            .Where(t => t.Duration.HasValue)
            .Select(t => t.Duration!.Value.TotalSeconds)
            .DefaultIfEmpty(0)
            .Average();

        var toolUsageCounts = toolCalls
            .GroupBy(tc => tc.ToolName)
            .ToDictionary(g => g.Key, g => g.Count());

        var taskTypeCounts = tasks
            .GroupBy(t => t.Type)
            .ToDictionary(g => g.Key, g => g.Count());

        return Task.FromResult(new TaskHistoryStatistics
        {
            TotalTasks = tasks.Count,
            CompletedTasks = completedTasks.Count,
            FailedTasks = failedTasks.Count,
            RunningTasks = runningTasks.Count,
            AverageDurationSeconds = avgDuration,
            TotalToolCalls = toolCalls.Count,
            ToolUsageCounts = toolUsageCounts,
            TaskTypeCounts = taskTypeCounts
        });
    }

    public Task<string> ExportAsync(DateTime? from = null, DateTime? to = null, CancellationToken ct = default)
    {
        var tasks = LoadAllTasks();

        if (from.HasValue)
            tasks = tasks.Where(t => t.CreatedAt >= from.Value).ToList();

        if (to.HasValue)
            tasks = tasks.Where(t => t.CreatedAt <= to.Value).ToList();

        return Task.FromResult(JsonSerializer.Serialize(tasks, new JsonSerializerOptions { WriteIndented = true }));
    }

    public async Task<int> CleanupAsync(int keepDays = 30, CancellationToken ct = default)
    {
        var tasks = LoadAllTasks();
        var cutoff = DateTime.UtcNow.AddDays(-keepDays);

        var removed = tasks.RemoveAll(t => t.CreatedAt < cutoff);
        
        if (removed > 0)
        {
            await SaveAllTasksAsync(tasks);
        }
        
        return removed;
    }
}
