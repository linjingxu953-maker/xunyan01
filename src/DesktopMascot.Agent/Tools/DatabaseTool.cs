using DesktopMascot.Core.Tools;
using System.Data;
using System.Data.SQLite;
using System.Text;
using System.Text.Json;
using DesktopMascot.Agent.Models;

namespace DesktopMascot.Agent.Tools;

/// <summary>
/// 数据库操作工具 - SQLite 查询、管理、分析
/// </summary>
public class DatabaseTool : ITool
{
    private string _connectionString = "";

    public string Name => "database";
    public string Description => "数据库操作：SQLite 查询、表管理、数据分析。";
    public string ParametersSchema => """
    {
        "type": "object",
        "properties": {
            "action": { "type": "string", "enum": ["connect", "query", "tables", "schema", "insert", "update", "delete", "stats"], "description": "操作类型" },
            "db_path": { "type": "string", "description": "数据库文件路径" },
            "sql": { "type": "string", "description": "SQL 查询语句" },
            "table": { "type": "string", "description": "表名" },
            "data": { "type": "string", "description": "JSON 数据（insert模式）" }
        },
        "required": ["action"]
    }
    """;

    public async Task<ToolResult> ExecuteAsync(string arguments, CancellationToken ct = default)
    {
        try
        {
            var doc = JsonDocument.Parse(arguments);
            var root = doc.RootElement;
            var action = root.TryGetProperty("action", out var aEl) ? aEl.GetString() ?? "" : "";

            return action switch
            {
                "connect" => ConnectDatabase(root),
                "query" => await QueryAsync(root, ct),
                "tables" => await ListTablesAsync(ct),
                "schema" => await GetSchemaAsync(root, ct),
                "insert" => await InsertDataAsync(root, ct),
                "update" => await UpdateDataAsync(root, ct),
                "delete" => await DeleteDataAsync(root, ct),
                "stats" => await GetStatsAsync(ct),
                _ => Fail($"不支持的操作：{action}")
            };
        }
        catch (Exception ex)
        {
            return Fail($"数据库操作失败：{ex.Message}");
        }
    }

    private ToolResult ConnectDatabase(JsonElement root)
    {
        var dbPath = root.TryGetProperty("db_path", out var pEl) ? pEl.GetString() ?? "" : "";
        if (string.IsNullOrEmpty(dbPath)) return Fail("缺少 db_path 参数");

        _connectionString = $"Data Source={dbPath}";
        return new ToolResult { Name = Name, Success = true, Content = $"已连接数据库：{dbPath}" };
    }

    private async Task<ToolResult> QueryAsync(JsonElement root, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(_connectionString)) return Fail("请先连接数据库");

        var sql = root.TryGetProperty("sql", out var sEl) ? sEl.GetString() ?? "" : "";
        if (string.IsNullOrEmpty(sql)) return Fail("缺少 sql 参数");

        using var connection = new SQLiteConnection(_connectionString);
        await connection.OpenAsync(ct);

        using var command = new SQLiteCommand(sql, connection);
        using var reader = await command.ExecuteReaderAsync(ct);

        var sb = new StringBuilder();
        var rowCount = 0;

        // 输出列头
        for (int i = 0; i < reader.FieldCount; i++)
        {
            sb.Append(reader.GetName(i).PadRight(20));
        }
        sb.AppendLine();

        // 输出数据行
        while (await reader.ReadAsync(ct))
        {
            rowCount++;
            for (int i = 0; i < reader.FieldCount; i++)
            {
                var value = reader.IsDBNull(i) ? "NULL" : reader[i]?.ToString() ?? "";
                sb.Append(value.PadRight(20));
            }
            sb.AppendLine();
        }

        sb.AppendLine();
        sb.AppendLine($"共 {rowCount} 行");

