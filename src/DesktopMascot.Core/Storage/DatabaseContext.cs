using Microsoft.Data.Sqlite;

namespace DesktopMascot.Core.Storage;

/// <summary>
/// 数据库上下文
/// </summary>
public class DatabaseContext : IDisposable
{
    private readonly SqliteConnection _connection;
    private bool _disposed;

    public DatabaseContext(string connectionString)
    {
        _connection = new SqliteConnection(connectionString);
    }

    public SqliteConnection Connection => _connection;

    public async Task OpenAsync()
    {
        if (_connection.State != System.Data.ConnectionState.Open)
        {
            await _connection.OpenAsync();
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _connection?.Dispose();
            _disposed = true;
        }
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// 数据库迁移管理器
/// </summary>
public class DatabaseMigrator
{
    private readonly DatabaseContext _context;

    public DatabaseMigrator(DatabaseContext context)
    {
        _context = context;
    }

    public async Task MigrateAsync()
    {
        await _context.OpenAsync();
        var connection = _context.Connection;

        // 创建版本表
        var createVersionTable = @"
            CREATE TABLE IF NOT EXISTS __MigrationHistory (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Version INTEGER NOT NULL,
                Name TEXT NOT NULL,
                AppliedAt TEXT NOT NULL
            )";
        
        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = createVersionTable;
            await cmd.ExecuteNonQueryAsync();
        }

        // 获取当前版本
        var currentVersion = await GetCurrentVersionAsync();
        
        // 执行迁移
        if (currentVersion < 1)
        {
            await ApplyMigration1_Async();
        }
        if (currentVersion < 2)
        {
            await ApplyMigration2_Async();
        }
    }

    private async Task<int> GetCurrentVersionAsync()
    {
        using var cmd = _context.Connection.CreateCommand();
        cmd.CommandText = "SELECT COALESCE(MAX(Version), 0) FROM __MigrationHistory";
        var result = await cmd.ExecuteScalarAsync();
        return Convert.ToInt32(result);
    }

    private async Task ApplyMigration1_Async()
    {
        var sql = @"
            CREATE TABLE IF NOT EXISTS Tasks (
                Id TEXT PRIMARY KEY,
                Title TEXT NOT NULL,
                Input TEXT NOT NULL,
                Type INTEGER NOT NULL,
                Status INTEGER NOT NULL,
                Result TEXT,
                Error TEXT,
                CreatedAt TEXT NOT NULL,
                CompletedAt TEXT
            );

            CREATE TABLE IF NOT EXISTS TaskEvents (
                Id TEXT PRIMARY KEY,
                TaskId TEXT NOT NULL,
                State TEXT NOT NULL,
                Message TEXT NOT NULL,
                Progress INTEGER DEFAULT -1,
                Timestamp TEXT NOT NULL,
                FOREIGN KEY (TaskId) REFERENCES Tasks(Id)
            );

            CREATE TABLE IF NOT EXISTS ToolCalls (
                Id TEXT PRIMARY KEY,
                TaskId TEXT NOT NULL,
                ToolName TEXT NOT NULL,
                Arguments TEXT NOT NULL,
                Result TEXT,
                Success INTEGER NOT NULL,
                Error TEXT,
                Timestamp TEXT NOT NULL,
                DurationMs INTEGER,
                FOREIGN KEY (TaskId) REFERENCES Tasks(Id)
            );

            CREATE INDEX IX_TaskEvents_TaskId ON TaskEvents(TaskId);
            CREATE INDEX IX_ToolCalls_TaskId ON ToolCalls(TaskId);
            CREATE INDEX IX_Tasks_CreatedAt ON Tasks(CreatedAt);
            CREATE INDEX IX_Tasks_Status ON Tasks(Status);";

        await ExecuteSqlAsync(sql);
        await RecordMigrationAsync(1, "Initial Tables");
    }

    private async Task ApplyMigration2_Async()
    {
        var sql = @"
            CREATE TABLE IF NOT EXISTS Memories (
                Id TEXT PRIMARY KEY,
                Type INTEGER NOT NULL,
                Key TEXT NOT NULL,
                Content TEXT NOT NULL,
                Source TEXT,
                TaskId TEXT,
                IsConfirmed INTEGER NOT NULL DEFAULT 0,
                CreatedAt TEXT NOT NULL,
                UpdatedAt TEXT NOT NULL,
                ExpiresAt TEXT
            );

            CREATE TABLE IF NOT EXISTS AuditLogs (
                Id TEXT PRIMARY KEY,
                TaskId TEXT,
                Operation TEXT NOT NULL,
                Target TEXT NOT NULL,
                Level INTEGER NOT NULL,
                Decision INTEGER NOT NULL,
                Details TEXT,
                Timestamp TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS Settings (
                Key TEXT PRIMARY KEY,
                Value TEXT NOT NULL,
                UpdatedAt TEXT NOT NULL
            );

            CREATE INDEX IX_Memories_Type ON Memories(Type);
            CREATE INDEX IX_Memories_Key ON Memories(Key);
            CREATE INDEX IX_AuditLogs_Timestamp ON AuditLogs(Timestamp);
            CREATE INDEX IX_AuditLogs_TaskId ON AuditLogs(TaskId);";

        await ExecuteSqlAsync(sql);
        await RecordMigrationAsync(2, "Memories, AuditLogs, Settings");
    }

    private async Task ExecuteSqlAsync(string sql)
    {
        using var cmd = _context.Connection.CreateCommand();
        cmd.CommandText = sql;
        await cmd.ExecuteNonQueryAsync();
    }

    private async Task RecordMigrationAsync(int version, string name)
    {
        using var cmd = _context.Connection.CreateCommand();
        cmd.CommandText = "INSERT INTO __MigrationHistory (Version, Name, AppliedAt) VALUES (@v, @n, @a)";
        cmd.Parameters.AddWithValue("@v", version);
        cmd.Parameters.AddWithValue("@n", name);
        cmd.Parameters.AddWithValue("@a", DateTime.UtcNow.ToString("O"));
        await cmd.ExecuteNonQueryAsync();
    }
}
