namespace DesktopMascot.Core.Caching;

/// <summary>
/// 缓存过期策略
/// </summary>
public enum CacheExpiration
{
    /// <summary>不过期</summary>
    None,
    /// <summary>绝对过期</summary>
    Absolute,
    /// <summary>滑动过期（访问后重置）</summary>
    Sliding,
    /// <summary>文件依赖</summary>
    FileDependency
}

/// <summary>
/// 缓存条目
/// </summary>
public class CacheEntry<T>
{
    public string Key { get; set; } = string.Empty;
    public T? Value { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastAccessedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ExpiresAt { get; set; }
    public CacheExpiration Expiration { get; set; }
    public TimeSpan? SlidingExpiration { get; set; }
    public int AccessCount { get; set; }
    public long SizeBytes { get; set; }
    public string? Tags { get; set; }
}

/// <summary>
/// 缓存统计
/// </summary>
public class CacheStatistics
{
    public int TotalEntries { get; set; }
    public int TotalHits { get; set; }
    public int TotalMisses { get; set; }
    public double HitRate => TotalHits + TotalMisses > 0 
        ? (double)TotalHits / (TotalHits + TotalMisses) * 100 
        : 0;
    public long TotalSizeBytes { get; set; }
    public DateTime? OldestEntry { get; set; }
    public DateTime? NewestEntry { get; set; }
}

/// <summary>
/// 缓存选项
/// </summary>
public class CacheOptions
{
    public int MaxEntries { get; set; } = 1000;
    public long MaxSizeBytes { get; set; } = 100 * 1024 * 1024; // 100MB
    public TimeSpan? DefaultExpiration { get; set; } = TimeSpan.FromMinutes(30);
    public CacheExpiration DefaultExpirationType { get; set; } = CacheExpiration.Sliding;
    public bool EnableStatistics { get; set; } = true;
}
