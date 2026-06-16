using System.Collections.Concurrent;
using DesktopMascot.Core.Tools;

namespace DesktopMascot.Core.Workflow;

/// <summary>
/// 工作流引擎接口
/// </summary>
public interface IWorkflowEngine
{
    /// <summary>创建工作流</summary>
    WorkflowInstance CreateWorkflow(WorkflowDefinition definition, Dictionary<string, object>? variables = null);
    
    /// <summary>执行工作流</summary>
    Task<WorkflowInstance> ExecuteAsync(WorkflowInstance workflow, CancellationToken ct = default);
    
    /// <summary>暂停工作流</summary>
    Task PauseAsync(string workflowId, CancellationToken ct = default);
    
    /// <summary>恢复工作流</summary>
    Task ResumeAsync(string workflowId, CancellationToken ct = default);
    
    /// <summary>取消工作流</summary>
    Task CancelAsync(string workflowId, CancellationToken ct = default);

    /// <summary>批准工作流步骤</summary>
    Task ApproveStepAsync(string workflowId, string stepId, CancellationToken ct = default);

    /// <summary>拒绝工作流步骤</summary>
    Task RejectStepAsync(string workflowId, string stepId, string? reason = null, CancellationToken ct = default);

    /// <summary>获取工作流状态</summary>
    WorkflowInstance? GetWorkflow(string workflowId);
    
    /// <summary>工作流事件</summary>
    event Action<WorkflowEvent>? WorkflowEventOccurred;
}

/// <summary>
/// 工作流引擎实现
/// </summary>
public class WorkflowEngine : IWorkflowEngine
{
    private readonly IToolRegistry _toolRegistry;
    private readonly ConcurrentDictionary<string, WorkflowInstance> _workflows = new();

    public event Action<WorkflowEvent>? WorkflowEventOccurred;

    public WorkflowEngine(IToolRegistry toolRegistry)
    {
        _toolRegistry = toolRegistry;
    }

    public WorkflowInstance CreateWorkflow(WorkflowDefinition definition, Dictionary<string, object>? variables = null)
    {
        var workflow = new WorkflowInstance
        {
            Id = Guid.NewGuid().ToString("N"),
            DefinitionId = definition.Id,
            Name = definition.Name,
            Variables = variables ?? new Dictionary<string, object>()
        };

        // 创建步骤实例（深拷贝关键字段，避免后续依赖定义对象）
        foreach (var step in definition.Steps.OrderBy(s => s.Order))
        {
            workflow.Steps.Add(new StepInstance
            {
                StepId = step.Id,
                Name = step.Name,
                ToolName = step.ToolName,
                ArgumentsTemplate = step.ArgumentsTemplate,
                RequiresApproval = step.RequiresApproval,
                OutputMappingKeys = step.OutputMapping.Keys.ToList(),
                OutputMappingValues = step.OutputMapping.Values.ToList(),
                Status = StepStatus.Pending
            });
        }

        _workflows[workflow.Id] = workflow;
        return workflow;
    }

    public async Task<WorkflowInstance> ExecuteAsync(WorkflowInstance workflow, CancellationToken ct = default)
    {
        workflow.Status = WorkflowStatus.Running;
        workflow.StartedAt = DateTime.UtcNow;

        RaiseEvent(workflow.Id, null, "started", $"工作流 {workflow.Name} 开始执行");

        try
        {
            // 按顺序执行步骤（简化版本，不支持并行）
            foreach (var step in workflow.Steps)
            {
                if (ct.IsCancellationRequested)
                {
                    workflow.Status = WorkflowStatus.Cancelled;
                    break;
                }

                await ExecuteStepAsync(workflow, step, ct);

                if (step.Status == StepStatus.Failed)
                {
                    workflow.Status = WorkflowStatus.Failed;
                    workflow.ErrorMessage = $"步骤 {step.Name} 失败: {step.Error}";
                    workflow.CompletedAt = DateTime.UtcNow;
                    RaiseEvent(workflow.Id, null, "failed", workflow.ErrorMessage);
                    return workflow;
                }

                if (step.Status == StepStatus.WaitingForApproval)
                {
                    workflow.Status = WorkflowStatus.Paused;
                    RaiseEvent(workflow.Id, step.StepId, "paused_for_approval",
                        $"工作流暂停，等待步骤 {step.Name} 审批");
                    return workflow;
                }
            }

            workflow.Status = WorkflowStatus.Completed;
            workflow.CompletedAt = DateTime.UtcNow;
        }
        catch (Exception ex)
        {
            workflow.Status = WorkflowStatus.Failed;
            workflow.ErrorMessage = ex.Message;
        }

        RaiseEvent(workflow.Id, null, "completed", $"工作流 {workflow.Name} 执行完成");
        return workflow;
    }

