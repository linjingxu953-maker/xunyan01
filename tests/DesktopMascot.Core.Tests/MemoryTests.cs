using DesktopMascot.Core.Memory;

namespace DesktopMascot.Core.Tests;

public class MemoryStoreTests : IDisposable
{
    private readonly FileMemoryStore _store;
    private readonly string _testDir;

    public MemoryStoreTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"mascot_test_{Guid.NewGuid():N}");
        _store = new FileMemoryStore(_testDir);
    }

    void IDisposable.Dispose()
    {
        if (Directory.Exists(_testDir))
            Directory.Delete(_testDir, true);
    }

    [Fact]
    public async Task SaveAsync_ShouldPersistEntry()
    {
        var entry = new MemoryEntry
        {
            Type = MemoryType.User,
            Key = "test_key",
            Content = "test content"
        };

        var saved = await _store.SaveAsync(entry);

        Assert.NotNull(saved);
        Assert.Equal("test_key", saved.Key);
    }

    [Fact]
    public async Task GetByIdAsync_ShouldReturnEntry()
    {
        var entry = new MemoryEntry
        {
            Type = MemoryType.Project,
            Key = "project1",
            Content = "Project info"
        };
        await _store.SaveAsync(entry);

        var result = await _store.GetByIdAsync(entry.Id);

        Assert.NotNull(result);
        Assert.Equal("project1", result!.Key);
    }

    [Fact]
    public async Task GetByKeyAsync_ShouldReturnEntry()
    {
        var entry = new MemoryEntry
        {
            Type = MemoryType.Skill,
            Key = "summarize",
            Content = "Summarize skill"
        };
        await _store.SaveAsync(entry);

        var result = await _store.GetByKeyAsync("summarize", MemoryType.Skill);

        Assert.NotNull(result);
        Assert.Equal("Summarize skill", result!.Content);
    }

    [Fact]
    public async Task DeleteAsync_ShouldRemoveEntry()
    {
        var entry = new MemoryEntry
        {
            Type = MemoryType.User,
            Key = "to_delete",
            Content = "Delete me"
        };
        await _store.SaveAsync(entry);

        var deleted = await _store.DeleteAsync(entry.Id);

        Assert.True(deleted);
        var result = await _store.GetByIdAsync(entry.Id);
        Assert.Null(result);
    }

    [Fact]
    public async Task SearchAsync_ShouldFindEntries()
    {
        await _store.SaveAsync(new MemoryEntry
        {
            Type = MemoryType.User,
            Key = "preference",
            Content = "喜欢深色主题"
        });
        await _store.SaveAsync(new MemoryEntry
        {
            Type = MemoryType.Project,
            Key = "tech_stack",
            Content = "使用 Avalonia UI"
        });

        var results = await _store.SearchAsync("深色");

        Assert.Single(results.Entries);
        Assert.Contains("深色", results.Entries[0].Content);
    }

    [Fact]
    public async Task GetByTypeAsync_ShouldFilterByType()
    {
        await _store.SaveAsync(new MemoryEntry { Type = MemoryType.User, Key = "u1", Content = "user1" });
        await _store.SaveAsync(new MemoryEntry { Type = MemoryType.User, Key = "u2", Content = "user2" });
        await _store.SaveAsync(new MemoryEntry { Type = MemoryType.Project, Key = "p1", Content = "project1" });

        var userMemories = await _store.GetByTypeAsync(MemoryType.User);

        Assert.Equal(2, userMemories.Count);
    }

    [Fact]
    public async Task ConfirmAsync_ShouldMarkAsConfirmed()
    {
        var entry = new MemoryEntry
        {
            Type = MemoryType.User,
            Key = "unconfirmed",
            Content = "Need confirmation",
            IsConfirmed = false
        };
        await _store.SaveAsync(entry);

        var confirmed = await _store.ConfirmAsync(entry.Id);

        Assert.True(confirmed);
        var result = await _store.GetByIdAsync(entry.Id);
        Assert.True(result!.IsConfirmed);
    }

    [Fact]
    public async Task GetStatisticsAsync_ShouldReturnCorrectStats()
    {
        await _store.SaveAsync(new MemoryEntry { Type = MemoryType.User, Key = "u1", Content = "user" });
        await _store.SaveAsync(new MemoryEntry { Type = MemoryType.Project, Key = "p1", Content = "project" });

        var stats = await _store.GetStatisticsAsync();

        Assert.Equal(2, stats.TotalCount);
        Assert.Equal(1, stats.UserCount);
        Assert.Equal(1, stats.ProjectCount);
    }

    [Fact]
    public async Task ExportAsync_ShouldReturnJson()
    {
        await _store.SaveAsync(new MemoryEntry { Type = MemoryType.User, Key = "u1", Content = "user" });

        var json = await _store.ExportAsync();

        Assert.NotEmpty(json);
        Assert.Contains("u1", json);
    }
}

public class MemoryManagerTests : IDisposable
{
    private readonly FileMemoryStore _store;
    private readonly MemoryManager _manager;
    private readonly string _testDir;

    public MemoryManagerTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"mascot_test_{Guid.NewGuid():N}");
        _store = new FileMemoryStore(_testDir);
        _manager = new MemoryManager(_store);
    }

    void IDisposable.Dispose()
    {
        if (Directory.Exists(_testDir))
            Directory.Delete(_testDir, true);
    }

    [Fact]
    public async Task SaveQuickAsync_ShouldSaveEntry()
    {
        var entry = await _manager.SaveQuickAsync("key1", "content1", MemoryType.User);

        Assert.NotNull(entry);
        Assert.True(entry.IsConfirmed);
    }

    [Fact]
    public async Task SaveQuickAsync_UpdateExisting_ShouldUpdate()
    {
        await _manager.SaveQuickAsync("key1", "original", MemoryType.User);
        var updated = await _manager.SaveQuickAsync("key1", "updated", MemoryType.User);

        Assert.Equal("updated", updated.Content);
    }

    [Fact]
    public async Task SearchAsync_ShouldFindEntries()
    {
        await _manager.SaveQuickAsync("pref", "喜欢蓝色", MemoryType.User);

        var results = await _manager.SearchAsync("蓝色");

        Assert.Single(results.Entries);
    }

    [Fact]
    public async Task DeleteAsync_ShouldRemoveEntry()
    {
        var entry = await _manager.SaveQuickAsync("to_delete", "delete me", MemoryType.User);

        var deleted = await _manager.DeleteAsync(entry.Id);

        Assert.True(deleted);
    }
}
