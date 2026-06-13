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
}

/// <summary>
/// 内存缓存实现
/// </summary>
public class MemoryCache : ICache
{
    private readonly Dictionary<string, object> _cache = new();
    private readonly Dictionary<string, CacheEntry<object>> _metadata = new();
    private readonly CacheOptions _options;
    private readonly object _lock = new();
    private int _hits;
    private int _misses;

    public MemoryCache(CacheOptions? options = null)
    {
        _options = options ?? new CacheOptions();
    }

    public T? Get<T>(string key)
    {
        lock (_lock)
        {
            if (!_cache.ContainsKey(key))
            {
                _misses++;
                return default;
            }

            var entry = _metadata[key];
            
            // 检查是否过期
            if (IsExpired(entry))
            {
                Remove(key);
                _misses++;
                return default;
            }

            // 更新访问信息
            entry.LastAccessedAt = DateTime.UtcNow;
            entry.AccessCount++;
            _hits++;

            if (entry.Expiration == CacheExpiration.Sliding && entry.SlidingExpiration.HasValue)
            {
                entry.ExpiresAt = DateTime.UtcNow.Add(entry.SlidingExpiration.Value);
            }

            return (T)_cache[key];
        }
    }

    public void Set<T>(string key, T value, CacheExpiration expiration = CacheExpiration.Sliding, TimeSpan? expirationTime = null)
    {
        lock (_lock)
        {
            // 检查容量
            if (_cache.Count >= _options.MaxEntries)
            {
                EvictOldest();
            }

            var effectiveExpiration = expirationTime ?? _options.DefaultExpiration;
            
            _cache[key] = value!;
            _metadata[key] = new CacheEntry<object>
            {
                Key = key,
                Value = value,
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
    }

    public bool Remove(string key)
    {
        lock (_lock)
        {
            _metadata.Remove(key);
            return _cache.Remove(key);
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            _cache.Clear();
            _metadata.Clear();
            _hits = 0;
            _misses = 0;
        }
    }

    public bool Contains(string key)
    {
        lock (_lock)
        {
            if (!_cache.ContainsKey(key))
                return false;

            var entry = _metadata[key];
            if (IsExpired(entry))
            {
                Remove(key);
                return false;
            }

            return true;
        }
    }

    public CacheStatistics GetStatistics()
    {
        lock (_lock)
        {
            return new CacheStatistics
            {
                TotalEntries = _cache.Count,
                TotalHits = _hits,
                TotalMisses = _misses,
                TotalSizeBytes = _metadata.Values.Sum(e => e.SizeBytes),
                OldestEntry = _metadata.Values.Any() ? _metadata.Values.Min(e => e.CreatedAt) : null,
                NewestEntry = _metadata.Values.Any() ? _metadata.Values.Max(e => e.CreatedAt) : null
            };
        }
    }

    public int Cleanup()
    {
        lock (_lock)
        {
            var expiredKeys = _metadata
                .Where(kvp => IsExpired(kvp.Value))
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var key in expiredKeys)
            {
                Remove(key);
            }

            return expiredKeys.Count;
        }
    }

    public int RemoveByTag(string tag)
    {
        lock (_lock)
        {
            var keysToRemove = _metadata
                .Where(kvp => kvp.Value.Tags != null && kvp.Value.Tags.Contains(tag))
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var key in keysToRemove)
            {
                Remove(key);
            }

            return keysToRemove.Count;
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

    private void EvictOldest()
    {
        if (_metadata.Count == 0)
            return;

        var oldest = _metadata.OrderBy(kvp => kvp.Value.LastAccessedAt).First();
        Remove(oldest.Key);
    }

    private long CalculateSize(object value)
    {
        // 简化的大小估算
        return System.Text.Json.JsonSerializer.Serialize(value).Length;
    }
}
