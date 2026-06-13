using System.Collections.Concurrent;
using DesktopMascot.Core.Tools;

namespace DesktopMascot.Core.Workflow;

/// <summary>
/// 支持持久化的工作流引擎
/// </summary>
public class PersistentWorkflowEngine : IWorkflowEngine
{
    private readonly IToolRegistry _toolRegistry;
    private readonly IWorkflowStore _store;
    private readonly ConcurrentDictionary<string, WorkflowInstance> _cache = new();

    public event Action<WorkflowEvent>? WorkflowEventOccurred;

    public PersistentWorkflowEngine(IToolRegistry toolRegistry, IWorkflowStore store)
    {
        _toolRegistry = toolRegistry;
        _store = store;
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

        foreach (var step in definition.Steps.OrderBy(s => s.Order))
        {
            workflow.Steps.Add(new StepInstance
            {
                StepId = step.Id,
                Name = step.Name,
                Status = StepStatus.Pending
            });
            workflow.Variables[$"step_{step.Id}"] = step;
        }

        _cache[workflow.Id] = workflow;
        return workflow;
    }

    public async Task<WorkflowInstance> ExecuteAsync(WorkflowInstance workflow, CancellationToken ct = default)
    {
        workflow.Status = WorkflowStatus.Running;
        workflow.StartedAt = DateTime.UtcNow;
        await _store.SaveAsync(workflow, ct);

        RaiseEvent(workflow.Id, null, "started", $"工作流 {workflow.Name} 开始执行");

        try
        {
            foreach (var step in workflow.Steps)
            {
                if (ct.IsCancellationRequested)
                {
                    workflow.Status = WorkflowStatus.Cancelled;
                    break;
                }

                await ExecuteStepAsync(workflow, step, ct);
                await _store.SaveAsync(workflow, ct);

                if (step.Status == StepStatus.Failed)
                {
                    workflow.Status = WorkflowStatus.Failed;
                    workflow.ErrorMessage = $"步骤 {step.Name} 失败: {step.Error}";
                    workflow.CompletedAt = DateTime.UtcNow;
                    await _store.SaveAsync(workflow, ct);
                    RaiseEvent(workflow.Id, null, "failed", workflow.ErrorMessage);
                    return workflow;
                }
            }

            workflow.Status = WorkflowStatus.Completed;
            workflow.CompletedAt = DateTime.UtcNow;
            await _store.SaveAsync(workflow, ct);
        }
        catch (Exception ex)
        {
            workflow.Status = WorkflowStatus.Failed;
            workflow.ErrorMessage = ex.Message;
            workflow.CompletedAt = DateTime.UtcNow;
            await _store.SaveAsync(workflow, ct);
        }

        RaiseEvent(workflow.Id, null, "completed", $"工作流 {workflow.Name} 执行完成");
        return workflow;
    }

    public async Task<WorkflowInstance?> ResumeFromCheckpointAsync(string workflowId, CancellationToken ct = default)
    {
        var workflow = await _store.GetByIdAsync(workflowId, ct);
        if (workflow == null)
            return null;

        _cache[workflowId] = workflow;

        // 找到第一个未完成的步骤继续执行
        var nextStep = workflow.Steps.FirstOrDefault(s => 
            s.Status == StepStatus.Pending || s.Status == StepStatus.Running);
        
        if (nextStep != null)
        {
            workflow.Status = WorkflowStatus.Running;
            await ExecuteAsync(workflow, ct);
        }

        return workflow;
    }