        return new ToolResult { Name = Name, Success = true, Content = sb.ToString() };
    }

    private async Task<ToolResult> ListTablesAsync(CancellationToken ct)
    {
        if (string.IsNullOrEmpty(_connectionString)) return Fail("请先连接数据库");

        using var connection = new SQLiteConnection(_connectionString);
        await connection.OpenAsync(ct);

        using var command = new SQLiteCommand("SELECT name FROM sqlite_master WHERE type='table' ORDER BY name", connection);
        using var reader = await command.ExecuteReaderAsync(ct);

        var sb = new StringBuilder();
        sb.AppendLine("数据库表列表：");
        sb.AppendLine();

        while (await reader.ReadAsync(ct))
        {
            sb.AppendLine($"  - {reader.GetString(0)}");
        }

        return new ToolResult { Name = Name, Success = true, Content = sb.ToString() };
    }

    private async Task<ToolResult> GetSchemaAsync(JsonElement root, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(_connectionString)) return Fail("请先连接数据库");

        var table = root.TryGetProperty("table", out var tEl) ? tEl.GetString() ?? "" : "";
        if (string.IsNullOrEmpty(table)) return Fail("缺少 table 参数");

        using var connection = new SQLiteConnection(_connectionString);
        await connection.OpenAsync(ct);

        using var command = new SQLiteCommand($"PRAGMA table_info({table})", connection);
        using var reader = await command.ExecuteReaderAsync(ct);

        var sb = new StringBuilder();
        sb.AppendLine($"表结构：{table}");
        sb.AppendLine();

        while (await reader.ReadAsync(ct))
        {
            var name = reader.GetString(1);
            var type = reader.GetString(2);
            var notNull = reader.GetInt32(3) == 1 ? "NOT NULL" : "";
            var pk = reader.GetInt32(5) == 1 ? " PRIMARY KEY" : "";
            sb.AppendLine($"  {name} {type} {notNull}{pk}");
        }

        return new ToolResult { Name = Name, Success = true, Content = sb.ToString() };
    }

    private async Task<ToolResult> InsertDataAsync(JsonElement root, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(_connectionString)) return Fail("请先连接数据库");

        var table = root.TryGetProperty("table", out var tEl) ? tEl.GetString() ?? "" : "";
        var data = root.TryGetProperty("data", out var dEl) ? dEl.GetString() ?? "" : "";

        if (string.IsNullOrEmpty(table)) return Fail("缺少 table 参数");
        if (string.IsNullOrEmpty(data)) return Fail("缺少 data 参数");

        var records = JsonSerializer.Deserialize<List<Dictionary<string, JsonElement>>>(data);
        if (records == null || records.Count == 0) return Fail("无效的 JSON 数据");

        using var connection = new SQLiteConnection(_connectionString);
        await connection.OpenAsync(ct);

        var inserted = 0;
        foreach (var record in records)
        {
            var columns = string.Join(", ", record.Keys);
            var values = string.Join(", ", record.Values.Select(v => $"'{v}'"));
            var sql = $"INSERT INTO {table} ({columns}) VALUES ({values})";

            using var command = new SQLiteCommand(sql, connection);
            await command.ExecuteNonQueryAsync(ct);
            inserted++;
        }

        return new ToolResult { Name = Name, Success = true, Content = $"已插入 {inserted} 条记录到 {table}" };
    }

    private async Task<ToolResult> UpdateDataAsync(JsonElement root, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(_connectionString)) return Fail("请先连接数据库");

        var sql = root.TryGetProperty("sql", out var sEl) ? sEl.GetString() ?? "" : "";
        if (string.IsNullOrEmpty(sql)) return Fail("缺少 sql 参数");

        using var connection = new SQLiteConnection(_connectionString);
        await connection.OpenAsync(ct);

        using var command = new SQLiteCommand(sql, connection);
        var affected = await command.ExecuteNonQueryAsync(ct);

        return new ToolResult { Name = Name, Success = true, Content = $"已更新 {affected} 条记录" };
    }

    private async Task<ToolResult> DeleteDataAsync(JsonElement root, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(_connectionString)) return Fail("请先连接数据库");

        var sql = root.TryGetProperty("sql", out var sEl) ? sEl.GetString() ?? "" : "";
        if (string.IsNullOrEmpty(sql)) return Fail("缺少 sql 参数");

        using var connection = new SQLiteConnection(_connectionString);
        await connection.OpenAsync(ct);

        using var command = new SQLiteCommand(sql, connection);
        var affected = await command.ExecuteNonQueryAsync(ct);

        return new ToolResult { Name = Name, Success = true, Content = $"已删除 {affected} 条记录" };
    }

    private async Task<ToolResult> GetStatsAsync(CancellationToken ct)
    {
        if (string.IsNullOrEmpty(_connectionString)) return Fail("请先连接数据库");

        using var connection = new SQLiteConnection(_connectionString);
        await connection.OpenAsync(ct);

        var sb = new StringBuilder();
        sb.AppendLine("数据库统计");

        // 获取表数量
        using (var cmd = new SQLiteCommand("SELECT COUNT(*) FROM sqlite_master WHERE type='table'", connection))
        {
            var tableCount = Convert.ToInt32(await cmd.ExecuteScalarAsync(ct));
            sb.AppendLine($"表数量：{tableCount}");
        }

        // 获取各表行数
        using (var cmd = new SQLiteCommand("SELECT name FROM sqlite_master WHERE type='table'", connection))
        {
            using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                var tableName = reader.GetString(0);
                using (var countCmd = new SQLiteCommand($"SELECT COUNT(*) FROM {tableName}", connection))
                {
                    var count = Convert.ToInt32(await countCmd.ExecuteScalarAsync(ct));
                    sb.AppendLine($"  {tableName}：{count} 行");
                }
            }
        }

        return new ToolResult { Name = Name, Success = true, Content = sb.ToString() };
    }

    private ToolResult Fail(string error) => new() { Name = Name, Success = false, Error = error };
}
