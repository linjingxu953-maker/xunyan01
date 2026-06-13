namespace DesktopMascot.Core.Tools;

/// <summary>
/// 工具链模式
/// </summary>
public enum ChainMode
{
    /// <summary>顺序执行</summary>
    Sequential,
    /// <summary>并行执行</summary>
    Parallel,
    /// <summary>条件执行</summary>
    Conditional
}

/// <summary>
/// 工具链步骤
/// </summary>
public class ChainStep
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string ToolName { get; set; } = string.Empty;
    public string ArgumentsTemplate { get; set; } = "{}";
    public string? Condition { get; set; }
    public List<string> DependsOn { get; set; } = new();
    public Dictionary<string, string> OutputMapping { get; set; } = new();
    public bool ContinueOnError { get; set; } = false;
}

/// <summary>
/// 工具链定义
/// </summary>
public class ToolChain
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public ChainMode Mode { get; set; } = ChainMode.Sequential;
    public List<ChainStep> Steps { get; set; } = new();
    public Dictionary<string, object> Variables { get; set; } = new();
}

/// <summary>
/// 工具链结果
/// </summary>
public class ChainResult
{
    public string ChainId { get; set; } = string.Empty;
    public bool Success { get; set; }
    public List<StepResult> StepResults { get; set; } = new();
    public Dictionary<string, object> Outputs { get; set; } = new();
    public string? Error { get; set; }
    public TimeSpan TotalDuration { get; set; }
}

/// <summary>
/// 步骤结果
/// </summary>
public class StepResult
{
    public string StepId { get; set; } = string.Empty;
    public string ToolName { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string? Input { get; set; }
    public string? Output { get; set; }
    public string? Error { get; set; }
    public TimeSpan Duration { get; set; }
}

/// <summary>
/// 工具链构建器
/// </summary>
public class ToolChainBuilder
{
    private readonly ToolChain _chain = new();

    public ToolChainBuilder(string name)
    {
        _chain.Name = name;
    }

    public ToolChainBuilder WithDescription(string description)
    {
        _chain.Description = description;
        return this;
    }

    public ToolChainBuilder WithMode(ChainMode mode)
    {
        _chain.Mode = mode;
        return this;
    }

    public ToolChainBuilder WithVariable(string key, object value)
    {
        _chain.Variables[key] = value;
        return this;
    }

    public ToolChainBuilder AddStep(string toolName, string arguments = "{}", bool continueOnError = false)
    {
        _chain.Steps.Add(new ChainStep
        {
            ToolName = toolName,
            ArgumentsTemplate = arguments,
            ContinueOnError = continueOnError
        });
        return this;
    }

    public ToolChainBuilder AddStepWithCondition(string toolName, string condition, string arguments = "{}")
    {
        _chain.Steps.Add(new ChainStep
        {
            ToolName = toolName,
            ArgumentsTemplate = arguments,
            Condition = condition
        });
        return this;
    }

    public ToolChainBuilder AddParallelSteps(params string[] toolNames)
    {
        foreach (var toolName in toolNames)
        {
            _chain.Steps.Add(new ChainStep
            {
                ToolName = toolName,
                ArgumentsTemplate = "{}"
            });
        }
        _chain.Mode = ChainMode.Parallel;
        return this;
    }

    public ToolChain Build()
    {
        return _chain;
    }
}
