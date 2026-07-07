using System.Diagnostics;
using DesktopMascot.Agent.Context;
using DesktopMascot.Agent.Models;
using DesktopMascot.Agent.Providers;
using DesktopMascot.Core.Tools;
using CoreToolDefinition = DesktopMascot.Core.Tools.ToolDefinition;
using LlmToolDefinition = DesktopMascot.Agent.Models.ToolDefinition;

namespace DesktopMascot.Agent.Tools;

/// <summary>
/// Agent tool registry. This is also the Core tool registry used by the permission pipeline.
/// </summary>
public class ToolRegistry : DesktopMascot.Core.Tools.IToolRegistry
{
    private readonly Dictionary<string, ITool> _tools = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<ToolCallLog> _callLogs = new();
    private readonly object _callLogLock = new();
    private IContextProvider? _contextProvider;
    private ILlmProvider? _llmProvider;

    public void Register(ITool tool)
    {
        _tools[tool.Name] = tool;
    }

    public async Task RegisterProviderAsync(IToolProvider provider, CancellationToken ct = default)
    {
        await provider.InitializeAsync(ct);
        foreach (var tool in provider.GetTools())
        {
            Register(tool);
        }
    }

    public void SetContextProvider(IContextProvider contextProvider)
    {
        _contextProvider = contextProvider;
    }

    public IContextProvider? GetContextProvider()
    {
        return _contextProvider;
    }

    public void SetLlmProvider(ILlmProvider llmProvider)
    {
        _llmProvider = llmProvider;
    }

    public ILlmProvider? GetLlmProvider()
    {
        return _llmProvider;
    }

    public ITool? GetTool(string name)
    {
        return _tools.TryGetValue(name, out var tool) ? tool : null;
    }

    public bool RequiresConfirmation(string toolName)
    {
        var tool = GetTool(toolName);
        return tool is not null && ToolPermissionPolicy.ResolveRequiredPermission(tool) > DesktopMascot.Core.Enums.PermissionLevel.L0_Chat;
    }

    public string GetConfirmationMessage(string toolName)
    {
        var tool = GetTool(toolName);
        return tool?.ConfirmationMessage ?? string.Empty;
    }

    public IEnumerable<LlmToolDefinition> GetToolDefinitions()
    {
        return _tools.Values.Select(t => new LlmToolDefinition
        {
            Name = t.Name,
            Description = t.Description,
            Parameters = t.ParametersSchema
        });
    }

    public IEnumerable<CoreToolDefinition> GetAllDefinitions()
    {
        return _tools.Values.Select(BuildCoreDefinition).ToList();
    }

    public IEnumerable<CoreToolDefinition> GetByCategory(string category)
    {
        return GetAllDefinitions()
            .Where(t => string.Equals(t.Category, category, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    public IEnumerable<CoreToolDefinition> GetByTag(string tag)
    {
        return GetAllDefinitions()
            .Where(t => t.Tags.Any(x => string.Equals(x, tag, StringComparison.OrdinalIgnoreCase)))
            .ToList();
    }

    public IEnumerable<CoreToolDefinition> Search(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return GetAllDefinitions();

        return GetAllDefinitions()
            .Where(t =>
                t.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                t.Description.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                t.Tags.Any(tag => tag.Contains(query, StringComparison.OrdinalIgnoreCase)))
            .ToList();
    }

    public async Task<ToolCallResponse> ExecuteAsync(ToolCallRequest request, CancellationToken ct = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = await ExecuteToolAsync(new ToolCall
        {
            Id = request.RequestId ?? Guid.NewGuid().ToString("N"),
            Name = request.ToolName,
            Arguments = request.Arguments
        }, ct);

        stopwatch.Stop();

        var response = new ToolCallResponse
        {
            RequestId = result.ToolCallId,
            ToolName = request.ToolName,
            Success = result.Success,
            Result = result.Content,
            Error = result.Error,
            Duration = stopwatch.Elapsed,
            Timestamp = DateTime.UtcNow
        };

        AddCallLog(new ToolCallLog
        {
            ToolName = request.ToolName,
            Arguments = request.Arguments,
            Success = response.Success,
            Result = response.Result,
            Error = response.Error,
            Duration = response.Duration,
            TaskId = request.TaskId,
            Timestamp = response.Timestamp
        });

        return response;
    }

    public Task<List<ToolCallLog>> GetCallLogsAsync(int limit = 100, CancellationToken ct = default)
    {
        lock (_callLogLock)
        {
            return Task.FromResult(_callLogs
                .OrderByDescending(x => x.Timestamp)
                .Take(limit)
                .ToList());
        }
    }

    public async Task<ToolResult> ExecuteToolAsync(ToolCall call, CancellationToken ct = default)
    {
        var tool = GetTool(call.Name);
        if (tool == null)
        {
            return new ToolResult
            {
                ToolCallId = call.Id,
                Name = call.Name,
                Success = false,
                Error = $"工具不存在: {call.Name}"
            };
        }

        var result = await tool.ExecuteAsync(call.Arguments, ct);
        result.ToolCallId = string.IsNullOrWhiteSpace(result.ToolCallId) ? call.Id : result.ToolCallId;
        result.Name = string.IsNullOrWhiteSpace(result.Name) ? call.Name : result.Name;
        return result;
    }

    public int Count => _tools.Count;

    private void AddCallLog(ToolCallLog log)
    {
        lock (_callLogLock)
        {
            _callLogs.Add(log);
            if (_callLogs.Count > 1_000)
            {
                _callLogs.RemoveRange(0, _callLogs.Count - 1_000);
            }
        }
    }

    private static CoreToolDefinition BuildCoreDefinition(ITool tool)
    {
        var definition = tool.Definition;
        return new CoreToolDefinition
        {
            Name = tool.Name,
            Description = tool.Description,
            Version = definition.Version,
            Category = definition.Category,
            ParametersSchema = tool.ParametersSchema,
            RequiredPermission = ToolPermissionPolicy.ResolveRequiredPermission(tool),
            Tags = definition.Tags.ToList(),
            IsEnabled = definition.IsEnabled,
            RegisteredAt = definition.RegisteredAt
        };
    }
}
