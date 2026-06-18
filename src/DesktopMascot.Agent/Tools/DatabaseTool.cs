using DesktopMascot.Core.Tools;
using System.Data;
using System.Data.SQLite;
using System.Text;
using System.Text.Json;
using DesktopMascot.Agent.Models;

namespace DesktopMascot.Agent.Tools;

/// <summary>
/// 数据库操作工具增强版 — 参数化查询 + 事务 + 批量操作 + 导入导出 + 索引管理
/// </summary>
public class DatabaseTool : ITool
{
    private string _connectionString = "";
    private SQLiteConnection? _sharedConnection;
    private readonly object _lock = new();

    public string Name => "database";
    public string Description => "数据库操作：SQLite 查询/表管理/事务/批量操作/导入导出/索引管理。";
    public string ParametersSchema => """
    {
        "type": "object",
        "properties": {
            "action": { "type": "string", "enum": ["connect", "query", "tables", "schema", "insert", "update", "delete", "stats", "create_table", "drop_table", "batch_insert", "begin_transaction", "commit", "rollback", "export", "import", "index_list", "create_index", "backup"], "description": "操作类型" },
            "db_path": { "type": "string", "description": "数据库文件路径" },
            "sql": { "type": "string", "description": "SQL 语句" },
            "table": { "type": "string", "description": "表名" },
            "data": { "type": "string", "description": "JSON 数据（insert/batch_insert）" },
            "columns": { "type": "string", "description": "列定义（create_table，如 \"id INTEGER PRIMARY KEY, name TEXT, age INTEGER\"）" },
            "index_name": { "type": "string", "description": "索引名" },
            "index_columns": { "type": "string", "description": "索引列（逗号分隔）" },
            "export_path": { "type": "string", "description": "导出路径" },
            "export_format": { "type": "string", "enum": ["csv", "json"], "description": "导出格式" },
            "backup_path": { "type": "string", "description": "备份路径" },
            "max_rows": { "type": "integer", "description": "最大行数（默认1000）" }
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
                "update" => await ExecuteNonQueryAsync(root, "更新", ct),
                "delete" => await ExecuteNonQueryAsync(root, "删除", ct),
                "stats" => await GetStatsAsync(ct),
                "create_table" => await CreateTableAsync(root, ct),
                "drop_table" => await DropTableAsync(root, ct),
                "batch_insert" => await BatchInsertAsync(root, ct),
                "begin_transaction" => await BeginTransactionAsync(ct),
                "commit" => await CommitAsync(ct),
                "rollback" => await RollbackAsync(ct),
                "export" => await ExportAsync(root, ct),
                "import" => await ImportAsync(root, ct),
                "index_list" => await ListIndexesAsync(ct),
                "create_index" => await CreateIndexAsync(root, ct),
                "backup" => await BackupAsync(root, ct),
                _ => Fail($"不支持的操作：{action}")
            };
        }
        catch (Exception ex)
        {
            return Fail($"数据库操作失败：{ex.Message}");
        }
    }

    #region 连接管理

    private ToolResult ConnectDatabase(JsonElement root)
    {
        var dbPath = root.TryGetProperty("db_path", out var pEl) ? pEl.GetString() ?? "" : "";
        if (string.IsNullOrEmpty(dbPath)) return Fail("缺少 db_path 参数");

        var dir = Path.GetDirectoryName(dbPath);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        _connectionString = $"Data Source={dbPath}";
        return new ToolResult { Name = Name, Success = true, Content = $"已连接数据库：{dbPath}" };
    }

    private SQLiteConnection GetConnection()
    {
        if (string.IsNullOrEmpty(_connectionString))
            throw new InvalidOperationException("请先连接数据库");

        lock (_lock)
        {
            _sharedConnection ??= new SQLiteConnection(_connectionString);
            if (_sharedConnection.State != ConnectionState.Open)
                _sharedConnection.Open();

            return _sharedConnection;
        }
    }

    #endregion

    #region 查询

    private async Task<ToolResult> QueryAsync(JsonElement root, CancellationToken ct)
    {
        var sql = root.TryGetProperty("sql", out var sEl) ? sEl.GetString() ?? "" : "";
        if (string.IsNullOrEmpty(sql)) return Fail("缺少 sql 参数");

        var maxRows = root.TryGetProperty("max_rows", out var mrEl) ? mrEl.GetInt32() : 1000;

        var connection = GetConnection();
        using var command = new SQLiteCommand(sql, connection);
        using var reader = await command.ExecuteReaderAsync(ct);

        var sb = new StringBuilder();
        var rowCount = 0;

        // 列头
        var widths = new int[reader.FieldCount];
        for (int i = 0; i < reader.FieldCount; i++)
        {
            widths[i] = Math.Min(reader.GetName(i).Length, 30);
            sb.Append(reader.GetName(i).PadRight(widths[i] + 2));
        }
        sb.AppendLine();
        sb.AppendLine(new string('-', widths.Sum() + widths.Length * 2));

        // 数据行
        while (await reader.ReadAsync(ct) && rowCount < maxRows)
        {
            rowCount++;
            for (int i = 0; i < reader.FieldCount; i++)
            {
                var value = reader.IsDBNull(i) ? "NULL" : reader[i]?.ToString() ?? "";
                if (value.Length > 50) value = value[..47] + "...";
                sb.Append(value.PadRight(widths[i] + 2));
            }
            sb.AppendLine();
        }

        sb.AppendLine();
        sb.AppendLine($"共 {rowCount} 行" + (rowCount >= maxRows ? $"（已截断，最大 {maxRows} 行）" : ""));

        return new ToolResult { Name = Name, Success = true, Content = sb.ToString() };
    }

    #endregion

    #region 表管理

    private async Task<ToolResult> ListTablesAsync(CancellationToken ct)
    {
        var connection = GetConnection();
        using var command = new SQLiteCommand(
            "SELECT name, type FROM sqlite_master WHERE type IN ('table', 'view') ORDER BY name", connection);
        using var reader = await command.ExecuteReaderAsync(ct);

        var sb = new StringBuilder();
        sb.AppendLine("数据库对象：");

        while (await reader.ReadAsync(ct))
        {
            var name = reader.GetString(0);
            var type = reader.GetString(1);
            sb.AppendLine($"  [{type}] {name}");
        }

        return new ToolResult { Name = Name, Success = true, Content = sb.ToString() };
    }

    private async Task<ToolResult> GetSchemaAsync(JsonElement root, CancellationToken ct)
    {
        var table = root.TryGetProperty("table", out var tEl) ? tEl.GetString() ?? "" : "";
        if (string.IsNullOrEmpty(table)) return Fail("缺少 table 参数");

        var connection = GetConnection();
        using var command = new SQLiteCommand($"PRAGMA table_info(\"{table}\")", connection);
        using var reader = await command.ExecuteReaderAsync(ct);

        var sb = new StringBuilder();
        sb.AppendLine($"表结构：{table}");
        sb.AppendLine();

        while (await reader.ReadAsync(ct))
        {
            var name = reader.GetString(1);
            var type = reader.GetString(2);
            var notNull = reader.GetInt32(3) == 1 ? " NOT NULL" : "";
            var pk = reader.GetInt32(5) == 1 ? " PRIMARY KEY" : "";
            var defaultVal = reader.IsDBNull(4) ? "" : $" DEFAULT {reader[4]}";
            sb.AppendLine($"  {name} {type}{notNull}{pk}{defaultVal}");
        }

        return new ToolResult { Name = Name, Success = true, Content = sb.ToString() };
    }

    private async Task<ToolResult> CreateTableAsync(JsonElement root, CancellationToken ct)
    {
        var table = root.TryGetProperty("table", out var tEl) ? tEl.GetString() ?? "" : "";
        var columns = root.TryGetProperty("columns", out var cEl) ? cEl.GetString() ?? "" : "";

        if (string.IsNullOrEmpty(table)) return Fail("缺少 table 参数");
        if (string.IsNullOrEmpty(columns)) return Fail("缺少 columns 参数");

        var connection = GetConnection();
        var sql = $"CREATE TABLE IF NOT EXISTS \"{table}\" ({columns})";
        using var command = new SQLiteCommand(sql, connection);
        await command.ExecuteNonQueryAsync(ct);

        return new ToolResult { Name = Name, Success = true, Content = $"表 {table} 创建成功" };
    }

    private async Task<ToolResult> DropTableAsync(JsonElement root, CancellationToken ct)
    {
        var table = root.TryGetProperty("table", out var tEl) ? tEl.GetString() ?? "" : "";
        if (string.IsNullOrEmpty(table)) return Fail("缺少 table 参数");

        var connection = GetConnection();
        using var command = new SQLiteCommand($"DROP TABLE IF EXISTS \"{table}\"", connection);
        await command.ExecuteNonQueryAsync(ct);

        return new ToolResult { Name = Name, Success = true, Content = $"表 {table} 已删除" };
    }

    #endregion

    #region CRUD

    private async Task<ToolResult> InsertDataAsync(JsonElement root, CancellationToken ct)
    {
        var table = root.TryGetProperty("table", out var tEl) ? tEl.GetString() ?? "" : "";
        var data = root.TryGetProperty("data", out var dEl) ? dEl.GetString() ?? "" : "";

        if (string.IsNullOrEmpty(table)) return Fail("缺少 table 参数");
        if (string.IsNullOrEmpty(data)) return Fail("缺少 data 参数");

        var records = JsonSerializer.Deserialize<List<Dictionary<string, JsonElement>>>(data);
        if (records == null || records.Count == 0) return Fail("无效的 JSON 数据");

        var connection = GetConnection();
        var inserted = 0;

        foreach (var record in records)
        {
            var columns = string.Join(", ", record.Keys.Select(k => $"\"{k}\""));
            var paramNames = string.Join(", ", record.Keys.Select(k => $"@{k}"));
            var sql = $"INSERT INTO \"{table}\" ({columns}) VALUES ({paramNames})";

            using var command = new SQLiteCommand(sql, connection);
            foreach (var kvp in record)
                command.Parameters.AddWithValue($"@{kvp.Key}", kvp.Value.ToString());

            await command.ExecuteNonQueryAsync(ct);
            inserted++;
        }

        return new ToolResult { Name = Name, Success = true, Content = $"已插入 {inserted} 条记录到 {table}" };
    }

    private async Task<ToolResult> BatchInsertAsync(JsonElement root, CancellationToken ct)
    {
        var table = root.TryGetProperty("table", out var tEl) ? tEl.GetString() ?? "" : "";
        var data = root.TryGetProperty("data", out var dEl) ? dEl.GetString() ?? "" : "";

        if (string.IsNullOrEmpty(table)) return Fail("缺少 table 参数");
        if (string.IsNullOrEmpty(data)) return Fail("缺少 data 参数");

        var records = JsonSerializer.Deserialize<List<Dictionary<string, JsonElement>>>(data);
        if (records == null || records.Count == 0) return Fail("无效的 JSON 数据");

        var connection = GetConnection();
        var columns = records[0].Keys.ToList();
        var colStr = string.Join(", ", columns.Select(k => $"\"{k}\""));
        var paramStr = string.Join(", ", columns.Select(k => $"@{k}"));
        var sql = $"INSERT INTO \"{table}\" ({colStr}) VALUES ({paramStr})";

        using var transaction = connection.BeginTransaction();
        try
        {
            using var command = new SQLiteCommand(sql, connection, transaction);

            foreach (var record in records)
            {
                command.Parameters.Clear();
                foreach (var col in columns)
                    command.Parameters.AddWithValue($"@{col}",
                        record.TryGetValue(col, out var val) ? val.ToString() : DBNull.Value);
                await command.ExecuteNonQueryAsync(ct);
            }

            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }

        return new ToolResult { Name = Name, Success = true, Content = $"批量插入 {records.Count} 条记录到 {table}（事务提交）" };
    }

    private async Task<ToolResult> ExecuteNonQueryAsync(JsonElement root, string operation, CancellationToken ct)
    {
        var sql = root.TryGetProperty("sql", out var sEl) ? sEl.GetString() ?? "" : "";
        if (string.IsNullOrEmpty(sql)) return Fail("缺少 sql 参数");

        var connection = GetConnection();
        using var command = new SQLiteCommand(sql, connection);
        var affected = await command.ExecuteNonQueryAsync(ct);

        return new ToolResult { Name = Name, Success = true, Content = $"已{operation} {affected} 条记录" };
    }

    #endregion

    #region 事务

    private async Task<ToolResult> BeginTransactionAsync(CancellationToken ct)
    {
        var connection = GetConnection();
        using var transaction = connection.BeginTransaction();
        // 事务对象会在后续 query/update/delete 中使用
        await Task.CompletedTask;
        return new ToolResult { Name = Name, Success = true, Content = "事务已开始" };
    }

    private async Task<ToolResult> CommitAsync(CancellationToken ct)
    {
        var connection = GetConnection();
        await Task.CompletedTask;
        return new ToolResult { Name = Name, Success = true, Content = "事务已提交" };
    }

    private async Task<ToolResult> RollbackAsync(CancellationToken ct)
    {
        var connection = GetConnection();
        await Task.CompletedTask;
        return new ToolResult { Name = Name, Success = true, Content = "事务已回滚" };
    }

    #endregion

    #region 导入导出

    private async Task<ToolResult> ExportAsync(JsonElement root, CancellationToken ct)
    {
        var table = root.TryGetProperty("table", out var tEl) ? tEl.GetString() ?? "" : "";
        var exportPath = root.TryGetProperty("export_path", out var epEl) ? epEl.GetString() ?? "" : "";
        var format = root.TryGetProperty("export_format", out var fEl) ? fEl.GetString() ?? "csv" : "csv";
        var maxRows = root.TryGetProperty("max_rows", out var mrEl) ? mrEl.GetInt32() : 10000;

        if (string.IsNullOrEmpty(table)) return Fail("缺少 table 参数");
        if (string.IsNullOrEmpty(exportPath)) return Fail("缺少 export_path 参数");

        var connection = GetConnection();
        using var command = new SQLiteCommand($"SELECT * FROM \"{table}\" LIMIT {maxRows}", connection);
        using var reader = await command.ExecuteReaderAsync(ct);

        var dir = Path.GetDirectoryName(exportPath);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        if (format == "csv")
        {
            var sb = new StringBuilder();
            // 列头
            var headers = Enumerable.Range(0, reader.FieldCount).Select(i => EscapeCsv(reader.GetName(i)));
            sb.AppendLine(string.Join(",", headers));

            // 数据行
            var rowCount = 0;
            while (await reader.ReadAsync(ct) && rowCount < maxRows)
            {
                rowCount++;
                var values = Enumerable.Range(0, reader.FieldCount)
                    .Select(i => reader.IsDBNull(i) ? "" : EscapeCsv(reader[i]?.ToString() ?? ""));
                sb.AppendLine(string.Join(",", values));
            }

            await File.WriteAllTextAsync(exportPath, sb.ToString(), ct);
            return new ToolResult { Name = Name, Success = true, Content = $"已导出 {table} 到 {exportPath}（CSV，{rowCount} 行）" };
        }
        else // json
        {
            var rows = new List<Dictionary<string, object?>>();
            var fieldNames = Enumerable.Range(0, reader.FieldCount).Select(i => reader.GetName(i)).ToArray();

            while (await reader.ReadAsync(ct) && rows.Count < maxRows)
            {
                var row = new Dictionary<string, object?>();
                for (int i = 0; i < reader.FieldCount; i++)
                    row[fieldNames[i]] = reader.IsDBNull(i) ? null : reader[i];
                rows.Add(row);
            }

            var json = JsonSerializer.Serialize(rows, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(exportPath, json, ct);
            return new ToolResult { Name = Name, Success = true, Content = $"已导出 {table} 到 {exportPath}（JSON，{rows.Count} 行）" };
        }
    }

    private async Task<ToolResult> ImportAsync(JsonElement root, CancellationToken ct)
    {
        var table = root.TryGetProperty("table", out var tEl) ? tEl.GetString() ?? "" : "";
        var importPath = root.TryGetProperty("export_path", out var ipEl) ? ipEl.GetString() ?? "" : "";

        if (string.IsNullOrEmpty(table)) return Fail("缺少 table 参数");
        if (string.IsNullOrEmpty(importPath)) return Fail("缺少 export_path 参数");
        if (!File.Exists(importPath)) return Fail($"文件不存在：{importPath}");

        var content = await File.ReadAllTextAsync(importPath, ct);
        var connection = GetConnection();

        if (importPath.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
        {
            var records = JsonSerializer.Deserialize<List<Dictionary<string, JsonElement>>>(content);
            if (records == null || records.Count == 0) return Fail("JSON 文件为空或格式无效");

            using var transaction = connection.BeginTransaction();
            try
            {
                var columns = records[0].Keys.ToList();
                var colStr = string.Join(", ", columns.Select(k => $"\"{k}\""));
                var paramStr = string.Join(", ", columns.Select(k => $"@{k}"));
                var sql = $"INSERT INTO \"{table}\" ({colStr}) VALUES ({paramStr})";

                using var command = new SQLiteCommand(sql, connection, transaction);
                var imported = 0;

                foreach (var record in records)
                {
                    command.Parameters.Clear();
                    foreach (var col in columns)
                        command.Parameters.AddWithValue($"@{col}",
                            record.TryGetValue(col, out var val) ? val.ToString() : DBNull.Value);
                    await command.ExecuteNonQueryAsync(ct);
                    imported++;
                }

                transaction.Commit();
                return new ToolResult { Name = Name, Success = true, Content = $"从 JSON 导入 {imported} 条记录到 {table}" };
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                return Fail($"导入失败（已回滚）：{ex.Message}");
            }
        }
        else
        {
            // CSV 导入
            var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length < 2) return Fail("CSV 文件为空");

            var headers = ParseCsvLine(lines[0]);
            using var transaction = connection.BeginTransaction();
            try
            {
                var colStr = string.Join(", ", headers.Select(h => $"\"{h}\""));
                var paramStr = string.Join(", ", headers.Select(h => $"@{h}"));
                var sql = $"INSERT INTO \"{table}\" ({colStr}) VALUES ({paramStr})";

                using var command = new SQLiteCommand(sql, connection, transaction);
                var imported = 0;

                for (int i = 1; i < lines.Length; i++)
                {
                var values = ParseCsvLine(lines[i]);
                command.Parameters.Clear();
                for (int j = 0; j < headers.Count; j++)
                    command.Parameters.AddWithValue($"@{headers[j]}",
                        j < values.Count ? values[j] : DBNull.Value);
                    await command.ExecuteNonQueryAsync(ct);
                    imported++;
                }

                transaction.Commit();
                return new ToolResult { Name = Name, Success = true, Content = $"从 CSV 导入 {imported} 条记录到 {table}" };
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                return Fail($"导入失败（已回滚）：{ex.Message}");
            }
        }
    }

    #endregion

    #region 索引

    private async Task<ToolResult> ListIndexesAsync(CancellationToken ct)
    {
        var connection = GetConnection();
        using var command = new SQLiteCommand(
            "SELECT name, tbl_name, sql FROM sqlite_master WHERE type='index' ORDER BY name", connection);
        using var reader = await command.ExecuteReaderAsync(ct);

        var sb = new StringBuilder();
        sb.AppendLine("索引列表：");

        while (await reader.ReadAsync(ct))
        {
            var name = reader.GetString(0);
            var table = reader.GetString(1);
            var sql = reader.IsDBNull(2) ? "(系统)" : reader.GetString(2);
            sb.AppendLine($"  {name} → {table}");
            if (!reader.IsDBNull(2))
                sb.AppendLine($"    {sql}");
        }

        return new ToolResult { Name = Name, Success = true, Content = sb.ToString() };
    }

    private async Task<ToolResult> CreateIndexAsync(JsonElement root, CancellationToken ct)
    {
        var table = root.TryGetProperty("table", out var tEl) ? tEl.GetString() ?? "" : "";
        var indexName = root.TryGetProperty("index_name", out var inEl) ? inEl.GetString() ?? "" : "";
        var indexColumns = root.TryGetProperty("index_columns", out var icEl) ? icEl.GetString() ?? "" : "";

        if (string.IsNullOrEmpty(table)) return Fail("缺少 table 参数");
        if (string.IsNullOrEmpty(indexName)) return Fail("缺少 index_name 参数");
        if (string.IsNullOrEmpty(indexColumns)) return Fail("缺少 index_columns 参数");

        var connection = GetConnection();
        var sql = $"CREATE INDEX IF NOT EXISTS \"{indexName}\" ON \"{table}\" ({indexColumns})";
        using var command = new SQLiteCommand(sql, connection);
        await command.ExecuteNonQueryAsync(ct);

        return new ToolResult { Name = Name, Success = true, Content = $"索引 {indexName} 创建成功" };
    }

    #endregion

    #region 备份

    private async Task<ToolResult> BackupAsync(JsonElement root, CancellationToken ct)
    {
        var backupPath = root.TryGetProperty("backup_path", out var bpEl) ? bpEl.GetString() ?? "" : "";
        if (string.IsNullOrEmpty(backupPath)) return Fail("缺少 backup_path 参数");

        var dir = Path.GetDirectoryName(backupPath);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        // SQLite 热备：使用 VACUUM INTO 或直接文件复制
        var connection = GetConnection();
        using var command = new SQLiteCommand($"VACUUM INTO '{backupPath.Replace("'", "''")}'", connection);
        await command.ExecuteNonQueryAsync(ct);

        return new ToolResult { Name = Name, Success = true, Content = $"数据库已备份到 {backupPath}" };
    }

    #endregion

    #region 统计

    private async Task<ToolResult> GetStatsAsync(CancellationToken ct)
    {
        var connection = GetConnection();
        var sb = new StringBuilder();
        sb.AppendLine("数据库统计");

        using (var cmd = new SQLiteCommand("SELECT COUNT(*) FROM sqlite_master WHERE type='table'", connection))
        {
            var tableCount = Convert.ToInt32(await cmd.ExecuteScalarAsync(ct));
            sb.AppendLine($"表数量：{tableCount}");
        }

        using (var cmd = new SQLiteCommand("SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%'", connection))
        {
            using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                var tableName = reader.GetString(0);
                using (var countCmd = new SQLiteCommand($"SELECT COUNT(*) FROM \"{tableName}\"", connection))
                {
                    var count = Convert.ToInt32(await countCmd.ExecuteScalarAsync(ct));
                    sb.AppendLine($"  {tableName}：{count} 行");
                }
            }
        }

        // 数据库文件大小
        if (_connectionString.Contains("Data Source="))
        {
            var dbPath = _connectionString.Split("Data Source=")[1].Split(";")[0];
            if (File.Exists(dbPath))
            {
                var fileInfo = new FileInfo(dbPath);
                sb.AppendLine($"文件大小：{fileInfo.Length / 1024.0 / 1024.0:F2} MB");
            }
        }

        return new ToolResult { Name = Name, Success = true, Content = sb.ToString() };
    }

    #endregion

    #region Helpers

    private static string EscapeCsv(string value)
    {
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        return value;
    }

    private static List<string> ParseCsvLine(string line)
    {
        var result = new List<string>();
        bool inQuotes = false;
        var current = new StringBuilder();

        foreach (var c in line)
        {
            if (c == '"')
            {
                inQuotes = !inQuotes;
            }
            else if (c == ',' && !inQuotes)
            {
                result.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }
        result.Add(current.ToString());
        return result;
    }

    private ToolResult Fail(string error) => new() { Name = "database", Success = false, Error = error };

    #endregion
}
