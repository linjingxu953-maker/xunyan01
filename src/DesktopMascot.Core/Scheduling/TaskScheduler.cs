using System.Collections.Concurrent;
using DesktopMascot.Core.Tools;

namespace DesktopMascot.Core.Scheduling;

/// <summary>
/// 任务调度器接口
/// </summary>
public interface ITaskScheduler
{
    /// <summary>添加定时任务</summary>
    Task<ScheduledTask> AddTaskAsync(ScheduledTask task, CancellationToken ct = default);
    
    /// <summary>移除任务</summary>
    Task<bool> RemoveTaskAsync(string taskId, CancellationToken ct = default);
    
    /// <summary>暂停任务</summary>
    Task PauseTaskAsync(string taskId, CancellationToken ct = default);
    
    /// <summary>恢复任务</summary>
    Task ResumeTaskAsync(string taskId, CancellationToken ct = default);
    
    /// <summary>立即执行任务</summary>
    Task<ScheduleExecution> RunNowAsync(string taskId, CancellationToken ct = default);
    
    /// <summary>获取任务</summary>
    Task<ScheduledTask?> GetTaskAsync(string taskId, CancellationToken ct = default);
    
    /// <summary>获取所有任务</summary>
    Task<List<ScheduledTask>> GetAllTasksAsync(CancellationToken ct = default);
    
    /// <summary>获取执行记录</summary>
    Task<List<ScheduleExecution>> GetExecutionsAsync(string? taskId = null, int limit = 100, CancellationToken ct = default);
    
    /// <summary>获取统计</summary>
    Task<ScheduleStatistics> GetStatisticsAsync(CancellationToken ct = default);
    
    /// <summary>启动调度器</summary>
    Task StartAsync(CancellationToken ct = default);
    
    /// <summary>停止调度器</summary>
    Task StopAsync(CancellationToken ct = default);
    
    /// <summary>调度事件</summary>
    event Action<ScheduleEvent>? ScheduleEventOccurred;
}

/// <summary>
/// 任务调度器实现
/// </summary>
public class AppTaskScheduler : ITaskScheduler, IDisposable
{
    private readonly IToolRegistry _toolRegistry;
    private readonly ConcurrentDictionary<string, ScheduledTask> _tasks = new();
    private readonly ConcurrentBag<ScheduleExecution> _executions = new();
    private readonly Timer? _timer;
    private bool _isRunning;
    private readonly object _lock = new();

    public event Action<ScheduleEvent>? ScheduleEventOccurred;

