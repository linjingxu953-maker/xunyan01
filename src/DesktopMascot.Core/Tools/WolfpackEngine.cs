using System.Collections.Concurrent;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DesktopMascot.Core.Tools;

/// <summary>
/// Wolfpack 引擎 — 多 Agent 并行执行框架
/// 参考 Scream Code Wolfpack 模式
/// </summary>
public class WolfpackEngine
{
    private readonly ILogger _logger;
    private readonly ConcurrentDictionary<string, WolfpackPack> _packs = new();
    private WolfpackPack? _currentPack;

    public WolfpackPack? CurrentPack => _currentPack;
    public bool IsRunning => _currentPack?.Status == WolfpackPackStatus.Running;

    public event Action<WolfpackPack>? PackCompleted;
    public event Action<WolfpackAgent>? AgentCompleted;

    public WolfpackEngine() : this(new NullLogger<WolfpackEngine>()) { }
    public WolfpackEngine(ILogger logger) { _logger = logger; }

    /// <summary>
    /// 创建任务包
    /// </summary>
    public WolfpackPack CreatePack(string name, string description, bool autoApprove = false)
    {
        var pack = new WolfpackPack
        {
            Name = name,
            Description = description,
            AutoApprove = autoApprove
        };

        _packs[pack.Id] = pack;
        _currentPack = pack;
        return pack;
    }

    /// <summary>
    /// 添加子 Agent 到任务包
    /// </summary>
    public WolfpackAgent AddAgent(WolfpackPack pack, WolfpackAgentType type, string task,
        string? context = null, List<string>? dependsOn = null)
    {
        var agent = new WolfpackAgent
        {
            Type = type,
            Task = task,
            Context = context,
            DependsOn = dependsOn ?? new()
        };

        pack.Agents.Add(agent);
        return agent;
    }

    /// <summary>
    /// 自动拆解任务为子 Agent
    /// </summary>
    public WolfpackPack AutoDecompose(string taskDescription)
    {
        var pack = CreatePack($"任务：{taskDescription[..Math.Min(50, taskDescription.Length)]}", taskDescription);

        // 自动拆解为 5 类子 Agent
        AddAgent(pack, WolfpackAgentType.Plan, "分析任务需求，制定执行计划",
            $"任务：{taskDescription}");
        AddAgent(pack, WolfpackAgentType.Explore, "调研相关代码和文档",
            $"任务：{taskDescription}", new() { pack.Agents[0].Id });
        AddAgent(pack, WolfpackAgentType.Coder, "编写核心代码实现",
            $"任务：{taskDescription}", new() { pack.Agents[1].Id });
        AddAgent(pack, WolfpackAgentType.Verify, "验证代码正确性和测试",
            $"任务：{taskDescription}", new() { pack.Agents[2].Id });
        AddAgent(pack, WolfpackAgentType.Writer, "编写文档和注释",
            $"任务：{taskDescription}", new() { pack.Agents[2].Id });

        return pack;
    }

