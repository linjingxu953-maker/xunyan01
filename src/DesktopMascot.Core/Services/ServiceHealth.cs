namespace DesktopMascot.Core.Services;

/// <summary>
/// 服务健康状态
/// </summary>
public enum ServiceHealth
{
    Healthy,
    Degraded,
    Unhealthy,
    Unknown
}

/// <summary>
/// 服务健康检查结果
/// </summary>
public class HealthCheckResult
{
    public string ServiceName { get; set; } = string.Empty;
    public ServiceHealth Health { get; set; }
    public string? Message { get; set; }
    public TimeSpan ResponseTime { get; set; }
    public Dictionary<string, string> Metadata { get; set; } = new();
}

/// <summary>
/// 服务健康检查器接口
/// </summary>
public interface IServiceHealthChecker
{
    /// <summary>检查单个服务健康状态</summary>
    Task<HealthCheckResult> CheckHealthAsync(string serviceName, CancellationToken ct = default);
    
    /// <summary>检查所有服务健康状态</summary>
    Task<List<HealthCheckResult>> CheckAllHealthAsync(CancellationToken ct = default);
    
    /// <summary>注册健康检查</summary>
    void RegisterHealthCheck(string serviceName, Func<CancellationToken, Task<bool>> checkFunc);
}

/// <summary>
/// 服务健康检查器实现
/// </summary>
public class ServiceHealthChecker : IServiceHealthChecker
{
    private readonly Dictionary<string, Func<CancellationToken, Task<bool>>> _healthChecks = new();

    public void RegisterHealthCheck(string serviceName, Func<CancellationToken, Task<bool>> checkFunc)
    {
        _healthChecks[serviceName] = checkFunc;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(string serviceName, CancellationToken ct = default)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            if (!_healthChecks.TryGetValue(serviceName, out var checkFunc))
            {
                return new HealthCheckResult
                {
                    ServiceName = serviceName,
                    Health = ServiceHealth.Unknown,
                    Message = "未注册健康检查",
                    ResponseTime = stopwatch.Elapsed
                };
            }

            var isHealthy = await checkFunc(ct);
            stopwatch.Stop();

            return new HealthCheckResult
            {
                ServiceName = serviceName,
                Health = isHealthy ? ServiceHealth.Healthy : ServiceHealth.Unhealthy,
                ResponseTime = stopwatch.Elapsed
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return new HealthCheckResult
            {
                ServiceName = serviceName,
                Health = ServiceHealth.Unhealthy,
                Message = ex.Message,
                ResponseTime = stopwatch.Elapsed
            };
        }
    }

    public async Task<List<HealthCheckResult>> CheckAllHealthAsync(CancellationToken ct = default)
    {
        var results = new List<HealthCheckResult>();

        foreach (var serviceName in _healthChecks.Keys)
        {
            var result = await CheckHealthAsync(serviceName, ct);
            results.Add(result);
        }

        return results;
    }
}

/// <summary>
/// 服务初始化顺序管理器
/// </summary>
public class ServiceInitializer
{
    private readonly List<ServiceInitItem> _initOrder = new();

    public void Register(string serviceName, Func<CancellationToken, Task> initializer, int priority = 0)
    {
        _initOrder.Add(new ServiceInitItem
        {
            ServiceName = serviceName,
            Initializer = initializer,
            Priority = priority
        });
    }

    public async Task InitializeAllAsync(CancellationToken ct = default)
    {
        var ordered = _initOrder.OrderByDescending(x => x.Priority).ToList();

        foreach (var item in ordered)
        {
            try
            {
                await item.Initializer(ct);
            }
            catch (Exception ex)
            {
                // 记录错误但继续初始化其他服务
                Console.WriteLine($"服务初始化失败 {item.ServiceName}: {ex.Message}");
            }
        }
    }
}

/// <summary>
/// 服务初始化项
/// </summary>
public class ServiceInitItem
{
    public string ServiceName { get; set; } = string.Empty;
    public Func<CancellationToken, Task> Initializer { get; set; } = null!;
    public int Priority { get; set; }
}
