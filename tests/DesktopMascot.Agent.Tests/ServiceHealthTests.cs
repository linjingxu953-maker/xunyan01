using DesktopMascot.Core.Services;

namespace DesktopMascot.Agent.Tests;

public class ServiceHealthTests
{
    [Fact]
    public async Task HealthChecker_RegisteredCheck_ShouldReturnHealthy()
    {
        var checker = new ServiceHealthChecker();
        checker.RegisterHealthCheck("test", _ => Task.FromResult(true));

        var result = await checker.CheckHealthAsync("test");

        Assert.Equal(ServiceHealth.Healthy, result.Health);
        Assert.True(result.ResponseTime > TimeSpan.Zero);
    }

    [Fact]
    public async Task HealthChecker_UnhealthyCheck_ShouldReturnUnhealthy()
    {
        var checker = new ServiceHealthChecker();
        checker.RegisterHealthCheck("test", _ => Task.FromResult(false));

        var result = await checker.CheckHealthAsync("test");

        Assert.Equal(ServiceHealth.Unhealthy, result.Health);
    }

    [Fact]
    public async Task HealthChecker_ExceptionCheck_ShouldReturnUnhealthy()
    {
        var checker = new ServiceHealthChecker();
        checker.RegisterHealthCheck("test", _ => throw new Exception("test error"));

        var result = await checker.CheckHealthAsync("test");

        Assert.Equal(ServiceHealth.Unhealthy, result.Health);
        Assert.Contains("test error", result.Message);
    }

    [Fact]
    public async Task HealthChecker_UnregisteredService_ShouldReturnUnknown()
    {
        var checker = new ServiceHealthChecker();

        var result = await checker.CheckHealthAsync("unknown");

        Assert.Equal(ServiceHealth.Unknown, result.Health);
    }

    [Fact]
    public async Task HealthChecker_CheckAll_ShouldReturnAllResults()
    {
        var checker = new ServiceHealthChecker();
        checker.RegisterHealthCheck("service1", _ => Task.FromResult(true));
        checker.RegisterHealthCheck("service2", _ => Task.FromResult(false));

        var results = await checker.CheckAllHealthAsync();

        Assert.Equal(2, results.Count);
        Assert.Contains(results, r => r.Health == ServiceHealth.Healthy);
        Assert.Contains(results, r => r.Health == ServiceHealth.Unhealthy);
    }

    [Fact]
    public async Task ServiceInitializer_ShouldInitializeInOrder()
    {
        var initializer = new ServiceInitializer();
        var order = new List<string>();

        initializer.Register("low", _ => { order.Add("low"); return Task.CompletedTask; }, 1);
        initializer.Register("high", _ => { order.Add("high"); return Task.CompletedTask; }, 3);
        initializer.Register("medium", _ => { order.Add("medium"); return Task.CompletedTask; }, 2);

        await initializer.InitializeAllAsync();

        Assert.Equal("high", order[0]);
        Assert.Equal("medium", order[1]);
        Assert.Equal("low", order[2]);
    }
}