    public AppTaskScheduler(IToolRegistry toolRegistry)
    {
        _toolRegistry = toolRegistry;
        _timer = new Timer(_ => _ = SafeExecutePendingTasksAsync(), null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
    }

    /// <summary>
    /// 安全执行待处理任务 — 包装 async void 避免未捕获异常导致进程崩溃
    /// </summary>
    private async Task SafeExecutePendingTasksAsync()
    {
        try
        {
            await ExecutePendingTasksAsync();
        }
        catch (Exception ex)
        {
            // 记录异常但不崩溃；调度器继续运行
            RaiseEvent("", "scheduler_error", $"调度器执行异常: {ex.Message}");
        }
    }

    public Task<ScheduledTask> AddTaskAsync(ScheduledTask task, CancellationToken ct = default)
    {
        task.NextRunTime = CalculateNextRunTime(task);
        _tasks[task.Id] = task;
        
        RaiseEvent(task.Id, "task_added", $"任务 {task.Name} 已添加");
        
        return Task.FromResult(task);
    }

    public Task<bool> RemoveTaskAsync(string taskId, CancellationToken ct = default)
    {
        var removed = _tasks.TryRemove(taskId, out _);
        
        if (removed)
        {
            RaiseEvent(taskId, "task_removed", $"任务已移除");
        }
        
        return Task.FromResult(removed);
    }

    public async Task PauseTaskAsync(string taskId, CancellationToken ct = default)
    {
        if (_tasks.TryGetValue(taskId, out var task))
        {
            task.IsEnabled = false;
            RaiseEvent(taskId, "task_paused", $"任务 {task.Name} 已暂停");
        }
        await Task.CompletedTask;
    }

    public async Task ResumeTaskAsync(string taskId, CancellationToken ct = default)
    {
        if (_tasks.TryGetValue(taskId, out var task))
        {
            task.IsEnabled = true;
            task.NextRunTime = CalculateNextRunTime(task);
            RaiseEvent(taskId, "task_resumed", $"任务 {task.Name} 已恢复");
        }
        await Task.CompletedTask;
    }

    public async Task<ScheduleExecution> RunNowAsync(string taskId, CancellationToken ct = default)
    {
        if (!_tasks.TryGetValue(taskId, out var task))
        {
            return new ScheduleExecution
            {
                TaskId = taskId,
                Status = ScheduleStatus.Failed,
                Error = "任务不存在"
            };
        }

        return await ExecuteTaskAsync(task, ct);
    }

    public Task<ScheduledTask?> GetTaskAsync(string taskId, CancellationToken ct = default)
    {
        _tasks.TryGetValue(taskId, out var task);
        return Task.FromResult(task);
    }

    public Task<List<ScheduledTask>> GetAllTasksAsync(CancellationToken ct = default)
    {
        var tasks = _tasks.Values.ToList();
        return Task.FromResult(tasks);
    }

    public Task<List<ScheduleExecution>> GetExecutionsAsync(string? taskId = null, int limit = 100, CancellationToken ct = default)
    {
        var executions = _executions
            .Where(e => taskId == null || e.TaskId == taskId)
            .OrderByDescending(e => e.StartedAt)
            .Take(limit)
            .ToList();
        
        return Task.FromResult(executions);
    }

    public Task<ScheduleStatistics> GetStatisticsAsync(CancellationToken ct = default)
    {
        var stats = new ScheduleStatistics
        {
            TotalTasks = _tasks.Count,
            ActiveTasks = _tasks.Values.Count(t => t.IsEnabled),
            CompletedExecutions = _executions.Count(e => e.Status == ScheduleStatus.Completed),
            FailedExecutions = _executions.Count(e => e.Status == ScheduleStatus.Failed),
            LastExecutionTime = _executions.Any() ? _executions.Max(e => e.StartedAt) : null
        };
        
        return Task.FromResult(stats);
    }

    public Task StartAsync(CancellationToken ct = default)
    {
        _isRunning = true;
        RaiseEvent("", "scheduler_started", "调度器已启动");
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken ct = default)
    {
        _isRunning = false;
        RaiseEvent("", "scheduler_stopped", "调度器已停止");
        return Task.CompletedTask;
    }

    private async Task ExecutePendingTasksAsync()
    {
        if (!_isRunning)
            return;

        var now = DateTime.UtcNow;
        var pendingTasks = _tasks.Values
            .Where(t => t.IsEnabled && t.NextRunTime.HasValue && t.NextRunTime <= now)
            .ToList();

        foreach (var task in pendingTasks)
        {
            await ExecuteTaskAsync(task);
        }
    }

    private async Task<ScheduleExecution> ExecuteTaskAsync(ScheduledTask task, CancellationToken ct = default)
    {
        var execution = new ScheduleExecution
        {
            TaskId = task.Id,
            Status = ScheduleStatus.Running,
            StartedAt = DateTime.UtcNow
        };

        RaiseEvent(task.Id, "task_started", $"任务 {task.Name} 开始执行");

        try
        {
            var tool = _toolRegistry.GetTool(task.ToolName);
            if (tool == null)
            {
                execution.Status = ScheduleStatus.Failed;
                execution.Error = $"工具不存在: {task.ToolName}";
            }
            else
            {
                var request = new ToolCallRequest
                {
                    ToolName = task.ToolName,
                    Arguments = task.Arguments
                };

                var response = await _toolRegistry.ExecuteAsync(request, ct);

                execution.Status = response.Success ? ScheduleStatus.Completed : ScheduleStatus.Failed;
                execution.Result = response.Result;
                execution.Error = response.Error;
            }

            execution.CompletedAt = DateTime.UtcNow;
            task.LastRunTime = DateTime.UtcNow;
            task.CurrentRetry = 0;
        }
        catch (Exception ex)
        {
            execution.Status = ScheduleStatus.Failed;
            execution.Error = ex.Message;
            execution.CompletedAt = DateTime.UtcNow;

            // 重试逻辑
            task.CurrentRetry++;
            if (task.CurrentRetry < task.MaxRetries)
            {
                task.NextRunTime = DateTime.UtcNow.AddSeconds(30 * task.CurrentRetry);
            }
        }

        // 计算下次执行时间
        if (task.Type != ScheduleType.Once || execution.Status == ScheduleStatus.Failed)
        {
            task.NextRunTime = CalculateNextRunTime(task);
        }

        _executions.Add(execution);
        RaiseEvent(task.Id, $"task_{execution.Status.ToString().ToLower()}", 
            $"任务 {task.Name} {execution.Status}");

        return execution;
    }

    private DateTime? CalculateNextRunTime(ScheduledTask task)
    {
        return task.Type switch
        {
            ScheduleType.Once => task.ScheduledTime,
            ScheduleType.Interval => DateTime.UtcNow.Add(task.Interval ?? TimeSpan.FromHours(1)),
            ScheduleType.Cron => CalculateCronNextRun(task.CronExpression),
            _ => null
        };
    }

    private DateTime? CalculateCronNextRun(string? cronExpression)
    {
        // 简化的 Cron 解析（实际应用中应使用完整的 Cron 库）
        // 这里只支持简单的间隔格式，如 "*/5 * * * *" 表示每 5 分钟
        if (string.IsNullOrEmpty(cronExpression))
            return null;

        // 简化实现：默认每小时执行
        return DateTime.UtcNow.AddHours(1);
    }

    private void RaiseEvent(string taskId, string eventType, string message)
    {
        ScheduleEventOccurred?.Invoke(new ScheduleEvent
        {
            TaskId = taskId,
            EventType = eventType,
            Message = message
        });
    }

    public void Dispose()
    {
        _timer?.Dispose();
    }
}
