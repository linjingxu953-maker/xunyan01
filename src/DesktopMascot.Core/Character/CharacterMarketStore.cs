using System.Text.Json;

namespace DesktopMascot.Core.Character;

/// <summary>
/// 角色市场存储实现 — 本地 JSON 文件索引
/// </summary>
public class CharacterMarketStore : ICharacterMarketStore
{
    private readonly string _indexFilePath;
    private readonly object _lock = new();
    private List<CharacterMarketEntry> _entries = new();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public CharacterMarketStore(string? dataDirectory = null)
    {
        var dir = dataDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "DesktopMascot", "characters");
        Directory.CreateDirectory(dir);
        _indexFilePath = Path.Combine(dir, "market_index.json");
        Load();
    }

    public IReadOnlyList<CharacterMarketEntry> Browse(int offset = 0, int limit = 50)
    {
        lock (_lock)
        {
            return _entries
                .OrderByDescending(e => e.AddedAt)
                .Skip(offset)
                .Take(limit)
                .ToList();
        }
    }

    public IReadOnlyList<CharacterMarketEntry> Search(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return Browse();

        var q = query.ToLowerInvariant();
        lock (_lock)
        {
            return _entries
                .Where(e =>
                    e.Name.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                    e.Author.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                    e.Description.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                    e.Tags.Any(t => t.Contains(q, StringComparison.OrdinalIgnoreCase)))
                .OrderByDescending(e => e.Rating)
                .ToList();
        }
    }

    public CharacterMarketEntry? GetBySlug(string slug)
    {
        lock (_lock)
        {
            return _entries.FirstOrDefault(e =>
                string.Equals(e.Slug, slug, StringComparison.OrdinalIgnoreCase));
        }
    }

    public IReadOnlyList<CharacterMarketEntry> GetByTag(string tag)
    {
        lock (_lock)
        {
            return _entries
                .Where(e => e.Tags.Any(t =>
                    string.Equals(t, tag, StringComparison.OrdinalIgnoreCase)))
                .ToList();
        }
    }

    public IReadOnlyList<CharacterMarketEntry> GetFeatured(int count = 10)
    {
        lock (_lock)
        {
            return _entries
                .Where(e => e.IsFeatured)
                .OrderByDescending(e => e.Rating)
                .ThenByDescending(e => e.Downloads)
                .Take(count)
                .ToList();
        }
    }

    public void Add(CharacterMarketEntry entry)
    {
        lock (_lock)
        {
            var existing = _entries.FindIndex(e =>
                string.Equals(e.Slug, entry.Slug, StringComparison.OrdinalIgnoreCase));

            if (existing >= 0)
                _entries[existing] = entry;
            else
                _entries.Add(entry);
        }
    }

    public void AddRange(IEnumerable<CharacterMarketEntry> entries)
    {
        foreach (var entry in entries)
            Add(entry);
    }

    public bool Remove(string slug)
    {
        lock (_lock)
        {
            return _entries.RemoveAll(e =>
                string.Equals(e.Slug, slug, StringComparison.OrdinalIgnoreCase)) > 0;
        }
    }

    public void UpdateRating(string slug, float rating)
    {
        lock (_lock)
        {
            var entry = _entries.FirstOrDefault(e =>
                string.Equals(e.Slug, slug, StringComparison.OrdinalIgnoreCase));
            if (entry != null)
                entry.Rating = rating;
        }
    }

    public void IncrementDownloads(string slug)
    {
        lock (_lock)
        {
            var entry = _entries.FirstOrDefault(e =>
                string.Equals(e.Slug, slug, StringComparison.OrdinalIgnoreCase));
            if (entry != null)
                entry.Downloads++;
        }
    }

    public int Count()
    {
        lock (_lock) { return _entries.Count; }
    }

    public void Save()
    {
        lock (_lock)
        {
            var json = JsonSerializer.Serialize(_entries, JsonOptions);
            File.WriteAllText(_indexFilePath, json);
        }
    }

    public void Load()
    {
        if (!File.Exists(_indexFilePath)) return;

        try
        {
            var json = File.ReadAllText(_indexFilePath);
            _entries = JsonSerializer.Deserialize<List<CharacterMarketEntry>>(json, JsonOptions) ?? new();
        }
        catch
        {
            _entries = new();
        }
    }

    /// <summary>
    /// 从角色目录扫描并建立索引
    /// </summary>
    public int ScanAndIndex(string charactersDirectory)
    {
        if (!Directory.Exists(charactersDirectory)) return 0;

        var loader = new CharacterPackageLoader();
        var count = 0;

        foreach (var dir in Directory.GetDirectories(charactersDirectory))
        {
            var result = loader.LoadFromDirectory(dir);
            if (!result.Success || result.Manifest == null) continue;

            var manifest = result.Manifest;
            var entry = new CharacterMarketEntry
            {
                Slug = manifest.Slug,
                Name = manifest.Name,
                Author = manifest.Metadata?.Author ?? "",
                Description = manifest.Profile?.Description ?? "",
                Version = manifest.Version,
                Tags = manifest.Metadata?.Tags?.ToList() ?? new(),
                AvatarColor = manifest.Appearance?.AccentColor ?? "#2563EB",
                LocalPath = dir,
                Rating = manifest.Metadata?.Market?.Rating ?? 0,
                Downloads = manifest.Metadata?.Market?.Downloads ?? 0,
                IsFeatured = manifest.Metadata?.Market?.IsFeatured ?? false,
                PreviewImage = manifest.Metadata?.Market?.PreviewImage ?? "",
                PublishedAt = manifest.Metadata?.Market?.PublishedAt
            };

            Add(entry);
            count++;
        }

        return count;
    }
}
