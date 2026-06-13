using DesktopMascot.Core.Tools;
using DesktopMascot.Core.Workflow;

namespace DesktopMascot.Core.Tests;

public class WorkflowStoreTests : IDisposable
{
    private readonly FileWorkflowStore _store;
    private readonly string _testDir;

    public WorkflowStoreTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"workflow_store_test_{Guid.NewGuid():N}");
        _store = new FileWorkflowStore(_testDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
            Directory.Delete(_testDir, true);
    }

    [Fact]
    public async Task SaveAsync_ShouldPersistInstance()
    {
        var instance = new WorkflowInstance
        {
            Id = "wf-1",
            Name = "测试工作流",
            Status = WorkflowStatus.Running
        };

        await _store.SaveAsync(instance);

        var loaded = await _store.GetByIdAsync("wf-1");

        Assert.NotNull(loaded);
        Assert.Equal("测试工作流", loaded!.Name);
    }

    [Fact]
    public async Task GetByIdAsync_NonExisting_ShouldReturnNull()
    {
        var result = await _store.GetByIdAsync("non_existing");

        Assert.Null(result);
    }

    [Fact]
    public async Task GetAllAsync_ShouldReturnInstances()
    {
        await _store.SaveAsync(new WorkflowInstance { Id = "wf-1", Name = "工作流1" });
        await _store.SaveAsync(new WorkflowInstance { Id = "wf-2", Name = "工作流2" });

        var all = await _store.GetAllAsync();

        Assert.Equal(2, all.Count);
    }

    [Fact]
    public async Task GetByStatusAsync_ShouldFilterByStatus()
    {
        await _store.SaveAsync(new WorkflowInstance { Id = "wf-1", Status = WorkflowStatus.Completed });
        await _store.SaveAsync(new WorkflowInstance { Id = "wf-2", Status = WorkflowStatus.Running });
        await _store.SaveAsync(new WorkflowInstance { Id = "wf-3", Status = WorkflowStatus.Completed });

        var completed = await _store.GetByStatusAsync(WorkflowStatus.Completed);

        Assert.Equal(2, completed.Count);
    }

    [Fact]
    public async Task DeleteAsync_ShouldRemoveInstance()
    {
        await _store.SaveAsync(new WorkflowInstance { Id = "wf-1" });

        var deleted = await _store.DeleteAsync("wf-1");

        Assert.True(deleted);
        var result = await _store.GetByIdAsync("wf-1");
        Assert.Null(result);
    }

    [Fact]
    public async Task SaveDefinitionAsync_ShouldPersistDefinition()
    {
        var definition = new WorkflowDefinition
        {
            Id = "def-1",
            Name = "测试定义",
            Steps = new List<WorkflowStep>
            {
                new() { Id = "step-1", Name = "步骤1", ToolName = "tool1" }
            }
        };

        await _store.SaveDefinitionAsync(definition);

        var loaded = await _store.GetDefinitionAsync("def-1");

        Assert.NotNull(loaded);
        Assert.Equal("测试定义", loaded!.Name);
        Assert.Single(loaded.Steps);
    }

    [Fact]
    public async Task CleanupAsync_ShouldRemoveOldInstances()
    {
        await _store.SaveAsync(new WorkflowInstance
        {
            Id = "old",
            CreatedAt = DateTime.UtcNow.AddDays(-30),
            Status = WorkflowStatus.Completed
        });
        await _store.SaveAsync(new WorkflowInstance
        {
            Id = "new",
            CreatedAt = DateTime.UtcNow,
            Status = WorkflowStatus.Completed
        });

        var removed = await _store.CleanupAsync(7);

        Assert.Equal(1, removed);
    }
}

public class PersistentWorkflowEngineTests : IDisposable
{
    private readonly ToolRegistry _toolRegistry;
    private readonly FileWorkflowStore _store;
    private readonly PersistentWorkflowEngine _engine;
    private readonly string _testDir;

    public PersistentWorkflowEngineTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"persistent_wf_test_{Guid.NewGuid():N}");
        _toolRegistry = new ToolRegistry();
        _store = new FileWorkflowStore(_testDir);
        _engine = new PersistentWorkflowEngine(_toolRegistry, _store);

        _toolRegistry.Register(new GetCurrentTimeTool());
        _toolRegistry.Register(new CalculatorTool());
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
            Directory.Delete(_testDir, true);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldPersistWorkflow()
    {
        var definition = new WorkflowBuilder("持久化测试")
            .AddStep("获取时间", "get_current_time")
            .Build();

        var instance = _engine.CreateWorkflow(definition);
        await _engine.ExecuteAsync(instance);

        var saved = await _store.GetByIdAsync(instance.Id);

        Assert.NotNull(saved);
        Assert.Equal(WorkflowStatus.Completed, saved!.Status);
    }

    [Fact]
    public async Task ResumeFromCheckpointAsync_ShouldResume()
    {
        var definition = new WorkflowBuilder("恢复测试")
            .AddStep("步骤1", "get_current_time")
            .AddStep("步骤2", "calculator", """{"expression": "1+1"}""")
            .Build();

        var instance = _engine.CreateWorkflow(definition);
        
        // 模拟中断：只执行第一个步骤
        instance.Status = WorkflowStatus.Paused;
        instance.Steps[0].Status = StepStatus.Completed;
        instance.Steps[0].Output = "测试时间";
        await _store.SaveAsync(instance);

        // 从检查点恢复
        var resumed = await _engine.ResumeFromCheckpointAsync(instance.Id);

        Assert.NotNull(resumed);
    }

    [Fact]
    public async Task PauseAsync_ShouldPauseWorkflow()
    {
        var definition = new WorkflowBuilder("暂停测试")
            .AddStep("获取时间", "get_current_time")
            .Build();

        var instance = _engine.CreateWorkflow(definition);
        await _engine.PauseAsync(instance.Id);

        var saved = await _store.GetByIdAsync(instance.Id);
        Assert.Equal(WorkflowStatus.Paused, saved?.Status);
    }

    [Fact]
    public async Task CancelAsync_ShouldCancelWorkflow()
    {
        var definition = new WorkflowBuilder("取消测试")
            .AddStep("获取时间", "get_current_time")
            .Build();

        var instance = _engine.CreateWorkflow(definition);
        await _engine.CancelAsync(instance.Id);

        var saved = await _store.GetByIdAsync(instance.Id);
        Assert.Equal(WorkflowStatus.Cancelled, saved?.Status);
    }
}
