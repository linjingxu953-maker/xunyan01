using System.Diagnostics;
using System.Text.Json;

namespace DesktopMascot.Core.Tools;

/// <summary>
/// 工具链执行器接口
/// </summary>
public interface IToolChainExecutor
{
    /// <summary>执行工具链</summary>
    Task<ChainResult> ExecuteAsync(ToolChain chain, CancellationToken ct = default);
    
    /// <summary>执行单个步骤</summary>
    Task<StepResult> ExecuteStepAsync(ChainStep step, Dictionary<string, object> variables, CancellationToken ct = default);
    
    /// <summary>工具链事件</summary>
    event Action<ChainStep, StepResult>? StepCompleted;
}

/// <summary>
/// 工具链执行器实现
/// </summary>
public class ToolChainExecutor : IToolChainExecutor
{
    private readonly IToolRegistry _toolRegistry;

    public event Action<ChainStep, StepResult>? StepCompleted;

    public ToolChainExecutor(IToolRegistry toolRegistry)
    {
        _toolRegistry = toolRegistry;
    }

    public async Task<ChainResult> ExecuteAsync(ToolChain chain, CancellationToken ct = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = new ChainResult
        {
            ChainId = chain.Id,
            Outputs = new Dictionary<string, object>(chain.Variables)
        };

        try
        {
            switch (chain.Mode)
            {
                case ChainMode.Sequential:
                    await ExecuteSequentialAsync(chain, result, ct);
                    break;
                case ChainMode.Parallel:
                    await ExecuteParallelAsync(chain, result, ct);
                    break;
                case ChainMode.Conditional:
                    await ExecuteConditionalAsync(chain, result, ct);
                    break;
            }

            result.Success = result.StepResults.All(s => s.Success);
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Error = ex.Message;
        }

        stopwatch.Stop();
        result.TotalDuration = stopwatch.Elapsed;

        return result;
    }

    private async Task ExecuteSequentialAsync(ToolChain chain, ChainResult result, CancellationToken ct)
    {
        foreach (var step in chain.Steps)
        {
            if (ct.IsCancellationRequested)
                break;

            // 检查条件
            if (!string.IsNullOrEmpty(step.Condition))
            {
                if (!EvaluateCondition(step.Condition, result.Outputs))
                {
                    result.StepResults.Add(new StepResult
                    {
                        StepId = step.Id,
                        ToolName = step.ToolName,
                        Success = true,
                        Output = "跳过：条件不满足"
                    });
                    continue;
                }
            }

            var stepResult = await ExecuteStepAsync(step, result.Outputs, ct);
            result.StepResults.Add(stepResult);

            // 更新输出变量
            if (stepResult.Success)
            {
                foreach (var mapping in step.OutputMapping)
                {
                    result.Outputs[mapping.Value] = stepResult.Output ?? "";
                }
            }

            StepCompleted?.Invoke(step, stepResult);

            if (!stepResult.Success && !step.ContinueOnError)
                break;
        }
    }

    private async Task ExecuteParallelAsync(ToolChain chain, ChainResult result, CancellationToken ct)
    {
        var tasks = chain.Steps.Select(step => ExecuteStepAsync(step, result.Outputs, ct));
        var stepResults = await Task.WhenAll(tasks);

        result.StepResults.AddRange(stepResults);

        foreach (var stepResult in stepResults)
        {
            StepCompleted?.Invoke(
                chain.Steps.First(s => s.Id == stepResult.StepId),
                stepResult);
        }
    }

    private async Task ExecuteConditionalAsync(ToolChain chain, ChainResult result, CancellationToken ct)
    {
        foreach (var step in chain.Steps)
        {
            if (ct.IsCancellationRequested)
                break;

            if (!string.IsNullOrEmpty(step.Condition))
            {
                if (!EvaluateCondition(step.Condition, result.Outputs))
                    continue;
            }

            var stepResult = await ExecuteStepAsync(step, result.Outputs, ct);
            result.StepResults.Add(stepResult);
            StepCompleted?.Invoke(step, stepResult);
        }
    }

    public async Task<StepResult> ExecuteStepAsync(ChainStep step, Dictionary<string, object> variables, CancellationToken ct = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = new StepResult
        {
            StepId = step.Id,
            ToolName = step.ToolName
        };

        try
        {
            var tool = _toolRegistry.GetTool(step.ToolName);
            if (tool == null)
            {
                result.Success = false;
                result.Error = $"工具不存在: {step.ToolName}";
                return result;
            }

            var arguments = ResolveArguments(step.ArgumentsTemplate, variables);
            result.Input = arguments;

            var request = new ToolCallRequest
            {
                ToolName = step.ToolName,
                Arguments = arguments
            };

            var response = await _toolRegistry.ExecuteAsync(request, ct);

            result.Success = response.Success;
            result.Output = response.Result;
            result.Error = response.Error;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Error = ex.Message;
        }

        stopwatch.Stop();
        result.Duration = stopwatch.Elapsed;

        return result;
    }

    private string ResolveArguments(string template, Dictionary<string, object> variables)
    {
        var result = template;
        foreach (var kvp in variables)
        {
            if (kvp.Value != null)
                result = result.Replace($"{{{kvp.Key}}}", kvp.Value.ToString() ?? "");
        }
        return result;
    }

    private bool EvaluateCondition(string condition, Dictionary<string, object> variables)
    {
        // 简化的条件评估
        // 支持格式: "varName == value", "varName != value", "varName exists"
        try
        {
            if (condition.Contains("=="))
            {
                var parts = condition.Split("==", 2);
                var varName = parts[0].Trim();
                var expectedValue = parts[1].Trim().Trim('"');
                
                if (variables.TryGetValue(varName, out var actualValue))
                    return actualValue?.ToString() == expectedValue;
                return false;
            }
            
            if (condition.Contains("!="))
            {
                var parts = condition.Split("!=", 2);
                var varName = parts[0].Trim();
                var expectedValue = parts[1].Trim().Trim('"');
                
                if (variables.TryGetValue(varName, out var actualValue))
                    return actualValue?.ToString() != expectedValue;
                return true;
            }
            
            if (condition.EndsWith("exists"))
            {
                var varName = condition.Replace("exists", "").Trim();
                return variables.ContainsKey(varName);
            }
            
            return true;
        }
        catch
        {
            return false;
        }
    }
}
