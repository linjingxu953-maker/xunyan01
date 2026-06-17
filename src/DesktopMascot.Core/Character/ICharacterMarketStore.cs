namespace DesktopMascot.Core.Character;

/// <summary>
/// 角色市场存储接口 — 本地角色包索引的增删查改
/// </summary>
public interface ICharacterMarketStore
{
    /// <summary>浏览所有已索引角色</summary>
    IReadOnlyList<CharacterMarketEntry> Browse(int offset = 0, int limit = 50);

    /// <summary>搜索角色（按名称/标签/作者）</summary>
    IReadOnlyList<CharacterMarketEntry> Search(string query);

    /// <summary>按 slug 获取</summary>
    CharacterMarketEntry? GetBySlug(string slug);

    /// <summary>按标签筛选</summary>
    IReadOnlyList<CharacterMarketEntry> GetByTag(string tag);

    /// <summary>获取推荐角色（按评分/下载量）</summary>
    IReadOnlyList<CharacterMarketEntry> GetFeatured(int count = 10);

    /// <summary>添加角色到索引</summary>
    void Add(CharacterMarketEntry entry);

    /// <summary>批量添加</summary>
    void AddRange(IEnumerable<CharacterMarketEntry> entries);

    /// <summary>移除角色</summary>
    bool Remove(string slug);

    /// <summary>更新评分</summary>
    void UpdateRating(string slug, float rating);

    /// <summary>增加下载计数</summary>
    void IncrementDownloads(string slug);

    /// <summary>获取总数</summary>
    int Count();

    /// <summary>持久化到文件</summary>
    void Save();

    /// <summary>从文件加载</summary>
    void Load();
}

/// <summary>
/// 角色市场条目 — 从 character.json 提取的市场展示信息
/// </summary>
public class CharacterMarketEntry
{
    public string Slug { get; set; } = "";
    public string Name { get; set; } = "";
    public string Author { get; set; } = "";
    public string Description { get; set; } = "";
    public string Version { get; set; } = "1.0";
    public string PreviewImage { get; set; } = "";
    public string AvatarColor { get; set; } = "#2563EB";
    public List<string> Tags { get; set; } = new();
    public float Rating { get; set; }
    public int Downloads { get; set; }
    public bool IsFeatured { get; set; }
    public string? LocalPath { get; set; }
    public DateTime AddedAt { get; set; } = DateTime.UtcNow;
    public DateTime? PublishedAt { get; set; }
}
