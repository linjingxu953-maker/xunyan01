using System.Text.Json;
using DesktopMascot.Agent.Models;
using DesktopMascot.Core.Character;
using DesktopMascot.Core.Tools;

namespace DesktopMascot.Agent.Tools;

/// <summary>
/// 角色市场工具 — 浏览/搜索/评分/下载/安装角色包
/// </summary>
public class CharacterMarketTool : ITool
{
    private readonly ICharacterMarketStore _marketStore;
    private readonly ICharacterManager _characterManager;

    public string Name => "character_market";
    public string Description => "角色市场：浏览/搜索/安装/评分/查看推荐角色。";
    public string ParametersSchema => """
    {
        "type": "object",
        "properties": {
            "action": { "type": "string", "enum": ["browse", "search", "featured", "get", "install", "rate", "info", "scan", "tags"], "description": "操作类型" },
            "slug": { "type": "string", "description": "角色标识（get/install/rate 时使用）" },
            "query": { "type": "string", "description": "搜索关键词（search 时使用）" },
            "tag": { "type": "string", "description": "标签筛选（tags 时使用）" },
            "rating": { "type": "number", "description": "评分 1-5（rate 时使用）" },
            "directory": { "type": "string", "description": "扫描目录（scan 时使用）" },
            "limit": { "type": "integer", "description": "返回数量" }
        },
        "required": ["action"]
    }
    """;

    public CharacterMarketTool(ICharacterMarketStore marketStore, ICharacterManager characterManager)
    {
        _marketStore = marketStore;
        _characterManager = characterManager;
    }

    public Task<ToolResult> ExecuteAsync(string arguments, CancellationToken ct = default)
    {
        try
        {
            var doc = JsonDocument.Parse(arguments);
            var root = doc.RootElement;
            var action = root.TryGetProperty("action", out var aEl) ? aEl.GetString() ?? "" : "";

            return action switch
            {
                "browse" => Browse(root),
                "search" => Search(root),
                "featured" => Featured(root),
                "get" => GetCharacter(root),
                "install" => Install(root),
                "rate" => Rate(root),
                "info" => Info(),
                "scan" => Scan(root),
                "tags" => Tags(root),
                _ => Task.FromResult(Fail($"不支持的操作：{action}"))
            };
        }
        catch (Exception ex)
        {
            return Task.FromResult(Fail($"角色市场操作失败：{ex.Message}"));
        }
    }

    private Task<ToolResult> Browse(JsonElement root)
    {
        var limit = root.TryGetProperty("limit", out var lEl) ? lEl.GetInt32() : 20;
        var entries = _marketStore.Browse(0, limit);
        return Task.FromResult(new ToolResult
        {
            Name = Name,
            Success = true,
            Content = FormatEntries("浏览角色", entries)
        });
    }

    private Task<ToolResult> Search(JsonElement root)
    {
        var query = root.TryGetProperty("query", out var qEl) ? qEl.GetString() ?? "" : "";
        if (string.IsNullOrEmpty(query))
            return Task.FromResult(Fail("缺少 query 参数"));

        var entries = _marketStore.Search(query);
        return Task.FromResult(new ToolResult
        {
            Name = Name,
            Success = true,
            Content = FormatEntries($"搜索「{query}」", entries)
        });
    }

    private Task<ToolResult> Featured(JsonElement root)
    {
        var count = root.TryGetProperty("limit", out var lEl) ? lEl.GetInt32() : 10;
        var entries = _marketStore.GetFeatured(count);
        return Task.FromResult(new ToolResult
        {
            Name = Name,
            Success = true,
            Content = FormatEntries("推荐角色", entries)
        });
    }

    private Task<ToolResult> GetCharacter(JsonElement root)
    {
        var slug = root.TryGetProperty("slug", out var sEl) ? sEl.GetString() ?? "" : "";
        if (string.IsNullOrEmpty(slug))
            return Task.FromResult(Fail("缺少 slug 参数"));

        var entry = _marketStore.GetBySlug(slug);
        if (entry == null)
            return Task.FromResult(Fail($"角色 [{slug}] 不存在"));

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"角色详情：{entry.Name}");
        sb.AppendLine($"  标识：{entry.Slug}");
        sb.AppendLine($"  作者：{entry.Author}");
        sb.AppendLine($"  版本：{entry.Version}");
        sb.AppendLine($"  描述：{entry.Description}");
        sb.AppendLine($"  评分：{entry.Rating:F1}/5.0");
        sb.AppendLine($"  下载：{entry.Downloads}");
        sb.AppendLine($"  标签：{string.Join(", ", entry.Tags)}");
        if (entry.IsFeatured) sb.AppendLine("  ★ 推荐角色");
        if (entry.LocalPath != null) sb.AppendLine($"  本地路径：{entry.LocalPath}");

