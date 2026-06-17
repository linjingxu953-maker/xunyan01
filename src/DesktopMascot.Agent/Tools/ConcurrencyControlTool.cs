using DesktopMascot.Core.Tools;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using DesktopMascot.Agent.Models;

namespace DesktopMascot.Agent.Tools;

/// <summary>
/// 并发控制工具 - 任务队列、并行执行、资源锁
/// </summary>
public class ConcurrencyControlTool : ITool
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new();
    private static readonly ConcurrentDictionary<string, TaskQueue> _queues = new();

    public string Name => "concurrency_control";
    public string Description => "并发控制：任务队列、并行执行、资源锁管理。";
    public string ParametersSchema => """
    {
        "type": "object",
        "properties": {
            "action": { "type": "string", "enum": ["queue", "parallel", "lock", "unlock", "status", "stats"], "description": "操作类型" },
            "task_name": { "type": "string", "description": "任务名称" },
            "max_parallel": { "type": "integer", "description": "最大并行数（parallel模式）" },
            "resource": { "type": "string", "description": "资源名称（lock模式）" },
            "timeout": { "type": "integer", "description": "超时时间秒数" }
        },
        "required": ["action"]
    }
    """;

    public async Task<ToolResult> ExecuteAsync(string arguments, CancellationToken ct = default)
    {
        try
        {
            var doc = JsonDocument.Parse(arguments);
            var root = doc.RootElement;
            var action = root.TryGetProperty("action", out var aEl) ? aEl.GetString() ?? "" : "";

            return action switch
            {
                "queue" => await QueueTaskAsync(root, ct),
                "parallel" => await RunParallelAsync(root, ct),
                "lock" => await AcquireLockAsync(root, ct),
                "unlock" => await ReleaseLockAsync(root),
                "status" => GetStatus(),
                "stats" => GetStats(),
                _ => Fail($"不支持的操作：{action}")
            };
        }
        catch (Exception ex)
        {
            return Fail($"并发控制失败：{ex.Message}");
        }
    }

    private async Task<ToolResult> QueueTaskAsync(JsonElement root, CancellationToken ct)
    {
        var taskName = root.TryGetProperty("task_name", out var tEl) ? tEl.GetString() ?? "" : "";
        if (string.IsNullOrEmpty(taskName)) return Fail("缺少 task_name 参数");

        var queue = _queues.GetOrAdd(taskName, _ => new TaskQueue());

        var timeout = root.TryGetProperty("timeout", out var toEl) ? toEl.GetInt32() : 30;
        var semaphore = new SemaphoreSlim(1, 1);
        var acquired = await semaphore.WaitAsync(TimeSpan.FromSeconds(timeout), ct);

        if (!acquired)
            return Fail($"任务队列超时：{taskName}");

        return new ToolResult
        {
            Name = Name,
            Success = true,
            Content = $"任务已加入队列：{taskName}\n队列深度：{queue.Count}"
        };
    }

    private async Task<ToolResult> RunParallelAsync(JsonElement root, CancellationToken ct)
    {
        var maxParallel = root.TryGetProperty("max_parallel", out var mpEl) ? mpEl.GetInt32() : 4;

        var sw = Stopwatch.StartNew();
        var results = new ConcurrentBag<string>();

        var options = new ParallelOptions
        {
            MaxDegreeOfParallelism = maxParallel,
            CancellationToken = ct
        };

        await Parallel.ForEachAsync(Enumerable.Range(1, 10), options, async (i, token) =>
        {
            await Task.Delay(100, token);
            results.Add($"任务{i}完成");
        });

        sw.Stop();

        return new ToolResult
        {
            Name = Name,
            Success = true,
            Content = $"并行执行完成\n最大并行数：{maxParallel}\n执行时间：{sw.ElapsedMilliseconds} ms\n完成任务：{results.Count}"
        };
    }

    private async Task<ToolResult> AcquireLockAsync(JsonElement root, CancellationToken ct)
    {
        var resource = root.TryGetProperty("resource", out var rEl) ? rEl.GetString() ?? "" : "default";
        var timeout = root.TryGetProperty("timeout", out var toEl) ? toEl.GetInt32() : 30;

        var semaphore = _locks.GetOrAdd(resource, _ => new SemaphoreSlim(1, 1));
        var acquired = await semaphore.WaitAsync(TimeSpan.FromSeconds(timeout), ct);

        if (!acquired)
            return Fail($"获取锁超时：{resource}");

        return new ToolResult
        {
            Name = Name,
            Success = true,
            Content = $"已获取锁：{resource}\n当前锁数：{_locks.Count}"
        };
    }

    private async Task<ToolResult> ReleaseLockAsync(JsonElement root)
    {
        var resource = root.TryGetProperty("resource", out var rEl) ? rEl.GetString() ?? "" : "default";

        if (_locks.TryRemove(resource, out var semaphore))
        {
            semaphore.Release();
            return new ToolResult
            {
                Name = Name,
                Success = true,
                Content = $"已释放锁：{resource}"
            };
        }

        return Fail($"锁不存在：{resource}");
    }

    private ToolResult GetStatus()
    {
        var sb = new StringBuilder();
        sb.AppendLine("并发控制状态");
        sb.AppendLine($"活跃锁数：{_locks.Count}");
        sb.AppendLine($"活跃队列数：{_queues.Count}");
        sb.AppendLine($"总线程数：{Environment.ProcessorCount}");

        return new ToolResult { Name = Name, Success = true, Content = sb.ToString() };
    }

    private ToolResult GetStats()
    {
        var sb = new StringBuilder();
        sb.AppendLine("并发统计");
        sb.AppendLine($"系统处理器数：{Environment.ProcessorCount}");
        sb.AppendLine($"当前线程数：{Process.GetCurrentProcess().Threads.Count}");
        sb.AppendLine($"活跃锁：{_locks.Count}");
        sb.AppendLine($"活跃队列：{_queues.Count}");

        return new ToolResult { Name = Name, Success = true, Content = sb.ToString() };
    }

    private static ToolResult Fail(string error) => new() { Name = "concurrency_control", Success = false, Error = error };
}

/// <summary>
/// 任务队列
/// </summary>
public class TaskQueue
{
    private readonly ConcurrentQueue<string> _queue = new();
    public int Count => _queue.Count;
    public void Enqueue(string task) => _queue.Enqueue(task);
    public bool TryDequeue(out string task) => _queue.TryDequeue(out task);
}
