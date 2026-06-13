using System.Text.Json;

namespace DesktopMascot.Core.Security;

/// <summary>
/// 审计日志存储接口
/// </summary>
public interface IAuditLogStore
{
    Task SaveAsync(AuditLogEntry entry, CancellationToken ct = default);
    Task<List<AuditLogEntry>> GetLogsAsync(int limit = 100, CancellationToken ct = default);
    Task<List<AuditLogEntry>> GetLogsByTaskAsync(string taskId, CancellationToken ct = default);
    Task<int> GetTotalCountAsync(CancellationToken ct = default);
}

/// <summary>
/// 文件审计日志存储
/// </summary>
public class FileAuditLogStore : IAuditLogStore
{
    private readonly string _logDirectory;
    private readonly object _lock = new();
    private const string LogFileName = "audit_log.jsonl";

    public FileAuditLogStore(string? logDirectory = null)
    {
        _logDirectory = logDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "DesktopMascot",
            "logs");
        Directory.CreateDirectory(_logDirectory);
    }

    public async Task SaveAsync(AuditLogEntry entry, CancellationToken ct = default)
    {
        var filePath = Path.Combine(_logDirectory, LogFileName);
        var json = JsonSerializer.Serialize(entry);
        
        lock (_lock)
        {
            File.AppendAllText(filePath, json + Environment.NewLine);
        }
        
        await Task.CompletedTask;
    }

    public async Task<List<AuditLogEntry>> GetLogsAsync(int limit = 100, CancellationToken ct = default)
    {
        var filePath = Path.Combine(_logDirectory, LogFileName);
        
        if (!File.Exists(filePath))
            return new List<AuditLogEntry>();
        
        var entries = new List<AuditLogEntry>();
        
        lock (_lock)
        {
            var lines = File.ReadAllLines(filePath);
            foreach (var line in lines.Reverse())
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                
                try
                {
                    var entry = JsonSerializer.Deserialize<AuditLogEntry>(line);
                    if (entry != null)
                    {
                        entries.Add(entry);
                        if (entries.Count >= limit) break;
                    }
                }
                catch
                {
                    // 跳过无效行
                }
            }
        }
        
        return await Task.FromResult(entries);
    }

    public async Task<List<AuditLogEntry>> GetLogsByTaskAsync(string taskId, CancellationToken ct = default)
    {
        var allLogs = await GetLogsAsync(10000, ct);
        return allLogs.Where(l => l.TaskId == taskId).ToList();
    }

    public Task<int> GetTotalCountAsync(CancellationToken ct = default)
    {
        var filePath = Path.Combine(_logDirectory, LogFileName);
        
        if (!File.Exists(filePath))
            return Task.FromResult(0);
        
        lock (_lock)
        {
            var lineCount = File.ReadLines(filePath).Count();
            return Task.FromResult(lineCount);
        }
    }
}
