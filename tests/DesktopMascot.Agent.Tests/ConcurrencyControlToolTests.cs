using System.Text.Json;
using DesktopMascot.Agent.Tools;

namespace DesktopMascot.Agent.Tests;

public class ConcurrencyControlToolTests
{
    [Fact]
    public async Task QueueTask_ShouldQueueSuccessfully()
    {
        var tool = new ConcurrencyControlTool();
        var args = JsonSerializer.Serialize(new { action = "queue", task_name = "test_task" });
        var result = await tool.ExecuteAsync(args);

        Assert.True(result.Success);
        Assert.Contains("已加入队列", result.Content);
    }

    [Fact]
    public async Task RunParallel_ShouldExecute()
    {
        var tool = new ConcurrencyControlTool();
        var args = JsonSerializer.Serialize(new { action = "parallel", max_parallel = 2 });
        var result = await tool.ExecuteAsync(args);

        Assert.True(result.Success);
        Assert.Contains("并行执行完成", result.Content);
    }

    [Fact]
    public async Task AcquireLock_ShouldAcquireLock()
    {
        var tool = new ConcurrencyControlTool();
        var args = JsonSerializer.Serialize(new { action = "lock", resource = "test_resource" });
        var result = await tool.ExecuteAsync(args);

        Assert.True(result.Success);
        Assert.Contains("已获取锁", result.Content);
    }

    [Fact]
    public async Task ReleaseLock_ShouldReleaseLock()
    {
        var tool = new ConcurrencyControlTool();

        // 先获取锁
        var acquireArgs = JsonSerializer.Serialize(new { action = "lock", resource = "test_release" });
        await tool.ExecuteAsync(acquireArgs);

        // 释放锁
        var releaseArgs = JsonSerializer.Serialize(new { action = "unlock", resource = "test_release" });
        var result = await tool.ExecuteAsync(releaseArgs);

        Assert.True(result.Success);
        Assert.Contains("已释放锁", result.Content);
    }

    [Fact]
    public async Task ReleaseNonexistentLock_ShouldFail()
    {
        var tool = new ConcurrencyControlTool();
        var args = JsonSerializer.Serialize(new { action = "unlock", resource = "nonexistent" });
        var result = await tool.ExecuteAsync(args);

        Assert.False(result.Success);
        Assert.Contains("不存在", result.Error);
    }

    [Fact]
    public async Task Status_ShouldReturnInfo()
    {
        var tool = new ConcurrencyControlTool();
        var args = JsonSerializer.Serialize(new { action = "status" });
        var result = await tool.ExecuteAsync(args);

        Assert.True(result.Success);
        Assert.Contains("并发控制状态", result.Content);
    }

    [Fact]
    public async Task Stats_ShouldReturnInfo()
    {
        var tool = new ConcurrencyControlTool();
        var args = JsonSerializer.Serialize(new { action = "stats" });
        var result = await tool.ExecuteAsync(args);

        Assert.True(result.Success);
        Assert.Contains("并发统计", result.Content);
    }

    [Fact]
    public void ConcurrencyControlTool_Metadata_ShouldBeCorrect()
    {
        var tool = new ConcurrencyControlTool();
        Assert.Equal("concurrency_control", tool.Name);
        Assert.Contains("queue", tool.ParametersSchema);
        Assert.Contains("lock", tool.ParametersSchema);
    }
}
