using System.Text.Json;

namespace DesktopMascot.Core.Memory;

/// <summary>
/// 文件记忆存储实现（带内存缓存，减少文件 IO）
/// </summary>
public class FileMemoryStore : IMemoryStore
{
    private readonly string _memoryDirectory;
    private readonly object _lock = new();
    private const string MemoryFileName = "memories.json";

    // 内存缓存层 — 避免每次 CRUD 都全量读写文件
    private List<MemoryEntry>? _cache;
    private bool _isDirty;
    private DateTime _lastLoadedAt = DateTime.MinValue;

    public FileMemoryStore(string? memoryDirectory = null)
    {
        _memoryDirectory = memoryDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "DesktopMascot",
            "memory");
        Directory.CreateDirectory(_memoryDirectory);
    }

    private string GetFilePath() => Path.Combine(_memoryDirectory, MemoryFileName);

    /// <summary>
    /// 获取缓存（首次调用从文件加载）
    /// </summary>
    private List<MemoryEntry> GetCache()
    {
        if (_cache != null)
            return _cache;

        lock (_lock)
        {
            if (_cache != null)
                return _cache;

            var filePath = GetFilePath();
            if (!File.Exists(filePath))
            {
                _cache = new List<MemoryEntry>();
            }
            else
            {
                var json = File.ReadAllText(filePath);
                _cache = JsonSerializer.Deserialize<List<MemoryEntry>>(json) ?? new List<MemoryEntry>();
            }

            _lastLoadedAt = DateTime.UtcNow;
            _isDirty = false;
            return _cache;
        }
    }

    /// <summary>
    /// 标记缓存已变更并刷新到文件
    /// </summary>
    private void PersistCache()
    {
        lock (_lock)
        {
            if (_cache == null)
                return;

            var filePath = GetFilePath();
            var json = JsonSerializer.Serialize(_cache, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(filePath, json);
            _isDirty = false;
        }
    }

    /// <summary>
    /// 强制刷新到文件（显式调用）
    /// </summary>
    public Task FlushAsync(CancellationToken ct = default)
    {
        return Task.Run(() => PersistCache(), ct);
    }

    public Task<MemoryEntry> SaveAsync(MemoryEntry entry, CancellationToken ct = default)
    {
        var entries = GetCache();

        // 更新或添加
        var existingIndex = entries.FindIndex(e => e.Id == entry.Id);
        if (existingIndex >= 0)
        {
            entry.UpdatedAt = DateTime.UtcNow;
            entries[existingIndex] = entry;
        }
        else
        {
            entries.Add(entry);
        }

        PersistCache();
        return Task.FromResult(entry);
    }

    public Task<MemoryEntry?> GetByIdAsync(string id, CancellationToken ct = default)
    {
        var entries = GetCache();
        var result = entries.FirstOrDefault(e => e.Id == id);
        return Task.FromResult(result);
    }

    public Task<MemoryEntry?> GetByKeyAsync(string key, MemoryType type, CancellationToken ct = default)
    {
        var entries = GetCache();
        var result = entries.FirstOrDefault(e => e.Key == key && e.Type == type);
        return Task.FromResult(result);
    }

    public Task<bool> DeleteAsync(string id, CancellationToken ct = default)
    {
        var entries = GetCache();
        var removed = entries.RemoveAll(e => e.Id == id);

        if (removed > 0)
        {
            PersistCache();
            return Task.FromResult(true);
        }

        return Task.FromResult(false);
    }

    public Task<MemorySearchResult> SearchAsync(string query, MemoryType? type = null, int limit = 50, CancellationToken ct = default)
    {
        var entries = GetCache();
        var queryLower = query.ToLower();

        var results = entries
            .Where(e =>
                (type == null || e.Type == type) &&
                (e.Key.ToLower().Contains(queryLower) ||
                 e.Content.ToLower().Contains(queryLower) ||
                 e.Tags.Any(t => t.Value.ToLower().Contains(queryLower))))
            .OrderByDescending(e => e.UpdatedAt)
            .Take(limit)
            .ToList();

        return Task.FromResult(new MemorySearchResult
        {
            Entries = results,
            TotalCount = results.Count,
            Query = query
        });
    }

    public Task<List<MemoryEntry>> GetByTypeAsync(MemoryType type, int limit = 100, CancellationToken ct = default)
    {
        var entries = GetCache();
        var results = entries
            .Where(e => e.Type == type)
            .OrderByDescending(e => e.UpdatedAt)
            .Take(limit)
            .ToList();

        return Task.FromResult(results);
    }

    public Task<bool> ConfirmAsync(string id, CancellationToken ct = default)
    {
        var entries = GetCache();
        var entry = entries.FirstOrDefault(e => e.Id == id);

        if (entry == null)
            return Task.FromResult(false);

        entry.IsConfirmed = true;
        entry.UpdatedAt = DateTime.UtcNow;
        PersistCache();

        return Task.FromResult(true);
    }

    public Task<MemoryStatistics> GetStatisticsAsync(CancellationToken ct = default)
    {
        var entries = GetCache();

        return Task.FromResult(new MemoryStatistics
        {
            TotalCount = entries.Count,
            UserCount = entries.Count(e => e.Type == MemoryType.User),
            ProjectCount = entries.Count(e => e.Type == MemoryType.Project),
            SkillCount = entries.Count(e => e.Type == MemoryType.Skill),
            HistoryCount = entries.Count(e => e.Type == MemoryType.History),
            UnconfirmedCount = entries.Count(e => !e.IsConfirmed),
            LastUpdated = entries.Any() ? entries.Max(e => e.UpdatedAt) : null
        });
    }

    public Task<string> ExportAsync(MemoryType? type = null, CancellationToken ct = default)
    {
        var entries = GetCache();

        if (type.HasValue)
        {
            entries = entries.Where(e => e.Type == type.Value).ToList();
        }

        var json = JsonSerializer.Serialize(entries, new JsonSerializerOptions { WriteIndented = true });
        return Task.FromResult(json);
    }

    public Task<int> ImportAsync(string data, CancellationToken ct = default)
    {
        var imported = JsonSerializer.Deserialize<List<MemoryEntry>>(data);
        if (imported == null)
            return Task.FromResult(0);

        var entries = GetCache();
        var count = 0;

        foreach (var entry in imported)
        {
            if (!entries.Any(e => e.Key == entry.Key && e.Type == entry.Type))
            {
                entries.Add(entry);
                count++;
            }
        }

        PersistCache();
        return Task.FromResult(count);
    }
}
