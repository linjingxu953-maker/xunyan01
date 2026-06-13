using Microsoft.Data.Sqlite;
using DesktopMascot.Core.Enums;

namespace DesktopMascot.Core.Storage;

/// <summary>
/// SQLite 任务历史存储
/// </summary>
public class SqliteTaskHistoryStore : ITaskHistoryStore
{
    private readonly string _connectionString;

    public SqliteTaskHistoryStore(string dbPath)
    {
        _connectionString = $"Data Source={dbPath}";
    }

    private async Task<SqliteConnection> OpenConnectionAsync()
    {
        var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        return connection;
    }

    public async Task<TaskHistoryRecord> SaveTaskAsync(TaskHistoryRecord record, CancellationToken ct = default)
    {
        using var connection = await OpenConnectionAsync();
        
        var sql = @"INSERT OR REPLACE INTO Tasks (Id, Title, Input, Type, Status, Result, Error, CreatedAt, CompletedAt) 
                     VALUES (@Id, @Title, @Input, @Type, @Status, @Result, @Error, @CreatedAt, @CompletedAt)";
        
        using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("@Id", record.Id);
        cmd.Parameters.AddWithValue("@Title", record.Title);
        cmd.Parameters.AddWithValue("@Input", record.Input);
        cmd.Parameters.AddWithValue("@Type", (int)record.Type);
        cmd.Parameters.AddWithValue("@Status", (int)record.Status);
        cmd.Parameters.AddWithValue("@Result", (object?)record.Result ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Error", (object?)record.Error ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@CreatedAt", record.CreatedAt.ToString("O"));
        cmd.Parameters.AddWithValue("@CompletedAt", (object?)record.CompletedAt?.ToString("O") ?? DBNull.Value);
        
        await cmd.ExecuteNonQueryAsync();
        return record;
    }

    public async Task<TaskHistoryRecord?> UpdateTaskAsync(TaskHistoryRecord record, CancellationToken ct = default)
    {
        using var connection = await OpenConnectionAsync();
        
        var sql = @"UPDATE Tasks SET Title=@Title, Input=@Input, Type=@Type, Status=@Status, 
                     Result=@Result, Error=@Error, CompletedAt=@CompletedAt WHERE Id=@Id";
        
        using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("@Id", record.Id);
        cmd.Parameters.AddWithValue("@Title", record.Title);
        cmd.Parameters.AddWithValue("@Input", record.Input);
        cmd.Parameters.AddWithValue("@Type", (int)record.Type);
        cmd.Parameters.AddWithValue("@Status", (int)record.Status);
        cmd.Parameters.AddWithValue("@Result", (object?)record.Result ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Error", (object?)record.Error ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@CompletedAt", (object?)record.CompletedAt?.ToString("O") ?? DBNull.Value);
        
        await cmd.ExecuteNonQueryAsync();
        return record;
    }

    public async Task<TaskHistoryRecord?> GetTaskAsync(string taskId, CancellationToken ct = default)
    {
        using var connection = await OpenConnectionAsync();
        
        var sql = "SELECT * FROM Tasks WHERE Id = @Id";
        using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("@Id", taskId);
        
        using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return MapToTaskRecord(reader);
        }
        