    private async Task ExecuteStepAsync(WorkflowInstance workflow, StepInstance step, CancellationToken ct)
    {
        step.Status = StepStatus.Running;
        step.StartedAt = DateTime.UtcNow;

        RaiseEvent(workflow.Id, step.StepId, "step_started", $"步骤 {step.Name} 开始执行");

        try
        {
            var definition = GetStepDefinition(workflow, step);
            if (definition == null)
            {
                step.Status = StepStatus.Failed;
                step.Error = $"步骤定义不存在: {step.StepId}";
                return;
            }

            var tool = _toolRegistry.GetTool(definition.ToolName);
            if (tool == null)
            {
                step.Status = StepStatus.Failed;
                step.Error = $"工具不存在: {definition.ToolName}";
                return;
            }

            var arguments = ResolveArguments(definition.ArgumentsTemplate, workflow.Variables);
            step.Input = arguments;

            var request = new ToolCallRequest
            {
                ToolName = definition.ToolName,
                Arguments = arguments,
                TaskId = workflow.Id
            };

            var response = await _toolRegistry.ExecuteAsync(request, ct);

            if (response.Success)
            {
                step.Output = response.Result;
                step.Status = StepStatus.Completed;

                foreach (var mapping in definition.OutputMapping)
                {
                    workflow.Variables[mapping.Value] = response.Result ?? "";
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

    private WorkflowStep? GetStepDefinition(WorkflowInstance workflow, StepInstance step)
    {
        if (workflow.Variables.TryGetValue($"step_{step.StepId}", out var stepDefObj) && stepDefObj is WorkflowStep stepDef)
        {
            return stepDef;
        }

        return new WorkflowStep
        {
            Id = step.StepId,
            Name = step.Name,
            ToolName = "get_current_time",
            ArgumentsTemplate = "{}"
        };
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

    public async Task PauseAsync(string workflowId, CancellationToken ct = default)
    {
        if (_cache.TryGetValue(workflowId, out var workflow))
        {
            workflow.Status = WorkflowStatus.Paused;
            await _store.SaveAsync(workflow, ct);
        }
    }

    public async Task ResumeAsync(string workflowId, CancellationToken ct = default)
    {
        if (_cache.TryGetValue(workflowId, out var workflow))
        {
            workflow.Status = WorkflowStatus.Running;
            await _store.SaveAsync(workflow, ct);
        }
    }

    public async Task CancelAsync(string workflowId, CancellationToken ct = default)
    {
        if (_cache.TryGetValue(workflowId, out var workflow))
        {
            workflow.Status = WorkflowStatus.Cancelled;
            workflow.CompletedAt = DateTime.UtcNow;
            await _store.SaveAsync(workflow, ct);
        }
    }

    public async Task ApproveStepAsync(string workflowId, string stepId, CancellationToken ct = default)
    {
        if (!_cache.TryGetValue(workflowId, out var workflow))
            throw new InvalidOperationException($"工作流不存在: {workflowId}");

        var step = workflow.Steps.FirstOrDefault(s => s.StepId == stepId);
        if (step == null)
            throw new InvalidOperationException($"步骤不存在: {stepId}");

        if (step.Status != StepStatus.WaitingForApproval)
            throw new InvalidOperationException($"步骤 {step.Name} 不在等待审批状态");

        step.Status = StepStatus.Running;
        RaiseEvent(workflowId, stepId, "step_approved", $"步骤 {step.Name} 已批准");

        workflow.Status = WorkflowStatus.Running;
        await _store.SaveAsync(workflow, ct);
        await ExecuteAsync(workflow, ct);
    }

    public async Task RejectStepAsync(string workflowId, string stepId, string? reason = null, CancellationToken ct = default)
    {
        if (!_cache.TryGetValue(workflowId, out var workflow))
            throw new InvalidOperationException($"工作流不存在: {workflowId}");

        var step = workflow.Steps.FirstOrDefault(s => s.StepId == stepId);
        if (step == null)
            throw new InvalidOperationException($"步骤不存在: {stepId}");

        if (step.Status != StepStatus.WaitingForApproval)
            throw new InvalidOperationException($"步骤 {step.Name} 不在等待审批状态");

        step.Status = StepStatus.Skipped;
        step.Error = reason ?? "用户拒绝执行";

        RaiseEvent(workflowId, stepId, "step_rejected", $"步骤 {step.Name} 被拒绝");

        await _store.SaveAsync(workflow, ct);
    }

    public WorkflowInstance? GetWorkflow(string workflowId)
    {
        return _cache.TryGetValue(workflowId, out var workflow) ? workflow : null;
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
