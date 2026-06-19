using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DesktopMascot.Core.Tools;

/// <summary>
/// Goal 循环引擎 — 自主目标驱动执行 + 裁判评估 + 预算控制
/// 参考 Scream Code 的 Goal Loop 设计
/// </summary>
public class GoalEngine
{
    private readonly ILogger _logger;
    private GoalDefinition? _currentGoal;

    public GoalDefinition? CurrentGoal => _currentGoal;
    public bool IsRunning => _currentGoal?.Status == GoalStatus.Running;

    public event Action<GoalDefinition>? GoalCompleted;
    public event Action<GoalDefinition, GoalIteration>? IterationCompleted;

    public GoalEngine() : this(new NullLogger<GoalEngine>()) { }

    public GoalEngine(ILogger logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// 创建新目标
    /// </summary>
    public GoalDefinition CreateGoal(string description, string successCriteria,
        int maxIterations = 10, int maxTokens = 50000, int maxTimeSeconds = 300)
    {
        _currentGoal = new GoalDefinition
        {
            Description = description,
            SuccessCriteria = successCriteria,
            MaxIterations = maxIterations,
            MaxTokens = maxTokens,
            MaxTimeSeconds = maxTimeSeconds,
            Status = GoalStatus.Pending,
            StartedAt = DateTime.UtcNow
        };

        return _currentGoal;
    }

    /// <summary>
    /// 执行一轮 Goal 迭代
    /// </summary>
    public async Task<GoalIterationResult> ExecuteIterationAsync(
        Func<GoalDefinition, string, CancellationToken, Task<string>> agentExecute,
        Func<GoalDefinition, CancellationToken, Task<GoalJudgeResult>> judgeEvaluate,
        CancellationToken ct = default)
    {
        if (_currentGoal == null)
            return new GoalIterationResult { Success = false, Error = "未设置目标" };

        if (_currentGoal.Status == GoalStatus.Completed || _currentGoal.Status == GoalStatus.Failed)
            return new GoalIterationResult { Success = false, Error = "目标已结束" };

        // 预算检查
        var budgetCheck = CheckBudget();
        if (budgetCheck != null)
        {
            _currentGoal.Status = GoalStatus.Failed;
            _currentGoal.FinalResult = budgetCheck;
            GoalCompleted?.Invoke(_currentGoal);
            return new GoalIterationResult { Success = false, Error = budgetCheck, GoalCompleted = true };
        }

        _currentGoal.Status = GoalStatus.Running;
        _currentGoal.CurrentIteration++;
        var round = _currentGoal.CurrentIteration;

        _logger.LogInformation($"[Goal] 第 {round}/{_currentGoal.MaxIterations} 轮执行");

        try
        {
            // 1. 构建当前轮次的上下文
            var context = BuildIterationContext();

            // 2. Agent 执行
            var startTime = DateTime.UtcNow;
            var result = await agentExecute(_currentGoal, context, ct);
            var elapsed = (DateTime.UtcNow - startTime).TotalSeconds;

            // 3. 估算 Token（简化：按字符数/4）
            var estimatedTokens = result.Length / 4;
            _currentGoal.TokensUsed += estimatedTokens;

            // 4. 记录迭代
            var iteration = new GoalIteration
            {
                Round = round,
                Timestamp = DateTime.UtcNow,
                Action = context,
                Result = result,
                Success = true,
                TokensUsed = estimatedTokens
            };
            _currentGoal.Iterations.Add(iteration);
            IterationCompleted?.Invoke(_currentGoal, iteration);

            // 5. 裁判评估
            var judgeResult = await judgeEvaluate(_currentGoal, ct);
            _currentGoal.JudgeScore = judgeResult.Score;
            _currentGoal.JudgeComment = judgeResult.Comment;

            _logger.LogInformation($"[Goal] 第 {round} 轮完成，裁判评分：{judgeResult.Score}/100");

            // 6. 判断目标是否达成
            if (judgeResult.Score >= 80)
            {
                _currentGoal.Status = GoalStatus.Completed;
                _currentGoal.FinalResult = result;
                _currentGoal.CompletedAt = DateTime.UtcNow;
                GoalCompleted?.Invoke(_currentGoal);

                return new GoalIterationResult
                {
                    Success = true,
                    Result = result,
                    JudgeScore = judgeResult.Score,
                    JudgeComment = judgeResult.Comment,
                    GoalCompleted = true
                };
            }

            return new GoalIterationResult
            {
                Success = true,
                Result = result,
                JudgeScore = judgeResult.Score,
                JudgeComment = judgeResult.Comment
            };
        }
        catch (Exception ex)
        {
            _currentGoal.Iterations.Add(new GoalIteration
            {
                Round = round,
                Timestamp = DateTime.UtcNow,
                Success = false,
                Error = ex.Message
            });

            _logger.LogError($"[Goal] 第 {round} 轮执行失败：{ex.Message}");
            return new GoalIterationResult { Success = false, Error = ex.Message };
        }
    }

    /// <summary>
    /// 暂停目标
    /// </summary>
    public void Pause()
    {
        if (_currentGoal?.Status == GoalStatus.Running)
            _currentGoal.Status = GoalStatus.Paused;
    }

    /// <summary>
    /// 恢复目标
    /// </summary>
    public void Resume()
    {
        if (_currentGoal?.Status == GoalStatus.Paused)
            _currentGoal.Status = GoalStatus.Running;
    }

    /// <summary>
    /// 取消目标
    /// </summary>
    public void Cancel()
    {
        if (_currentGoal != null && _currentGoal.Status is GoalStatus.Running or GoalStatus.Paused)
        {
            _currentGoal.Status = GoalStatus.Cancelled;
            _currentGoal.CompletedAt = DateTime.UtcNow;
            GoalCompleted?.Invoke(_currentGoal);
        }
    }

    /// <summary>
    /// 获取目标摘要
    /// </summary>
    public string GetSummary()
    {
        if (_currentGoal == null) return "无活动目标";

        var sb = new StringBuilder();
        sb.AppendLine($"目标：{_currentGoal.Description}");
        sb.AppendLine($"状态：{_currentGoal.Status}");
        sb.AppendLine($"轮次：{_currentGoal.CurrentIteration}/{_currentGoal.MaxIterations}");
        sb.AppendLine($"Token：{_currentGoal.TokensUsed}/{_currentGoal.MaxTokens}");
        sb.AppendLine($"耗时：{(DateTime.UtcNow - _currentGoal.StartedAt).TotalSeconds:F0}s / {_currentGoal.MaxTimeSeconds}s");
        sb.AppendLine($"裁判评分：{_currentGoal.JudgeScore}/100");
        if (_currentGoal.JudgeComment != null)
            sb.AppendLine($"裁判评语：{_currentGoal.JudgeComment}");
        if (_currentGoal.FinalResult != null)
            sb.AppendLine($"结果：{_currentGoal.FinalResult[..Math.Min(200, _currentGoal.FinalResult.Length)]}");

        return sb.ToString();
    }

    private string BuildIterationContext()
    {
        if (_currentGoal == null) return "";

        var sb = new StringBuilder();
        sb.AppendLine($"## 目标（第 {_currentGoal.CurrentIteration} 轮）");
        sb.AppendLine($"目标：{_currentGoal.Description}");
        sb.AppendLine($"成功标准：{_currentGoal.SuccessCriteria}");
        sb.AppendLine($"剩余轮次：{_currentGoal.MaxIterations - _currentGoal.CurrentIteration}");
        sb.AppendLine();

        if (_currentGoal.Iterations.Count > 0)
        {
            sb.AppendLine("### 历史执行记录");
            foreach (var iter in _currentGoal.Iterations.TakeLast(3))
            {
                sb.AppendLine($"第 {iter.Round} 轮：{(iter.Success ? "成功" : "失败")}");
                if (iter.Result.Length > 100)
                    sb.AppendLine($"  结果：{iter.Result[..100]}...");
                else
                    sb.AppendLine($"  结果：{iter.Result}");
            }
            sb.AppendLine();
        }

        if (_currentGoal.JudgeScore > 0)
        {
            sb.AppendLine($"### 上轮裁判评分：{_currentGoal.JudgeScore}/100");
            if (_currentGoal.JudgeComment != null)
                sb.AppendLine($"评语：{_currentGoal.JudgeComment}");
        }

        return sb.ToString();
    }

    private string? CheckBudget()
    {
        if (_currentGoal == null) return null;

        if (_currentGoal.CurrentIteration >= _currentGoal.MaxIterations)
            return $"达到最大迭代次数（{_currentGoal.MaxIterations}）";

        if (_currentGoal.TokensUsed >= _currentGoal.MaxTokens)
            return $"达到最大 Token 数（{_currentGoal.MaxTokens}）";

        var elapsed = (DateTime.UtcNow - _currentGoal.StartedAt).TotalSeconds;
        if (elapsed >= _currentGoal.MaxTimeSeconds)
            return $"达到最大执行时间（{_currentGoal.MaxTimeSeconds}秒）";

        return null;
    }
}

/// <summary>
/// 裁判评估结果
/// </summary>
public class GoalJudgeResult
{
    /// <summary>评分 0-100</summary>
    public int Score { get; set; }

    /// <summary>评语</summary>
    public string Comment { get; set; } = "";

    /// <summary>是否达成目标</summary>
    public bool IsAchieved => Score >= 80;
}

/// <summary>
/// Goal 迭代结果
/// </summary>
public class GoalIterationResult
{
    public bool Success { get; set; }
    public string Result { get; set; } = "";
    public string? Error { get; set; }
    public int JudgeScore { get; set; }
    public string? JudgeComment { get; set; }
    public bool GoalCompleted { get; set; }
}
