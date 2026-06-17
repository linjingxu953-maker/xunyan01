using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;

namespace DesktopMascot.Core.Tools;

/// <summary>
/// 工具注册表实现
/// </summary>
public class ToolRegistry : IToolRegistry
{
    private readonly ConcurrentDictionary<string, ITool> _tools = new();
    private readonly ConcurrentBag<ToolCallLog> _callLogs = new();
    private readonly List<IToolProvider> _providers = new();
    private readonly object _providerLock = new();

    /// <summary>工具注册事件</summary>
    public event Action<ToolDefinition>? ToolRegistered;
    
    /// <summary>工具调用事件</summary>
    public event Action<ToolCallLog>? ToolCalled;

    public void Register(ITool tool)
    {
        var definition = tool.Definition;
        
        if (_tools.ContainsKey(definition.Name))
        {
            throw new InvalidOperationException($"工具已存在: {definition.Name}");
        }
        
        _tools[definition.Name] = tool;
        ToolRegistered?.Invoke(definition);
    }

    public async Task RegisterProviderAsync(IToolProvider provider, CancellationToken ct = default)
    {
        await provider.InitializeAsync(ct);
        
        lock (_providerLock)
        {
            _providers.Add(provider);
        }
        
        foreach (var tool in provider.GetTools())
        {
            Register(tool);
        }
    }

    public ITool? GetTool(string name)
    {
        return _tools.TryGetValue(name, out var tool) ? tool : null;
    }

    public IEnumerable<ToolDefinition> GetAllDefinitions()
    {
        return _tools.Values.Select(t => t.Definition);
    }

    public IEnumerable<ToolDefinition> GetByCategory(string category)
    {
        return _tools.Values
            .Where(t => t.Definition.Category == category)
            .Select(t => t.Definition);
    }

    public IEnumerable<ToolDefinition> GetByTag(string tag)
    {
        return _tools.Values
            .Where(t => t.Definition.Tags.Contains(tag))
            .Select(t => t.Definition);
    }

    public IEnumerable<ToolDefinition> Search(string query)
    {
        var queryLower = query.ToLower();
        return _tools.Values
            .Where(t => 
                t.Definition.Name.ToLower().Contains(queryLower) ||
                t.Definition.Description.ToLower().Contains(queryLower) ||
                t.Definition.Tags.Any(tag => tag.ToLower().Contains(queryLower)))
            .Select(t => t.Definition);
    }

    public async Task<ToolCallResponse> ExecuteAsync(ToolCallRequest request, CancellationToken ct = default)
    {
        var tool = GetTool(request.ToolName);
        if (tool == null)
        {
            return new ToolCallResponse
            {
                RequestId = request.RequestId ?? "",
                ToolName = request.ToolName,
                Success = false,
                Error = $"工具不存在: {request.ToolName}"
            };
        }

        if (!tool.Definition.IsEnabled)
        {
            return new ToolCallResponse
            {
                RequestId = request.RequestId ?? "",
                ToolName = request.ToolName,
                Success = false,
                Error = $"工具已禁用: {request.ToolName}"
            };
        }

        var stopwatch = Stopwatch.StartNew();
        var result = await tool.ExecuteAsync(request.Arguments, ct);
        stopwatch.Stop();

        var response = new ToolCallResponse
        {
            RequestId = request.RequestId ?? "",
            ToolName = request.ToolName,
            Success = result.Success,
            Result = result.Content,
            Error = result.Error,
            Duration = stopwatch.Elapsed
        };

        // 记录日志
        var log = new ToolCallLog
        {
            ToolName = request.ToolName,
            Arguments = request.Arguments,
            Success = response.Success,
            Result = response.Result,
            Error = response.Error,
            Duration = response.Duration,
            TaskId = request.TaskId
        };

        _callLogs.Add(log);
        ToolCalled?.Invoke(log);

        return response;
    }

    public Task<List<ToolCallLog>> GetCallLogsAsync(int limit = 100, CancellationToken ct = default)
    {
        var logs = _callLogs
            .OrderByDescending(l => l.Timestamp)
            .Take(limit)
            .ToList();
        
        return Task.FromResult(logs);
    }
}
