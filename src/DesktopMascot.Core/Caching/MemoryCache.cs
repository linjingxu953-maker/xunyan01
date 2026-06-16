using System.Collections.Concurrent;

namespace DesktopMascot.Core.Caching;

/// <summary>
/// 缓存接口
/// </summary>
public interface ICache
{
    /// <summary>获取缓存</summary>
    T? Get<T>(string key);
    
    /// <summary>设置缓存</summary>
    void Set<T>(string key, T value, CacheExpiration expiration = CacheExpiration.Sliding, TimeSpan? expirationTime = null);
    
    /// <summary>移除缓存</summary>
    bool Remove(string key);
    
    /// <summary>清空缓存</summary>
    void Clear();
    
    /// <summary>检查是否存在</summary>
    bool Contains(string key);
    
    /// <summary>获取缓存统计</summary>
    CacheStatistics GetStatistics();
    
    /// <summary>清理过期条目</summary>
    int Cleanup();
    
    /// <summary>按标签移除</summary>
    int RemoveByTag(string tag);
    
    /// <summary>批量获取</summary>
    Dictionary<string, T?> GetBatch<T>(IEnumerable<string> keys);
    
    /// <summary>批量设置</summary>
    void SetBatch<T>(IEnumerable<KeyValuePair<string, T>> items, CacheExpiration expiration = CacheExpiration.Sliding, TimeSpan? expirationTime = null);
}

/// <summary>
/// 内存缓存实现（LRU 驱逐策略）
/// </summary>
public class MemoryCache : ICache
{
    private readonly ConcurrentDictionary<string, CacheEntry<object>> _cache = new();
    private readonly CacheOptions _options;
    private long _hits;
    private long _misses;

    public MemoryCache(CacheOptions? options = null)
    {
        _options = options ?? new CacheOptions();
    }

    public T? Get<T>(string key)
    {
        if (!_cache.TryGetValue(key, out var entry))
        {
            Interlocked.Increment(ref _misses);
            return default;
        }

        if (IsExpired(entry))
        {
            _cache.TryRemove(key, out _);
            Interlocked.Increment(ref _misses);
            return default;
        }

        entry.LastAccessedAt = DateTime.UtcNow;
        Interlocked.Increment(ref entry.AccessCount);
        Interlocked.Increment(ref _hits);

        if (entry.Expiration == CacheExpiration.Sliding && entry.SlidingExpiration.HasValue)
        {
            entry.ExpiresAt = DateTime.UtcNow.Add(entry.SlidingExpiration.Value);
        }

        return (T)entry.Value;
    }

    public void Set<T>(string key, T value, CacheExpiration expiration = CacheExpiration.Sliding, TimeSpan? expirationTime = null)
    {
        if (_cache.Count >= _options.MaxEntries)
        {
            EvictLRU();
        }

        var effectiveExpiration = expirationTime ?? _options.DefaultExpiration;
        
        _cache[key] = new CacheEntry<object>
        {
            Key = key,
            Value = value!,
            CreatedAt = DateTime.UtcNow,
            LastAccessedAt = DateTime.UtcNow,
            Expiration = expiration,
            SlidingExpiration = effectiveExpiration,
            ExpiresAt = expiration == CacheExpiration.Absolute 
                ? DateTime.UtcNow.Add(effectiveExpiration ?? TimeSpan.FromMinutes(30))
                : null,
            SizeBytes = CalculateSize(value)
        };
    }

    public bool Remove(string key)
    {
        return _cache.TryRemove(key, out _);
    }

    public void Clear()
    {
        _cache.Clear();
        Interlocked.Exchange(ref _hits, 0);
        Interlocked.Exchange(ref _misses, 0);
    }

    public bool Contains(string key)
    {
        if (!_cache.TryGetValue(key, out var entry))
            return false;

        if (IsExpired(entry))
        {
            _cache.TryRemove(key, out _);
            return false;
        }

        return true;
    }

    public CacheStatistics GetStatistics()
    {
        var entries = _cache.Values.ToList();
        return new CacheStatistics
        {
            TotalEntries = entries.Count,
            TotalHits = (int)Interlocked.Read(ref _hits),
            TotalMisses = (int)Interlocked.Read(ref _misses),
            TotalSizeBytes = entries.Sum(e => e.SizeBytes),
            OldestEntry = entries.Any() ? entries.Min(e => e.CreatedAt) : null,
            NewestEntry = entries.Any() ? entries.Max(e => e.CreatedAt) : null
        };
    }

    public int Cleanup()
    {
        var expiredKeys = _cache
            .Where(kvp => IsExpired(kvp.Value))
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in expiredKeys)
        {
            _cache.TryRemove(key, out _);
        }

        return expiredKeys.Count;
    }

    public int RemoveByTag(string tag)
    {
        var keysToRemove = _cache
            .Where(kvp => kvp.Value.Tags != null && kvp.Value.Tags.Contains(tag))
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in keysToRemove)
        {
            _cache.TryRemove(key, out _);
        }

        return keysToRemove.Count;
    }

    public Dictionary<string, T?> GetBatch<T>(IEnumerable<string> keys)
    {
        var result = new Dictionary<string, T?>();
        foreach (var key in keys)
        {
            result[key] = Get<T>(key);
        }
        return result;
    }

    public void SetBatch<T>(IEnumerable<KeyValuePair<string, T>> items, CacheExpiration expiration = CacheExpiration.Sliding, TimeSpan? expirationTime = null)
    {
        foreach (var item in items)
        {
            Set(item.Key, item.Value, expiration, expirationTime);
        }
    }

    private bool IsExpired(CacheEntry<object> entry)
    {
        if (entry.Expiration == CacheExpiration.None)
            return false;

        if (entry.ExpiresAt.HasValue && entry.ExpiresAt <= DateTime.UtcNow)
            return true;

        return false;
    }

    private void EvictLRU()
    {
        if (_cache.IsEmpty) return;

        var oldest = _cache.OrderBy(kvp => kvp.Value.LastAccessedAt).FirstOrDefault();
        if (!oldest.Equals(default(KeyValuePair<string, CacheEntry<object>>)))
        {
            _cache.TryRemove(oldest.Key, out _);
        }
    }

    private long CalculateSize(object value)
    {
        try
        {
            return System.Text.Json.JsonSerializer.Serialize(value).Length;
        }
        catch
        {
            return 0;
        }
    }
}

/// <summary>
/// 分布式缓存接口（用于未来扩展）
/// </summary>
public interface IDistributedCache
{
    Task<T?> GetAsync<T>(string key, CancellationToken ct = default);
    Task SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken ct = default);
    Task<bool> RemoveAsync(string key, CancellationToken ct = default);
}