    private async Task ExecuteStepAsync(WorkflowInstance workflow, StepInstance step, CancellationToken ct)
    {
        step.Status = StepStatus.Running;
        step.StartedAt = DateTime.UtcNow;

        RaiseEvent(workflow.Id, step.StepId, "step_started", $"步骤 {step.Name} 开始执行");

        try
        {
            // 使用步骤上直接存储的工具名和参数模板
            var toolName = step.ToolName;
            if (string.IsNullOrWhiteSpace(toolName))
            {
                step.Status = StepStatus.Failed;
                step.Error = $"步骤缺少工具名: {step.StepId}";
                return;
            }

            var tool = _toolRegistry.GetTool(toolName);
            if (tool == null)
            {
                step.Status = StepStatus.Failed;
                step.Error = $"工具不存在: {toolName}";
                return;
            }

            // 准备参数（替换模板变量）
            var arguments = ResolveArguments(step.ArgumentsTemplate, workflow.Variables);
            step.Input = arguments;

            // 检查是否需要确认（已批准过的步骤不再重复请求确认，防止死循环）
            if (step.RequiresApproval && !step.IsApproved)
            {
                step.Status = StepStatus.WaitingForApproval;
                RaiseEvent(workflow.Id, step.StepId, "waiting_approval", $"步骤 {step.Name} 等待确认");
                // 审批步骤在此暂停，由外部 ApproveStepAsync/RejectStepAsync 驱动继续
                return;
            }

            // 执行工具
            var request = new ToolCallRequest
            {
                ToolName = toolName,
                Arguments = arguments,
                TaskId = workflow.Id
            };

            var response = await _toolRegistry.ExecuteAsync(request, ct);

            if (response.Success)
            {
                step.Output = response.Result;
                step.Status = StepStatus.Completed;

                // 映射输出到变量
                for (int i = 0; i < step.OutputMappingKeys.Count && i < step.OutputMappingValues.Count; i++)
                {
                    workflow.Variables[step.OutputMappingValues[i]] = response.Result ?? "";
                }
            }
            else
            {
                step.Status = StepStatus.Failed;
                step.Error = response.Error;
            }
        }
        catch (Exception ex)
        {
            step.Status = StepStatus.Failed;
            step.Error = ex.Message;
        }

        step.CompletedAt = DateTime.UtcNow;

        RaiseEvent(workflow.Id, step.StepId, 
            step.Status == StepStatus.Completed ? "step_completed" : "step_failed",
            $"步骤 {step.Name} {step.Status}");
    }

    private string ResolveArguments(string template, Dictionary<string, object> variables)
    {
        var result = template;
        foreach (var kvp in variables)
        {
            result = result.Replace($"{{{kvp.Key}}}", kvp.Value?.ToString() ?? "");
        }
        return result;
    }

    public Task PauseAsync(string workflowId, CancellationToken ct = default)
    {
        if (_workflows.TryGetValue(workflowId, out var workflow))
        {
            workflow.Status = WorkflowStatus.Paused;
        }
        return Task.CompletedTask;
    }

    public Task ResumeAsync(string workflowId, CancellationToken ct = default)
    {
        if (_workflows.TryGetValue(workflowId, out var workflow))
        {
            workflow.Status = WorkflowStatus.Running;
        }
        return Task.CompletedTask;
    }

