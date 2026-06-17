using System.Text.Json;

namespace DesktopMascot.Core.Character;

/// <summary>
/// Petdex 角色包 → 寻研角色包转换器
/// 只读取 Petdex 的 pet.json 中的兼容字段，转成寻研格式
/// </summary>
public class PetdexImportConverter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true
    };

    private static readonly Dictionary<string, string> PetdexToXunyanStateMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["idle"] = "Idle",
        ["wave"] = "Completed",
        ["run"] = "Working",
        ["failed"] = "Error",
        ["review"] = "WaitingApproval",
        ["jump"] = "Idle",
        ["extra1"] = "Idle",
        ["extra2"] = "Idle"
    };

    /// <summary>
    /// 从 Petdex 目录导入，生成寻研角色包
    /// </summary>
    public CharacterLoadResult ImportFromPetdexDirectory(string petdexDir)
    {
        var petJsonPath = Path.Combine(petdexDir, "pet.json");
        if (!File.Exists(petJsonPath))
            return CharacterLoadResult.Fail($"Petdex 目录缺少 pet.json：{petdexDir}");

        try
        {
            var json = File.ReadAllText(petJsonPath);
            return ImportFromPetdexJson(json, petdexDir);
        }
        catch (Exception ex)
        {
            return CharacterLoadResult.Fail($"读取 pet.json 失败：{ex.Message}");
        }
    }

    /// <summary>
    /// 从 Petdex JSON 字符串导入
    /// </summary>
    public CharacterLoadResult ImportFromPetdexJson(string petJson, string? sourceDir = null)
    {
        try
        {
            var petDoc = JsonDocument.Parse(petJson);
            var root = petDoc.RootElement;

            var name = GetStringProp(root, "name") ?? "Imported Pet";
            var slug = GetStringProp(root, "slug") ?? CreateSlug(name);
            var kind = GetStringProp(root, "kind") ?? "pet";

            // 解析动画状态
            var petdexStates = new Dictionary<string, PetdexStateEntry>();
            var xunyanStates = new Dictionary<string, CharacterStateMapping>();

            if (root.TryGetProperty("animationStates", out var statesEl))
            {
                foreach (var prop in statesEl.EnumerateObject())
                {
                    var petdexStateName = prop.Name;
                    var row = prop.Value.TryGetProperty("row", out var rEl) ? rEl.GetInt32() : 0;
                    var frames = prop.Value.TryGetProperty("frames", out var fEl) ? fEl.GetInt32() : 6;
                    var loopMs = prop.Value.TryGetProperty("loopMs", out var lEl) ? lEl.GetInt32() : 1100;

                    petdexStates[petdexStateName] = new PetdexStateEntry
                    {
                        PetdexState = petdexStateName,
                        Row = row,
                        Frames = frames,
                        LoopMs = loopMs
                    };

                    // 映射到寻研状态
                    if (PetdexToXunyanStateMap.TryGetValue(petdexStateName, out var xunyanState))
                    {
                        if (!xunyanStates.ContainsKey(xunyanState))
                        {
                            xunyanStates[xunyanState] = new CharacterStateMapping
                            {
                                DisplayName = GetDisplayName(xunyanState),
                                Image = "",
                                FallbackState = "Idle",
                                PetdexState = petdexStateName
                            };
                        }
                    }
                }
            }

            // 确保核心状态存在
            EnsureCoreStates(xunyanStates, petdexStates);

            // 解析帧尺寸
            var frameWidth = 192;
            var frameHeight = 208;
            if (root.TryGetProperty("frameSize", out var fsEl))
            {
                if (fsEl.TryGetProperty("width", out var wEl)) frameWidth = wEl.GetInt32();
                if (fsEl.TryGetProperty("height", out var hEl)) frameHeight = hEl.GetInt32();
            }

            // 解析网格
            var gridRows = 8;
            var gridCols = 9;
            if (root.TryGetProperty("grid", out var gEl))
            {
                if (gEl.TryGetProperty("rows", out var rowsEl)) gridRows = rowsEl.GetInt32();
                if (gEl.TryGetProperty("cols", out var colsEl)) gridCols = colsEl.GetInt32();
            }

            // 标签
            var tags = new List<string> { "imported-from-petdex", kind };
            if (root.TryGetProperty("tags", out var tagsEl) && tagsEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var tag in tagsEl.EnumerateArray())
                {
                    if (tag.GetString() is { Length: > 0 } t)
                        tags.Add(t);
                }
            }

            // 构建寻研角色包
            var manifest = new CharacterManifest
            {
                Schema = CharacterSchema.CurrentVersion,
                Version = "1.0",
                Slug = slug,
                Name = name,
                Kind = "desktop-assistant",
                Profile = new CharacterProfile
                {
                    Role = $"从 Petdex 导入的 {kind}",
                    Description = $"由 Petdex 社区角色 [{name}] 导入",
                    Personality = "友善",
                    ToneStyle = "Friendly",
                    LanguageStyle = "Standard",
                    ReplyLength = "Balanced",
                    Traits = new List<string> { "imported", kind }
                },
                Appearance = new CharacterAppearance
                {
                    AvatarText = name.Length > 0 ? name[..1] : "P",
                    ImageFolder = "assets/characters/" + slug,
                    AvatarImage = "avatar.png"
                },
                States = xunyanStates,
                Animation = new CharacterAnimation
                {
                    PrimaryMode = "state-images",
                    PetdexCompatibility = new PetdexCompat
                    {
                        Enabled = true,
                        SpriteSheet = "spritesheet.webp",
                        FrameWidth = frameWidth,
                        FrameHeight = frameHeight,
                        GridRows = gridRows,
                        GridColumns = gridCols,
                        StateMappings = petdexStates
                    }
                },
                Metadata = new CharacterMetadata
                {
                    Author = GetStringProp(root, "author") ?? "Petdex Community",
                    License = "Check Original",
                    Tags = tags,
                    Notes = "从 Petdex 格式导入。请检查原始角色许可证是否允许二次使用。"
                }
            };

            var warnings = new List<string>
            {
                "此角色从 Petdex 导入，请确认原始许可证允许使用",
                "精灵图需要手动放置到角色目录中"
            };

            return CharacterLoadResult.Ok(manifest, warnings);
        }
        catch (Exception ex)
        {
            return CharacterLoadResult.Fail($"解析 Petdex JSON 失败：{ex.Message}");
        }
    }

    private static void EnsureCoreStates(
        Dictionary<string, CharacterStateMapping> xunyanStates,
        Dictionary<string, PetdexStateEntry> petdexStates)
    {
        var requiredStates = new[] { "Idle", "Working", "Completed", "Error", "WaitingApproval" };
        foreach (var state in requiredStates)
        {
            if (!xunyanStates.ContainsKey(state))
            {
                xunyanStates[state] = new CharacterStateMapping
                {
                    DisplayName = GetDisplayName(state),
                    Image = "",
                    FallbackState = "Idle",
                    PetdexState = null
                };
            }
        }
    }

    private static string GetDisplayName(string state) => state switch
    {
        "Idle" => "空闲",
        "Working" => "工作中",
        "Completed" => "已完成",
        "Error" => "出错了",
        "WaitingApproval" => "等待确认",
        "Listening" => "监听中",
        "Understanding" => "理解中",
        "Planning" => "规划中",
        "MemoryConfirm" => "记忆确认",
        _ => state
    };

    private static string? GetStringProp(JsonElement el, string name)
    {
        return el.TryGetProperty(name, out var prop) && prop.ValueKind == JsonValueKind.String
            ? prop.GetString()
            : null;
    }

    private static string CreateSlug(string name)
    {
        return string.IsNullOrWhiteSpace(name)
            ? "imported-pet"
            : string.Join("-", name.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }
}
