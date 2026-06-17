using DesktopMascot.Core.Character;

namespace DesktopMascot.Core.Tests;

public class CharacterMarketStoreTests
{
    private readonly CharacterMarketStore _store;

    public CharacterMarketStoreTests()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"mkt_test_{Guid.NewGuid():N}");
        _store = new CharacterMarketStore(tempDir);
    }

    [Fact]
    public void Add_ShouldIncreaseCount()
    {
        _store.Add(CreateEntry("alpha", "Alpha"));
        Assert.Equal(1, _store.Count());
    }

    [Fact]
    public void Add_DuplicateSlug_ShouldUpdate()
    {
        _store.Add(CreateEntry("dup", "First", rating: 3.0f));
        _store.Add(CreateEntry("dup", "Second", rating: 4.5f));

        Assert.Equal(1, _store.Count());
        var entry = _store.GetBySlug("dup");
        Assert.Equal("Second", entry!.Name);
        Assert.Equal(4.5f, entry.Rating);
    }

    [Fact]
    public void Browse_ShouldReturnOrderedByAddedAt()
    {
        _store.Add(CreateEntry("a", "A"));
        _store.Add(CreateEntry("b", "B"));
        _store.Add(CreateEntry("c", "C"));

        var result = _store.Browse(0, 2);
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void Search_ShouldMatchName()
    {
        _store.Add(CreateEntry("search-a", "Hello World"));
        _store.Add(CreateEntry("search-b", "Goodbye World"));

        var result = _store.Search("hello");
        Assert.Single(result);
        Assert.Equal("search-a", result[0].Slug);
    }

    [Fact]
    public void Search_ShouldMatchTag()
    {
        _store.Add(CreateEntry("tag-a", "Tagged", tags: new() { "cute", "cat" }));
        _store.Add(CreateEntry("tag-b", "Not Tagged"));

        var result = _store.Search("cute");
        Assert.Single(result);
        Assert.Equal("tag-a", result[0].Slug);
    }

    [Fact]
    public void GetBySlug_ShouldFindCorrect()
    {
        _store.Add(CreateEntry("find-me", "Find Me"));
        _store.Add(CreateEntry("other", "Other"));

        var result = _store.GetBySlug("find-me");
        Assert.NotNull(result);
        Assert.Equal("Find Me", result.Name);
    }

    [Fact]
    public void GetBySlug_NonExistent_ShouldReturnNull()
    {
        Assert.Null(_store.GetBySlug("nonexistent"));
    }

    [Fact]
    public void GetByTag_ShouldReturnMatching()
    {
        _store.Add(CreateEntry("tag-1", "A", tags: new() { "dog" }));
        _store.Add(CreateEntry("tag-2", "B", tags: new() { "cat", "cute" }));
        _store.Add(CreateEntry("tag-3", "C", tags: new() { "dog", "cute" }));

        var dogs = _store.GetByTag("dog");
        Assert.Equal(2, dogs.Count);

        var cats = _store.GetByTag("cat");
        Assert.Single(cats);
    }

    [Fact]
    public void GetFeatured_ShouldReturnFeaturedOnly()
    {
        _store.Add(CreateEntry("feat-1", "Featured", featured: true, rating: 4.5f));
        _store.Add(CreateEntry("feat-2", "Not Featured", featured: false, rating: 5.0f));

        var result = _store.GetFeatured();
        Assert.Single(result);
        Assert.Equal("feat-1", result[0].Slug);
    }

    [Fact]
    public void Remove_ShouldDecreaseCount()
    {
        _store.Add(CreateEntry("to-remove", "Remove Me"));
        Assert.Equal(1, _store.Count());

        var removed = _store.Remove("to-remove");
        Assert.True(removed);
        Assert.Equal(0, _store.Count());
    }

    [Fact]
    public void Remove_NonExistent_ShouldReturnFalse()
    {
        Assert.False(_store.Remove("nonexistent"));
    }

    [Fact]
    public void UpdateRating_ShouldChangeRating()
    {
        _store.Add(CreateEntry("rate-me", "Rate Me", rating: 3.0f));
        _store.UpdateRating("rate-me", 4.8f);

        var entry = _store.GetBySlug("rate-me");
        Assert.Equal(4.8f, entry!.Rating);
    }

    [Fact]
    public void IncrementDownloads_ShouldIncreaseCount()
    {
        _store.Add(CreateEntry("dl-me", "Download Me", downloads: 10));
        _store.IncrementDownloads("dl-me");
        _store.IncrementDownloads("dl-me");

        var entry = _store.GetBySlug("dl-me");
        Assert.Equal(12, entry!.Downloads);
    }

    [Fact]
    public void SaveAndLoad_ShouldPersistData()
    {
        _store.Add(CreateEntry("persist-1", "Persist 1"));
        _store.Add(CreateEntry("persist-2", "Persist 2"));
        _store.Save();

        // 创建新 store 从同一文件加载
        var store2 = new CharacterMarketStore(
            Path.GetDirectoryName(_store.GetType().Assembly.Location));

        // 使用相同路径重建
        var tempDir = Path.Combine(Path.GetTempPath(), $"mkt_load_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        var indexFile = Path.Combine(tempDir, "market_index.json");

        // 复制索引文件
        var originalIndex = typeof(CharacterMarketStore)
            .GetField("_indexFilePath", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .GetValue(_store)!.ToString()!;
        File.Copy(originalIndex, indexFile);

        var store3 = new CharacterMarketStore(tempDir);
        Assert.Equal(2, store3.Count());
    }

    [Fact]
    public void Search_EmptyQuery_ShouldReturnAll()
    {
        _store.Add(CreateEntry("s1", "A"));
        _store.Add(CreateEntry("s2", "B"));

        var result = _store.Search("");
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void Search_NoMatch_ShouldReturnEmpty()
    {
        _store.Add(CreateEntry("no-match", "Something"));

        var result = _store.Search("zzz_not_exist");
        Assert.Empty(result);
    }

    private static CharacterMarketEntry CreateEntry(string slug, string name,
        float rating = 0, int downloads = 0, bool featured = false,
        List<string>? tags = null)
    {
        return new CharacterMarketEntry
        {
            Slug = slug,
            Name = name,
            Author = "test",
            Rating = rating,
            Downloads = downloads,
            IsFeatured = featured,
            Tags = tags ?? new()
        };
    }
}