        return Task.FromResult(new ToolResult { Name = Name, Success = true, Content = sb.ToString() });
    }

    private Task<ToolResult> Install(JsonElement root)
    {
        var slug = root.TryGetProperty("slug", out var sEl) ? sEl.GetString() ?? "" : "";
        if (string.IsNullOrEmpty(slug))
            return Task.FromResult(Fail("缺少 slug 参数"));

        var entry = _marketStore.GetBySlug(slug);
        if (entry == null)
            return Task.FromResult(Fail($"角色 [{slug}] 不存在"));

        if (string.IsNullOrEmpty(entry.LocalPath))
            return Task.FromResult(Fail($"角色 [{slug}] 没有本地路径，无法安装"));

        var loadResult = _characterManager.Load(entry.LocalPath);
        if (!loadResult.Success)
            return Task.FromResult(Fail($"安装失败：{string.Join("; ", loadResult.Errors)}"));

        _marketStore.IncrementDownloads(slug);

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"已安装角色：{entry.Name}");
        sb.AppendLine($"版本：{entry.Version}");
        foreach (var w in loadResult.Warnings)
            sb.AppendLine($"⚠ {w}");

        return Task.FromResult(new ToolResult { Name = Name, Success = true, Content = sb.ToString() });
    }

    private Task<ToolResult> Rate(JsonElement root)
    {
        var slug = root.TryGetProperty("slug", out var sEl) ? sEl.GetString() ?? "" : "";
        var rating = root.TryGetProperty("rating", out var rEl) ? (float)rEl.GetDouble() : 0;

        if (string.IsNullOrEmpty(slug))
            return Task.FromResult(Fail("缺少 slug 参数"));
        if (rating < 1 || rating > 5)
            return Task.FromResult(Fail("评分范围：1-5"));

        var entry = _marketStore.GetBySlug(slug);
        if (entry == null)
            return Task.FromResult(Fail($"角色 [{slug}] 不存在"));

        _marketStore.UpdateRating(slug, rating);
        _marketStore.Save();

        return Task.FromResult(new ToolResult
        {
            Name = Name,
            Success = true,
            Content = $"已评分：{entry.Name} → {rating:F1}/5.0"
        });
    }

    private Task<ToolResult> Info()
    {
        var count = _marketStore.Count();
        var featured = _marketStore.GetFeatured(5);
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("角色市场信息");
        sb.AppendLine($"  已索引角色：{count}");
        if (featured.Count > 0)
        {
            sb.AppendLine("  推荐角色：");
            foreach (var f in featured)
                sb.AppendLine($"    [{f.Slug}] {f.Name} (★{f.Rating:F1}, {f.Downloads}次下载)");
        }

        return Task.FromResult(new ToolResult { Name = Name, Success = true, Content = sb.ToString() });
    }

    private Task<ToolResult> Scan(JsonElement root)
    {
        var directory = root.TryGetProperty("directory", out var dEl) ? dEl.GetString() ?? "" : "";
        if (string.IsNullOrEmpty(directory))
        {
            // 默认扫描 AppData 下的 characters 目录
            directory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "DesktopMascot", "characters");
        }

        if (!Directory.Exists(directory))
            return Task.FromResult(Fail($"目录不存在：{directory}"));

        var count = ((CharacterMarketStore)_marketStore).ScanAndIndex(directory);
        _marketStore.Save();

        return Task.FromResult(new ToolResult
        {
            Name = Name,
            Success = true,
            Content = $"扫描完成：在 {directory} 中发现 {count} 个角色"
        });
    }

    private Task<ToolResult> Tags(JsonElement root)
    {
        var tag = root.TryGetProperty("tag", out var tEl) ? tEl.GetString() ?? "" : "";
        if (string.IsNullOrEmpty(tag))
        {
            // 列出所有标签
            var allTags = _marketStore.Browse(0, 1000)
                .SelectMany(e => e.Tags)
                .GroupBy(t => t)
                .OrderByDescending(g => g.Count())
                .Select(g => $"  {g.Key} ({g.Count()})")
                .ToList();

            return Task.FromResult(new ToolResult
            {
                Name = Name,
                Success = true,
                Content = "所有标签：\n" + string.Join("\n", allTags)
            });
        }

        var entries = _marketStore.GetByTag(tag);
        return Task.FromResult(new ToolResult
        {
            Name = Name,
            Success = true,
            Content = FormatEntries($"标签「{tag}」", entries)
        });
    }

    private static string FormatEntries(string title, IReadOnlyList<CharacterMarketEntry> entries)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"{title}（{entries.Count} 个）");

        if (entries.Count == 0)
        {
            sb.AppendLine("  （无）");
        }
        else
        {
            foreach (var e in entries)
            {
                var featured = e.IsFeatured ? " ★" : "";
                sb.AppendLine($"  [{e.Slug}] {e.Name} v{e.Version} — {e.Author} (★{e.Rating:F1}, {e.Downloads}次){featured}");
            }
        }

        return sb.ToString();
    }

    private static ToolResult Fail(string error) => new() { Name = "character_market", Success = false, Error = error };
}
