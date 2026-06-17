using System.Collections.Concurrent;
using System.Text.Json;

namespace DesktopMascot.Core.Memory;

/// <summary>
/// 文件记忆存储实现（内存缓存 + 字典索引 + 批量刷新）
/// </summary>
public class FileMemoryStore : IMemoryStore
{
    private readonly string _memoryDirectory;
    private readonly object _lock = new();
    private const string MemoryFileName = "memories.json";
    private static readonly TimeSpan FlushInterval = TimeSpan.FromSeconds(30);

    // 内存缓存
    private List<MemoryEntry>? _list;
    private Dictionary<(string Key, MemoryType Type), MemoryEntry>? _keyIndex;
    private Dictionary<string, MemoryEntry>? _idIndex;
    private bool _isDirty;
    private DateTime _lastFlushAt = DateTime.UtcNow;
    private Timer? _flushTimer;

    public FileMemoryStore(string? memoryDirectory = null)
    {
        _memoryDirectory = memoryDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "DesktopMascot",
            "memory");
        Directory.CreateDirectory(_memoryDirectory);

        _flushTimer = new Timer(_ => PeriodicFlush(), null, FlushInterval, FlushInterval);
    }

    private string GetFilePath() => Path.Combine(_memoryDirectory, MemoryFileName);

    /// <summary>
    /// 加载并建立索引
    /// </summary>
    private void EnsureLoaded()
    {
        if (_list != null) return;

        lock (_lock)
        {
            if (_list != null) return;

            var filePath = GetFilePath();
            if (!File.Exists(filePath))
            {
                _list = new List<MemoryEntry>();
            }
            else
            {
                var json = File.ReadAllText(filePath);
                _list = JsonSerializer.Deserialize<List<MemoryEntry>>(json) ?? new List<MemoryEntry>();
            }

            RebuildIndexes();
            _isDirty = false;
            _lastFlushAt = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// 重建所有索引
    /// </summary>
    private void RebuildIndexes()
    {
        _keyIndex = new Dictionary<(string, MemoryType), MemoryEntry>();
        _idIndex = new Dictionary<string, MemoryEntry>();

        foreach (var entry in _list!)
        {
            _idIndex[entry.Id] = entry;
            _keyIndex[(entry.Key, entry.Type)] = entry;
        }
    }

    /// <summary>
    /// 写入文件
    /// </summary>
    private void Persist()
    {
        lock (_lock)
        {
            if (_list == null || !_isDirty) return;

            var filePath = GetFilePath();
            var json = JsonSerializer.Serialize(_list, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(filePath, json);
            _isDirty = false;
            _lastFlushAt = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// 定时刷新（30秒一次）
    /// </summary>
    private void PeriodicFlush()
    {
        if (_isDirty) Persist();
    }

    /// <summary>
    /// 强制刷新到文件
    /// </summary>
    public Task FlushAsync(CancellationToken ct = default)
    {
        return Task.Run(() => Persist(), ct);
    }

    public Task<MemoryEntry> SaveAsync(MemoryEntry entry, CancellationToken ct = default)
    {
        EnsureLoaded();

        lock (_lock)
        {
            if (_idIndex!.TryGetValue(entry.Id, out var existing))
            {
                // 更新旧索引
                _keyIndex!.Remove((existing.Key, existing.Type));

                entry.UpdatedAt = DateTime.UtcNow;
                existing.Key = entry.Key;
                existing.Type = entry.Type;
                existing.Content = entry.Content;
                existing.Tags = entry.Tags;
                existing.UpdatedAt = entry.UpdatedAt;
                existing.IsConfirmed = entry.IsConfirmed;
            }
            else
            {
                entry.CreatedAt = DateTime.UtcNow;
                entry.UpdatedAt = DateTime.UtcNow;
                _list!.Add(entry);
            }

            _idIndex[entry.Id] = entry;
            _keyIndex![(entry.Key, entry.Type)] = entry;
            _isDirty = true;
        }

        return Task.FromResult(entry);
    }

    public Task<MemoryEntry?> GetByIdAsync(string id, CancellationToken ct = default)
    {
        EnsureLoaded();

        _idIndex!.TryGetValue(id, out var result);
        return Task.FromResult(result);
    }

    public Task<MemoryEntry?> GetByKeyAsync(string key, MemoryType type, CancellationToken ct = default)
    {
        EnsureLoaded();

        _keyIndex!.TryGetValue((key, type), out var result);
        return Task.FromResult(result);
    }

    public Task<bool> DeleteAsync(string id, CancellationToken ct = default)
    {
        EnsureLoaded();

        lock (_lock)
        {
            if (!_idIndex!.TryGetValue(id, out var entry))
                return Task.FromResult(false);

            _list!.Remove(entry);
            _idIndex.Remove(id);
            _keyIndex!.Remove((entry.Key, entry.Type));
            _isDirty = true;
        }

        return Task.FromResult(true);
    }

    public Task<MemorySearchResult> SearchAsync(string query, MemoryType? type = null, int limit = 50, CancellationToken ct = default)
    {
        EnsureLoaded();

        var queryLower = query.ToLower();

        lock (_lock)
        {
            var results = _list!
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
    }

    public Task<List<MemoryEntry>> GetByTypeAsync(MemoryType type, int limit = 100, CancellationToken ct = default)
    {
        EnsureLoaded();

        lock (_lock)
        {
            var results = _list!
                .Where(e => e.Type == type)
                .OrderByDescending(e => e.UpdatedAt)
                .Take(limit)
                .ToList();

            return Task.FromResult(results);
        }
    }

    public Task<bool> ConfirmAsync(string id, CancellationToken ct = default)
    {
        EnsureLoaded();

        lock (_lock)
        {
            if (!_idIndex!.TryGetValue(id, out var entry))
                return Task.FromResult(false);

            entry.IsConfirmed = true;
            entry.UpdatedAt = DateTime.UtcNow;
            _isDirty = true;
        }

        return Task.FromResult(true);
    }

    public Task<MemoryStatistics> GetStatisticsAsync(CancellationToken ct = default)
    {
        EnsureLoaded();

        lock (_lock)
        {
            return Task.FromResult(new MemoryStatistics
            {
                TotalCount = _list!.Count,
                UserCount = _list.Count(e => e.Type == MemoryType.User),
                ProjectCount = _list.Count(e => e.Type == MemoryType.Project),
                SkillCount = _list.Count(e => e.Type == MemoryType.Skill),
                HistoryCount = _list.Count(e => e.Type == MemoryType.History),
                UnconfirmedCount = _list.Count(e => !e.IsConfirmed),
                LastUpdated = _list.Any() ? _list.Max(e => e.UpdatedAt) : null
            });
        }
    }

    public Task<string> ExportAsync(MemoryType? type = null, CancellationToken ct = default)
    {
        EnsureLoaded();

        lock (_lock)
        {
            var entries = type.HasValue
                ? _list!.Where(e => e.Type == type.Value).ToList()
                : _list!.ToList();

            var json = JsonSerializer.Serialize(entries, new JsonSerializerOptions { WriteIndented = true });
            return Task.FromResult(json);
        }
    }

    public Task<int> ImportAsync(string data, CancellationToken ct = default)
    {
        var imported = JsonSerializer.Deserialize<List<MemoryEntry>>(data);
        if (imported == null)
            return Task.FromResult(0);

        EnsureLoaded();
        var count = 0;

        lock (_lock)
        {
            foreach (var entry in imported)
            {
                if (!_keyIndex!.ContainsKey((entry.Key, entry.Type)))
                {
                    _list!.Add(entry);
                    _idIndex![entry.Id] = entry;
                    _keyIndex[(entry.Key, entry.Type)] = entry;
                    count++;
                }
            }

            _isDirty = true;
        }

        return Task.FromResult(count);
    }

    /// <summary>
    /// 释放资源
    /// </summary>
    public void Dispose()
    {
        _flushTimer?.Dispose();
        Persist();
    }
}