        return null;
    }

    public async Task<bool> DeleteTaskAsync(string taskId, CancellationToken ct = default)
    {
        using var connection = await OpenConnectionAsync();
        
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "DELETE FROM Tasks WHERE Id = @Id";
        cmd.Parameters.AddWithValue("@Id", taskId);
        
        var affected = await cmd.ExecuteNonQueryAsync();
        return affected > 0;
    }

    public async Task<TaskHistorySearchResult> SearchTasksAsync(string query, int limit = 50, CancellationToken ct = default)
    {
        using var connection = await OpenConnectionAsync();
        
        var sql = @"SELECT * FROM Tasks WHERE Title LIKE @Query OR Input LIKE @Query OR Result LIKE @Query 
                     ORDER BY CreatedAt DESC LIMIT @Limit";
        
        using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("@Query", $"%{query}%");
        cmd.Parameters.AddWithValue("@Limit", limit);
        
        var records = new List<TaskHistoryRecord>();
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            records.Add(MapToTaskRecord(reader));
        }
        
        return new TaskHistorySearchResult
        {
            Records = records,
            TotalCount = records.Count,
            Query = query
        };
    }

    public async Task<List<TaskHistoryRecord>> GetRecentTasksAsync(int limit = 20, CancellationToken ct = default)
    {
        using var connection = await OpenConnectionAsync();
        
        var sql = "SELECT * FROM Tasks ORDER BY CreatedAt DESC LIMIT @Limit";
        using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("@Limit", limit);
        
        var records = new List<TaskHistoryRecord>();
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            records.Add(MapToTaskRecord(reader));
        }
        
        return records;
    }

    public async Task<ToolCallRecord> SaveToolCallAsync(ToolCallRecord record, CancellationToken ct = default)
    {
        using var connection = await OpenConnectionAsync();
        
        var sql = @"INSERT INTO ToolCalls (Id, TaskId, ToolName, Arguments, Result, Success, Error, Timestamp, DurationMs) 
                     VALUES (@Id, @TaskId, @ToolName, @Arguments, @Result, @Success, @Error, @Timestamp, @DurationMs)";
        
        using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("@Id", record.Id);
        cmd.Parameters.AddWithValue("@TaskId", record.TaskId);
        cmd.Parameters.AddWithValue("@ToolName", record.ToolName);
        cmd.Parameters.AddWithValue("@Arguments", record.Arguments);
        cmd.Parameters.AddWithValue("@Result", (object?)record.Result ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Success", record.Success ? 1 : 0);
        cmd.Parameters.AddWithValue("@Error", (object?)record.Error ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Timestamp", record.Timestamp.ToString("O"));
        cmd.Parameters.AddWithValue("@DurationMs", (object?)record.Duration?.TotalMilliseconds ?? DBNull.Value);
        
        await cmd.ExecuteNonQueryAsync();
        return record;
    }

    public async Task<List<ToolCallRecord>> GetToolCallsAsync(string taskId, CancellationToken ct = default)
    {
        using var connection = await OpenConnectionAsync();
        
        var sql = "SELECT * FROM ToolCalls WHERE TaskId = @TaskId ORDER BY Timestamp";
        using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("@TaskId", taskId);
        
        var records = new List<ToolCallRecord>();
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            records.Add(MapToToolCallRecord(reader));
        }
        
        return records;
    }

    public async Task<TaskHistoryStatistics> GetStatisticsAsync(CancellationToken ct = default)
    {
        using var connection = await OpenConnectionAsync();
        
        var sql = @"SELECT 
                        COUNT(*) as Total,
                        SUM(CASE WHEN Status = 2 THEN 1 ELSE 0 END) as Completed,
                        SUM(CASE WHEN Status = 3 THEN 1 ELSE 0 END) as Failed,
                        SUM(CASE WHEN Status = 1 THEN 1 ELSE 0 END) as Running
                     FROM Tasks";
        
        using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        
        using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return new TaskHistoryStatistics
            {
                TotalTasks = reader.GetInt32(0),
                CompletedTasks = reader.GetInt32(1),
                FailedTasks = reader.GetInt32(2),
                RunningTasks = reader.GetInt32(3)
            };
        }
        
        return new TaskHistoryStatistics();
    }

    public async Task<string> ExportAsync(DateTime? from = null, DateTime? to = null, CancellationToken ct = default)
    {
        var tasks = await GetRecentTasksAsync(10000, ct);
        
        if (from.HasValue)
            tasks = tasks.Where(t => t.CreatedAt >= from.Value).ToList();
        
        if (to.HasValue)
            tasks = tasks.Where(t => t.CreatedAt <= to.Value).ToList();
        
        return System.Text.Json.JsonSerializer.Serialize(tasks, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
    }

    public async Task<int> CleanupAsync(int keepDays = 30, CancellationToken ct = default)
    {
        using var connection = await OpenConnectionAsync();
        
        var cutoff = DateTime.UtcNow.AddDays(-keepDays).ToString("O");
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "DELETE FROM Tasks WHERE CreatedAt < @Cutoff";
        cmd.Parameters.AddWithValue("@Cutoff", cutoff);
        
        var affected = await cmd.ExecuteNonQueryAsync();
        return affected;
    }

    private static TaskHistoryRecord MapToTaskRecord(SqliteDataReader reader)
    {
        return new TaskHistoryRecord
        {
            Id = reader["Id"].ToString()!,
            Title = reader["Title"].ToString()!,
            Input = reader["Input"].ToString()!,
            Type = (TaskType)Convert.ToInt32(reader["Type"]),
            Status = (AppTaskStatus)Convert.ToInt32(reader["Status"]),
            Result = reader["Result"] as string,
            Error = reader["Error"] as string,
            CreatedAt = DateTime.Parse(reader["CreatedAt"].ToString()!),
            CompletedAt = reader["CompletedAt"] as string != null ? DateTime.Parse(reader["CompletedAt"].ToString()!) : null
        };
    }

    private static ToolCallRecord MapToToolCallRecord(SqliteDataReader reader)
    {
        var durationMs = reader["DurationMs"] as double?;
        return new ToolCallRecord
        {
            Id = reader["Id"].ToString()!,
            TaskId = reader["TaskId"].ToString()!,
            ToolName = reader["ToolName"].ToString()!,
            Arguments = reader["Arguments"].ToString()!,
            Result = reader["Result"] as string,
            Success = Convert.ToInt32(reader["Success"]) == 1,
            Error = reader["Error"] as string,
            Timestamp = DateTime.Parse(reader["Timestamp"].ToString()!),
            Duration = durationMs.HasValue ? TimeSpan.FromMilliseconds(durationMs.Value) : null
        };
    }
}