    /// <summary>
    /// 执行任务包（支持并行 + 依赖）
    /// </summary>
    public async Task<WolfpackPackResult> ExecutePackAsync(
        WolfpackPack pack,
        Func<WolfpackAgent, CancellationToken, Task<string>> agentExecutor,
        int maxConcurrency = 5,
        CancellationToken ct = default)
    {
        pack.Status = WolfpackPackStatus.Running;
        var startTime = DateTime.UtcNow;

        _logger.LogInformation($"[Wolfpack] 开始执行任务包：{pack.Name}（{pack.Agents.Count} 个子 Agent）");

        var semaphore = new SemaphoreSlim(maxConcurrency);
        var tasks = new List<Task>();

        foreach (var agent in pack.Agents)
        {
            tasks.Add(Task.Run(async () =>
            {
                await semaphore.WaitAsync(ct);
                try
                {
                    // 检查依赖
                    if (agent.DependsOn.Count > 0)
                    {
                        var depsComplete = pack.Agents
                            .Where(a => agent.DependsOn.Contains(a.Id))
                            .All(a => a.Status == WolfpackAgentStatus.Completed);

                        if (!depsComplete)
                        {
                            agent.Status = WolfpackAgentStatus.Blocked;
                            agent.Error = "依赖未完成";
                            return;
                        }
                    }

                    agent.Status = WolfpackAgentStatus.Running;
                    _logger.LogInformation($"[Wolfpack] [{agent.Type}] 开始：{agent.Task[..Math.Min(50, agent.Task.Length)]}");

                    var result = await agentExecutor(agent, ct);

                    agent.Result = result;
                    agent.Status = WolfpackAgentStatus.Completed;
                    agent.CompletedAt = DateTime.UtcNow;
                    _logger.LogInformation($"[Wolfpack] [{agent.Type}] 完成");

                    AgentCompleted?.Invoke(agent);
                }
                catch (Exception ex)
                {
                    agent.Status = WolfpackAgentStatus.Failed;
                    agent.Error = ex.Message;
                    _logger.LogError($"[Wolfpack] [{agent.Type}] 失败：{ex.Message}");
                }
                finally
                {
                    semaphore.Release();
                }
            }, ct));
        }

        await Task.WhenAll(tasks);

        // 汇总结果
        var completed = pack.Agents.Count(a => a.Status == WolfpackAgentStatus.Completed);
        var failed = pack.Agents.Count(a => a.Status == WolfpackAgentStatus.Failed);
        var blocked = pack.Agents.Count(a => a.Status == WolfpackAgentStatus.Blocked);

        pack.Status = failed > 0
            ? WolfpackPackStatus.Failed
            : completed == pack.Agents.Count
                ? WolfpackPackStatus.Completed
                : WolfpackPackStatus.PartiallyCompleted;

        pack.CompletedAt = DateTime.UtcNow;

        var elapsed = (DateTime.UtcNow - startTime).TotalSeconds;
        _logger.LogInformation($"[Wolfpack] 任务包完成：{pack.Name}（{completed}成功/{failed}失败/{blocked}阻塞，耗时 {elapsed:F1}s）");

        PackCompleted?.Invoke(pack);

        return new WolfpackPackResult
        {
            PackId = pack.Id,
            Status = pack.Status,
            Completed = completed,
            Failed = failed,
            Blocked = blocked,
            DurationSeconds = elapsed,
            Results = pack.Agents
                .Where(a => a.Status == WolfpackAgentStatus.Completed)
                .Select(a => new { a.Type, a.Task, a.Result })
                .ToList()
        };
    }

    /// <summary>
    /// 获取任务包状态
    /// </summary>
    public string GetPackStatus(WolfpackPack pack)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"任务包：{pack.Name}");
        sb.AppendLine($"状态：{pack.Status}");
        sb.AppendLine($"子 Agent 数：{pack.Agents.Count}");
        sb.AppendLine();

        foreach (var agent in pack.Agents)
        {
            var icon = agent.Status switch
            {
                WolfpackAgentStatus.Completed => "✅",
                WolfpackAgentStatus.Failed => "❌",
                WolfpackAgentStatus.Running => "🔄",
                WolfpackAgentStatus.Blocked => "⏸️",
                _ => "⏳"
            };
            sb.AppendLine($"  {icon} [{agent.Type}] {agent.Task[..Math.Min(40, agent.Task.Length)]}");
            if (agent.Dependents.Count > 0)
                sb.AppendLine($"    依赖：{string.Join(", ", agent.Dependents)}");
        }

        return sb.ToString();
    }
}

public class WolfpackPackResult
{
    public string PackId { get; set; } = "";
    public WolfpackPackStatus Status { get; set; }
    public int Completed { get; set; }
    public int Failed { get; set; }
    public int Blocked { get; set; }
    public double DurationSeconds { get; set; }
    public object Results { get; set; } = new();
}
