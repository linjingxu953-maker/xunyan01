namespace DesktopMascot.Core.Caching;

/// <summary>
/// 缓存装饰器 - 为服务添加缓存能力
/// </summary>
public class CachedService<T> where T : class
{
    private readonly T _service;
    private readonly ICache _cache;
    private readonly string _prefix;

    public CachedService(T service, ICache cache, string? prefix = null)
    {
        _service = service;
        _cache = cache;
        _prefix = prefix ?? typeof(T).Name;
    }

    public T Service => _service;

    /// <summary>获取或创建缓存</summary>
    public TResult GetOrCreate<TResult>(
        string key,
        Func<TResult> factory,
        CacheExpiration expiration = CacheExpiration.Sliding,
        TimeSpan? expirationTime = null)
    {
        var cacheKey = $"{_prefix}:{key}";
        var cached = _cache.Get<TResult>(cacheKey);
        
        if (cached != null)
            return cached;

        var result = factory();
        _cache.Set(cacheKey, result, expiration, expirationTime);
        return result;
    }

    /// <summary>异步获取或创建缓存</summary>
    public async Task<TResult> GetOrCreateAsync<TResult>(
        string key,
        Func<Task<TResult>> factory,
        CacheExpiration expiration = CacheExpiration.Sliding,
        TimeSpan? expirationTime = null)
    {
        var cacheKey = $"{_prefix}:{key}";
        var cached = _cache.Get<TResult>(cacheKey);
        
        if (cached != null)
            return cached;

        var result = await factory();
        _cache.Set(cacheKey, result, expiration, expirationTime);
        return result;
    }

    /// <summary>移除缓存</summary>
    public void Invalidate(string key)
    {
        var cacheKey = $"{_prefix}:{key}";
        _cache.Remove(cacheKey);
    }

    /// <summary>移除所有相关缓存</summary>
    public void InvalidateAll()
    {
        _cache.RemoveByTag(_prefix);
    }
}

/// <summary>
/// 缓存扩展方法
/// </summary>
public static class CacheExtensions
{
    /// <summary>带缓存的执行</summary>
    public static T? WithCache<T>(
        this ICache cache,
        string key,
        Func<T?> factory,
        CacheExpiration expiration = CacheExpiration.Sliding,
        TimeSpan? expirationTime = null)
    {
        var cached = cache.Get<T>(key);
        if (cached != null)
            return cached;

        var result = factory();
        if (result != null)
            cache.Set(key, result, expiration, expirationTime);
        return result;
    }

    /// <summary>异步带缓存的执行</summary>
    public static async Task<T?> WithCacheAsync<T>(
        this ICache cache,
        string key,
        Func<Task<T?>> factory,
        CacheExpiration expiration = CacheExpiration.Sliding,
        TimeSpan? expirationTime = null)
    {
        var cached = cache.Get<T>(key);
        if (cached != null)
            return cached;

        var result = await factory();
        if (result != null)
            cache.Set(key, result, expiration, expirationTime);
        return result;
    }

    /// <summary>批量移除</summary>
    public static int RemoveByPrefix(this ICache cache, string prefix)
    {
        // 简化实现：需要遍历所有键
        // 实际应用中应维护键索引
        return 0;
    }
}
