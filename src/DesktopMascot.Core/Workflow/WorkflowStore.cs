using System.Text.Json;

namespace DesktopMascot.Core.Workflow;

/// <summary>
/// 工作流存储接口
/// </summary>
public interface IWorkflowStore
{
    /// <summary>保存工作流实例</summary>
    Task<WorkflowInstance> SaveAsync(WorkflowInstance instance, CancellationToken ct = default);
    
    /// <summary>获取工作流实例</summary>
    Task<WorkflowInstance?> GetByIdAsync(string id, CancellationToken ct = default);
    
    /// <summary>获取所有工作流</summary>
    Task<List<WorkflowInstance>> GetAllAsync(int limit = 100, CancellationToken ct = default);
    
    /// <summary>按状态获取工作流</summary>
    Task<List<WorkflowInstance>> GetByStatusAsync(WorkflowStatus status, CancellationToken ct = default);
    
    /// <summary>删除工作流</summary>
    Task<bool> DeleteAsync(string id, CancellationToken ct = default);
    
    /// <summary>清理已完成的工作流</summary>
    Task<int> CleanupAsync(int keepDays = 7, CancellationToken ct = default);
    
    /// <summary>保存工作流定义</summary>
    Task SaveDefinitionAsync(WorkflowDefinition definition, CancellationToken ct = default);
    
    /// <summary>获取工作流定义</summary>
    Task<WorkflowDefinition?> GetDefinitionAsync(string id, CancellationToken ct = default);
}

/// <summary>
/// 文件工作流存储
/// </summary>
public class FileWorkflowStore : IWorkflowStore
{
    private readonly string _storageDirectory;
    private readonly object _lock = new();
    private const string InstancesFileName = "workflow_instances.json";
    private const string DefinitionsFileName = "workflow_definitions.json";

    public FileWorkflowStore(string? storageDirectory = null)
    {
        _storageDirectory = storageDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "DesktopMascot",
            "workflows");
        Directory.CreateDirectory(_storageDirectory);
    }

    private string GetInstancesFilePath() => Path.Combine(_storageDirectory, InstancesFileName);
    private string GetDefinitionsFilePath() => Path.Combine(_storageDirectory, DefinitionsFileName);

    private async Task<List<WorkflowInstance>> LoadAllInstancesAsync()
    {
        var filePath = GetInstancesFilePath();
        
        if (!File.Exists(filePath))
            return new List<WorkflowInstance>();
        
        lock (_lock)
        {
            var json = File.ReadAllText(filePath);
            return JsonSerializer.Deserialize<List<WorkflowInstance>>(json) ?? new List<WorkflowInstance>();
        }
    }

    private async Task SaveAllInstancesAsync(List<WorkflowInstance> instances)
    {
        var filePath = GetInstancesFilePath();
        var json = JsonSerializer.Serialize(instances, new JsonSerializerOptions { WriteIndented = true });
        
        lock (_lock)
        {
            File.WriteAllText(filePath, json);
        }
        
        await Task.CompletedTask;
    }

    private async Task<List<WorkflowDefinition>> LoadAllDefinitionsAsync()
    {
        var filePath = GetDefinitionsFilePath();
        
        if (!File.Exists(filePath))
            return new List<WorkflowDefinition>();
        
        lock (_lock)
        {
            var json = File.ReadAllText(filePath);
            return JsonSerializer.Deserialize<List<WorkflowDefinition>>(json) ?? new List<WorkflowDefinition>();
        }
    }

    private async Task SaveAllDefinitionsAsync(List<WorkflowDefinition> definitions)
    {
        var filePath = GetDefinitionsFilePath();
        var json = JsonSerializer.Serialize(definitions, new JsonSerializerOptions { WriteIndented = true });
        
        lock (_lock)
        {
            File.WriteAllText(filePath, json);
        }
        
        await Task.CompletedTask;
    }

    public async Task<WorkflowInstance> SaveAsync(WorkflowInstance instance, CancellationToken ct = default)
    {
        var instances = await LoadAllInstancesAsync();
        
        var existingIndex = instances.FindIndex(i => i.Id == instance.Id);
        if (existingIndex >= 0)
        {
            instances[existingIndex] = instance;
        }
        else
        {
            instances.Add(instance);
        }
        
        await SaveAllInstancesAsync(instances);
        return instance;
    }

    public async Task<WorkflowInstance?> GetByIdAsync(string id, CancellationToken ct = default)
    {
        var instances = await LoadAllInstancesAsync();
        return instances.FirstOrDefault(i => i.Id == id);
    }

    public async Task<List<WorkflowInstance>> GetAllAsync(int limit = 100, CancellationToken ct = default)
    {
        var instances = await LoadAllInstancesAsync();
        return instances
            .OrderByDescending(i => i.CreatedAt)
            .Take(limit)
            .ToList();
    }

    public async Task<List<WorkflowInstance>> GetByStatusAsync(WorkflowStatus status, CancellationToken ct = default)
    {
        var instances = await LoadAllInstancesAsync();
        return instances
            .Where(i => i.Status == status)
            .OrderByDescending(i => i.CreatedAt)
            .ToList();
    }

    public async Task<bool> DeleteAsync(string id, CancellationToken ct = default)
    {
        var instances = await LoadAllInstancesAsync();
        var removed = instances.RemoveAll(i => i.Id == id);
        
        if (removed > 0)
        {
            await SaveAllInstancesAsync(instances);
            return true;
        }
        
        return false;
    }

    public async Task<int> CleanupAsync(int keepDays = 7, CancellationToken ct = default)
    {
        var instances = await LoadAllInstancesAsync();
        var cutoff = DateTime.UtcNow.AddDays(-keepDays);
        
        var removed = instances.RemoveAll(i => 
            i.CreatedAt < cutoff && 
            (i.Status == WorkflowStatus.Completed || i.Status == WorkflowStatus.Failed));
        
        if (removed > 0)
        {
            await SaveAllInstancesAsync(instances);
        }
        
        return removed;
    }

    public async Task SaveDefinitionAsync(WorkflowDefinition definition, CancellationToken ct = default)
    {
        var definitions = await LoadAllDefinitionsAsync();
        
        var existingIndex = definitions.FindIndex(d => d.Id == definition.Id);
        if (existingIndex >= 0)
        {
            definitions[existingIndex] = definition;
        }
        else
        {
            definitions.Add(definition);
        }
        
        await SaveAllDefinitionsAsync(definitions);
    }

    public async Task<WorkflowDefinition?> GetDefinitionAsync(string id, CancellationToken ct = default)
    {
        var definitions = await LoadAllDefinitionsAsync();
        return definitions.FirstOrDefault(d => d.Id == id);
    }
}
