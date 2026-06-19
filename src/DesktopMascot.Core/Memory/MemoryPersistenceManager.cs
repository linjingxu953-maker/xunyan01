using System.Text;
using System.Text.Json;

namespace DesktopMascot.Core.Memory;

/// <summary>
/// 记忆持久化管理器 — 备份/恢复/清理/迁移/分类统计
/// </summary>
public class MemoryPersistenceManager
{
    private readonly IMemoryStore _memoryStore;
    private readonly string _memoryDir;
    private readonly string _backupDir;
    private readonly object _lock = new();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public MemoryPersistenceManager(IMemoryStore memoryStore, string? memoryDirectory = null)
    {
        _memoryStore = memoryStore;
        _memoryDir = memoryDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "DesktopMascot", "memory");
        _backupDir = Path.Combine(_memoryDir, "backups");
        Directory.CreateDirectory(_backupDir);
    }

    /// <summary>
    /// 创建完整备份
    /// </summary>
    public async Task<MemoryBackupResult> BackupAsync(string? note = null, CancellationToken ct = default)
    {
        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
        var backupFileName = $"backup_{timestamp}.json";
        var backupPath = Path.Combine(_backupDir, backupFileName);

        var data = await _memoryStore.ExportAsync(null, ct);

        // 写入备份文件（带元数据）
        var backupData = new MemoryBackupFile
        {
            BackupTime = DateTime.UtcNow,
            Note = note ?? $"自动备份",
            EntryCount = JsonSerializer.Deserialize<List<MemoryEntry>>(data)?.Count ?? 0,
            Data = data
        };

        var json = JsonSerializer.Serialize(backupData, JsonOptions);
        await File.WriteAllTextAsync(backupPath, json, ct);

        // 清理旧备份（保留最近 20 个）
        CleanupOldBackups(20);

        return new MemoryBackupResult
        {
            Success = true,
            BackupPath = backupPath,
            EntryCount = backupData.EntryCount,
            SizeBytes = new FileInfo(backupPath).Length
        };
    }

    /// <summary>
    /// 从备份恢复
    /// </summary>
    public async Task<MemoryRestoreResult> RestoreAsync(string backupPath, bool merge = true, CancellationToken ct = default)
    {
        if (!File.Exists(backupPath))
            return new MemoryRestoreResult { Success = false, Error = $"备份文件不存在：{backupPath}" };

        try
        {
            var json = await File.ReadAllTextAsync(backupPath, ct);
            var backupFile = JsonSerializer.Deserialize<MemoryBackupFile>(json, JsonOptions);

            if (backupFile?.Data == null)
                return new MemoryRestoreResult { Success = false, Error = "备份文件格式无效" };

            if (merge)
            {
                // 合并模式：不覆盖已有记忆
                var imported = await _memoryStore.ImportAsync(backupFile.Data, ct);
                return new MemoryRestoreResult
                {
                    Success = true,
                    RestoredCount = imported,
                    BackupTime = backupFile.BackupTime,
                    Note = backupFile.Note
                };
            }
            else
            {
                // 覆盖模式：导出当前 → 替换
                await _memoryStore.ImportAsync(backupFile.Data, ct);
                return new MemoryRestoreResult
                {
                    Success = true,
                    RestoredCount = backupFile.EntryCount,
                    BackupTime = backupFile.BackupTime,
                    Note = backupFile.Note
                };
            }
        }
        catch (Exception ex)
        {
            return new MemoryRestoreResult { Success = false, Error = $"恢复失败：{ex.Message}" };
        }
    }

    /// <summary>
    /// 列出所有备份
    /// </summary>
    public List<MemoryBackupInfo> ListBackups()
    {
        var backups = new List<MemoryBackupInfo>();

        if (!Directory.Exists(_backupDir))
            return backups;

        foreach (var file in Directory.GetFiles(_backupDir, "backup_*.json").OrderByDescending(f => f))
        {
            try
            {
                var json = File.ReadAllText(file);
                var data = JsonSerializer.Deserialize<MemoryBackupFile>(json, JsonOptions);
                backups.Add(new MemoryBackupInfo
                {
                    Path = file,
                    BackupTime = data?.BackupTime ?? File.GetLastWriteTime(file),
                    Note = data?.Note ?? "",
                    EntryCount = data?.EntryCount ?? 0,
                    SizeBytes = new FileInfo(file).Length
                });
            }
            catch
            {
                backups.Add(new MemoryBackupInfo
                {
                    Path = file,
                    BackupTime = File.GetLastWriteTime(file),
                    Note = "(无法读取元数据)",
                    SizeBytes = new FileInfo(file).Length
                });
            }
        }

        return backups;
    }

    /// <summary>
    /// 清理过期记忆
    /// </summary>
    public async Task<MemoryCleanupResult> CleanupAsync(int maxAgeDays = 90, bool onlyUnconfirmed = true, CancellationToken ct = default)
    {
        var cutoff = DateTime.UtcNow.AddDays(-maxAgeDays);
        var removed = 0;
        var stats = await _memoryStore.GetStatisticsAsync(ct);

        // 搜索过期或未确认的旧记忆
        var types = new[] { MemoryType.User, MemoryType.Project, MemoryType.Skill, MemoryType.History };

        foreach (var type in types)
        {
            var entries = await _memoryStore.GetByTypeAsync(type, 1000, ct);

            foreach (var entry in entries)
            {
                ct.ThrowIfCancellationRequested();

                var isExpired = entry.ExpiresAt.HasValue && entry.ExpiresAt.Value < DateTime.UtcNow;
                var isOld = entry.UpdatedAt < cutoff;
                var isUnconfirmed = onlyUnconfirmed && !entry.IsConfirmed;

                if (isExpired || (isOld && isUnconfirmed))
                {
                    await _memoryStore.DeleteAsync(entry.Id, ct);
                    removed++;
                }
            }
        }

        return new MemoryCleanupResult
        {
            RemovedCount = removed,
            MaxAgeDays = maxAgeDays,
            OnlyUnconfirmed = onlyUnconfirmed
        };
    }

    /// <summary>
    /// 获取详细统计
    /// </summary>
    public async Task<MemoryDetailedStats> GetDetailedStatsAsync(CancellationToken ct = default)
    {
        var stats = await _memoryStore.GetStatisticsAsync(ct);
        var backups = ListBackups();

        var typeBreakdown = new Dictionary<string, int>();
        var types = new[] { MemoryType.User, MemoryType.Project, MemoryType.Skill, MemoryType.History };
        foreach (var type in types)
        {
            var entries = await _memoryStore.GetByTypeAsync(type, 10000, ct);
            typeBreakdown[type.ToString()] = entries.Count;
        }

        return new MemoryDetailedStats
        {
            TotalEntries = stats.TotalCount,
            UnconfirmedCount = stats.UnconfirmedCount,
            LastUpdated = stats.LastUpdated,
            BackupCount = backups.Count,
            LastBackupTime = backups.FirstOrDefault()?.BackupTime,
            TotalBackupSizeBytes = backups.Sum(b => b.SizeBytes),
            TypeBreakdown = typeBreakdown,
            MemoryDir = _memoryDir,
            BackupDir = _backupDir
        };
    }

    /// <summary>
    /// 导出为 Markdown 格式
    /// </summary>
    public async Task<string> ExportToMarkdownAsync(MemoryType? type = null, CancellationToken ct = default)
    {
        var stats = await _memoryStore.GetStatisticsAsync(ct);
        var entries = type.HasValue
            ? await _memoryStore.GetByTypeAsync(type.Value, 10000, ct)
            : (await _memoryStore.SearchAsync("", null, 10000, ct)).Entries;

        var sb = new StringBuilder();
        sb.AppendLine("# 记忆导出");
        sb.AppendLine();
        sb.AppendLine($"导出时间：{DateTime.Now:yyyy-MM-dd HH:mm}");
        sb.AppendLine($"总条目：{entries.Count}");
        sb.AppendLine();

        // 按类型分组
        var groups = entries.GroupBy(e => e.Type).OrderBy(g => g.Key);
        foreach (var group in groups)
        {
            sb.AppendLine($"## {group.Key}（{group.Count()} 条）");
            sb.AppendLine();

            foreach (var entry in group.OrderByDescending(e => e.UpdatedAt))
            {
                sb.AppendLine($"### {entry.Key}");
                sb.AppendLine();
                sb.AppendLine(entry.Content);
                sb.AppendLine();

                if (entry.Tags.Count > 0)
                    sb.AppendLine($"**标签：** {string.Join(", ", entry.Tags.Select(t => $"{t.Key}={t.Value}"))}");

                sb.AppendLine($"*创建：{entry.CreatedAt:yyyy-MM-dd} | 更新：{entry.UpdatedAt:yyyy-MM-dd} | 确认：{(entry.IsConfirmed ? "是" : "否")}*");
                sb.AppendLine();
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// 获取记忆健康度
    /// </summary>
    public async Task<MemoryHealthCheck> CheckHealthAsync(CancellationToken ct = default)
    {
        var check = new MemoryHealthCheck();
        var stats = await _memoryStore.GetStatisticsAsync(ct);

        // 检查 1：总条目数
        if (stats.TotalCount == 0)
            check.Issues.Add("记忆库为空，没有任何记忆条目");
        else if (stats.TotalCount > 5000)
            check.Issues.Add($"记忆条目过多（{stats.TotalCount}），建议清理过期记忆");

        // 检查 2：未确认比例
        if (stats.TotalCount > 0)
        {
            var unconfirmedRatio = (double)stats.UnconfirmedCount / stats.TotalCount;
            if (unconfirmedRatio > 0.5)
                check.Issues.Add($"未确认记忆比例过高（{unconfirmedRatio:P0}），建议清理或确认");
        }

        // 检查 3：备份状况
        var backups = ListBackups();
        if (backups.Count == 0)
            check.Warnings.Add("没有备份，建议定期备份");
        else if (backups.First().BackupTime < DateTime.UtcNow.AddDays(-7))
            check.Warnings.Add($"最近备份超过 7 天（{backups.First().BackupTime:yyyy-MM-dd}）");

        // 检查 4：文件大小
        var memoryFile = Path.Combine(_memoryDir, "memories.json");
        if (File.Exists(memoryFile))
        {
            var sizeMB = new FileInfo(memoryFile).Length / 1024.0 / 1024.0;
            if (sizeMB > 10)
                check.Warnings.Add($"记忆文件过大（{sizeMB:F1} MB），建议清理");
        }

        check.OverallHealth = check.Issues.Count == 0
            ? (check.Warnings.Count == 0 ? "健康" : "一般")
            : "需要关注";

        return check;
    }

    private void CleanupOldBackups(int keepCount)
    {
        lock (_lock)
        {
            var files = Directory.GetFiles(_backupDir, "backup_*.json")
                .OrderByDescending(f => f)
                .ToList();

            foreach (var file in files.Skip(keepCount))
            {
                try { File.Delete(file); } catch { }
            }
        }
    }
}

#region Models

public class MemoryBackupResult
{
    public bool Success { get; set; }
    public string? BackupPath { get; set; }
    public int EntryCount { get; set; }
    public long SizeBytes { get; set; }
    public string? Error { get; set; }
}

public class MemoryRestoreResult
{
    public bool Success { get; set; }
    public int RestoredCount { get; set; }
    public DateTime? BackupTime { get; set; }
    public string? Note { get; set; }
    public string? Error { get; set; }
}

public class MemoryBackupInfo
{
    public string Path { get; set; } = "";
    public DateTime BackupTime { get; set; }
    public string Note { get; set; } = "";
    public int EntryCount { get; set; }
    public long SizeBytes { get; set; }
}

public class MemoryBackupFile
{
    public DateTime BackupTime { get; set; }
    public string Note { get; set; } = "";
    public int EntryCount { get; set; }
    public string Data { get; set; } = "";
}

public class MemoryCleanupResult
{
    public int RemovedCount { get; set; }
    public int MaxAgeDays { get; set; }
    public bool OnlyUnconfirmed { get; set; }
}

public class MemoryDetailedStats
{
    public int TotalEntries { get; set; }
    public int UnconfirmedCount { get; set; }
    public DateTime? LastUpdated { get; set; }
    public int BackupCount { get; set; }
    public DateTime? LastBackupTime { get; set; }
    public long TotalBackupSizeBytes { get; set; }
    public Dictionary<string, int> TypeBreakdown { get; set; } = new();
    public string MemoryDir { get; set; } = "";
    public string BackupDir { get; set; } = "";
}

public class MemoryHealthCheck
{
    public string OverallHealth { get; set; } = "健康";
    public List<string> Issues { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}

#endregion