    public Task CancelAsync(string workflowId, CancellationToken ct = default)
    {
        if (_workflows.TryGetValue(workflowId, out var workflow))
        {
            workflow.Status = WorkflowStatus.Cancelled;
        }
        return Task.CompletedTask;
    }

    public async Task ApproveStepAsync(string workflowId, string stepId, CancellationToken ct = default)
    {
        if (!_workflows.TryGetValue(workflowId, out var workflow))
            throw new InvalidOperationException($"工作流不存在: {workflowId}");

        var step = workflow.Steps.FirstOrDefault(s => s.StepId == stepId);
        if (step == null)
            throw new InvalidOperationException($"步骤不存在: {stepId}");

        if (step.Status != StepStatus.WaitingForApproval)
            throw new InvalidOperationException($"步骤 {step.Name} 不在等待审批状态");

        // 批准后继续执行该步骤
        step.IsApproved = true;
        step.Status = StepStatus.Running;
        RaiseEvent(workflowId, stepId, "step_approved", $"步骤 {step.Name} 已批准");

        // 恢复工作流执行
        workflow.Status = WorkflowStatus.Running;
        await ExecuteStepAsync(workflow, step, ct);

        if (step.Status == StepStatus.Completed)
        {
            // 继续执行后续步骤
            await ExecuteRemainingStepsAsync(workflow, step, ct);
        }
    }

    public async Task RejectStepAsync(string workflowId, string stepId, string? reason = null, CancellationToken ct = default)
    {
        if (!_workflows.TryGetValue(workflowId, out var workflow))
            throw new InvalidOperationException($"工作流不存在: {workflowId}");

        var step = workflow.Steps.FirstOrDefault(s => s.StepId == stepId);
        if (step == null)
            throw new InvalidOperationException($"步骤不存在: {stepId}");

        if (step.Status != StepStatus.WaitingForApproval)
            throw new InvalidOperationException($"步骤 {step.Name} 不在等待审批状态");

        step.Status = StepStatus.Failed;
        step.Error = reason ?? "用户拒绝执行";
        workflow.Status = WorkflowStatus.Failed;
        workflow.ErrorMessage = $"步骤 {step.Name} 被拒绝: {step.Error}";
        workflow.CompletedAt = DateTime.UtcNow;

        RaiseEvent(workflowId, stepId, "step_rejected", $"步骤 {step.Name} 被拒绝");
    }

    /// <summary>
    /// 继续执行审批通过后的剩余步骤
    /// </summary>
    private async Task ExecuteRemainingStepsAsync(WorkflowInstance workflow, StepInstance completedStep, CancellationToken ct)
    {
        var startProcessing = false;
        foreach (var step in workflow.Steps)
        {
            if (step.StepId == completedStep.StepId)
            {
                startProcessing = true;
                continue;
            }

            if (!startProcessing || step.Status != StepStatus.Pending)
                continue;

            if (ct.IsCancellationRequested)
            {
                workflow.Status = WorkflowStatus.Cancelled;
                return;
            }

            await ExecuteStepAsync(workflow, step, ct);

            if (step.Status == StepStatus.Failed)
            {
                workflow.Status = WorkflowStatus.Failed;
                workflow.ErrorMessage = $"步骤 {step.Name} 失败: {step.Error}";
                workflow.CompletedAt = DateTime.UtcNow;
                return;
            }

            if (step.Status == StepStatus.WaitingForApproval)
            {
                workflow.Status = WorkflowStatus.Paused;
                return;
            }
        }

        workflow.Status = WorkflowStatus.Completed;
        workflow.CompletedAt = DateTime.UtcNow;
        RaiseEvent(workflow.Id, null, "completed", $"工作流 {workflow.Name} 执行完成");
    }

    public WorkflowInstance? GetWorkflow(string workflowId)
    {
        return _workflows.TryGetValue(workflowId, out var workflow) ? workflow : null;
    }

    private void RaiseEvent(string workflowId, string? stepId, string eventType, string message)
    {
        WorkflowEventOccurred?.Invoke(new WorkflowEvent
        {
            WorkflowId = workflowId,
            StepId = stepId,
            EventType = eventType,
            Message = message
        });
    }
}
