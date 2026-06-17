using System.Text.Json;

namespace DesktopMascot.Core.Character;

/// <summary>
/// 角色包加载器 — 从目录加载、验证、解析角色包
/// </summary>
public class CharacterPackageLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip
    };

    /// <summary>
    /// 从目录加载角色包
    /// </summary>
    public CharacterLoadResult LoadFromDirectory(string characterDir)
    {
        var manifestPath = Path.Combine(characterDir, "character.json");
        if (!File.Exists(manifestPath))
            return CharacterLoadResult.Fail($"角色目录缺少 character.json：{characterDir}");

        try
        {
            var json = File.ReadAllText(manifestPath);
            var manifest = JsonSerializer.Deserialize<CharacterManifest>(json, JsonOptions);
            if (manifest == null)
                return CharacterLoadResult.Fail("character.json 解析为空");

            // 校验
            var validation = Validate(manifest);
            if (!validation.IsValid)
                return CharacterLoadResult.Fail(validation.Errors.ToArray());

            // 检查图片资源
            var resourceWarnings = CheckResources(manifest, characterDir);

            return CharacterLoadResult.Ok(manifest, resourceWarnings);
        }
        catch (JsonException ex)
        {
            return CharacterLoadResult.Fail($"character.json 解析错误：{ex.Message}");
        }
    }

    /// <summary>
    /// 从 JSON 字符串加载角色包
    /// </summary>
    public CharacterLoadResult LoadFromJson(string json)
    {
        try
        {
            var manifest = JsonSerializer.Deserialize<CharacterManifest>(json, JsonOptions);
            if (manifest == null)
                return CharacterLoadResult.Fail("JSON 解析为空");

            var validation = Validate(manifest);
            if (!validation.IsValid)
                return CharacterLoadResult.Fail(validation.Errors.ToArray());

            return CharacterLoadResult.Ok(manifest);
        }
        catch (JsonException ex)
        {
            return CharacterLoadResult.Fail($"JSON 解析错误：{ex.Message}");
        }
    }

    /// <summary>
    /// 校验角色包是否合法
    /// </summary>
    public CharacterValidationResult Validate(CharacterManifest manifest)
    {
        var errors = new List<string>();

        // Schema 校验
        if (manifest.Schema != CharacterSchema.CurrentVersion)
            errors.Add($"Schema 版本不匹配：期望 {CharacterSchema.CurrentVersion}，实际 {manifest.Schema}");

        // 基本信息
        if (string.IsNullOrWhiteSpace(manifest.Name))
            errors.Add("角色名称不能为空");
        if (string.IsNullOrWhiteSpace(manifest.Slug))
            errors.Add("角色标识（slug）不能为空");

        // 人格设定
        if (manifest.Profile == null)
            errors.Add("缺少人格设定（profile）");
        else if (string.IsNullOrWhiteSpace(manifest.Profile.ToneStyle))
            errors.Add("人格设定中语气风格不能为空");

        // 外观
        if (manifest.Appearance == null)
            errors.Add("缺少外观配置（appearance）");

        // 状态映射
        if (manifest.States == null || manifest.States.Count == 0)
            errors.Add("至少需要一个状态映射");
        else if (!manifest.States.ContainsKey("Idle"))
            errors.Add("必须包含 Idle（空闲）状态");

        return errors.Count > 0
            ? CharacterValidationResult.Fail(errors.ToArray())
            : CharacterValidationResult.Success();
    }

    /// <summary>
    /// 检查图片资源是否存在
    /// </summary>
    private List<string> CheckResources(CharacterManifest manifest, string characterDir)
    {
        var warnings = new List<string>();

        if (manifest.Appearance != null)
        {
            // 检查头像
            var avatarPath = Path.Combine(characterDir, manifest.Appearance.ImageFolder, manifest.Appearance.AvatarImage);
            if (!File.Exists(avatarPath))
                warnings.Add($"头像文件不存在：{avatarPath}");

            // 检查各状态图片
            if (manifest.States != null)
            {
                foreach (var (stateName, state) in manifest.States)
                {
                    if (!string.IsNullOrEmpty(state.Image))
                    {
                        var imgPath = Path.Combine(characterDir, manifest.Appearance.ImageFolder, state.Image);
                        if (!File.Exists(imgPath))
                            warnings.Add($"状态 [{stateName}] 图片不存在：{state.Image}，将回退到 Idle");
                    }
                }
            }
        }

        // Petdex 兼容检查
        if (manifest.Animation?.PetdexCompatibility?.Enabled == true)
        {
            var spritePath = Path.Combine(characterDir, manifest.Appearance?.ImageFolder ?? "", manifest.Animation.PetdexCompatibility.SpriteSheet);
            if (!File.Exists(spritePath))
                warnings.Add($"Petdex 精灵图不存在：{manifest.Animation.PetdexCompatibility.SpriteSheet}");
        }

        return warnings;
    }
}

/// <summary>
/// 角色包加载结果
/// </summary>
public class CharacterLoadResult
{
    public bool Success { get; set; }
    public CharacterManifest? Manifest { get; set; }
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();

    public static CharacterLoadResult Ok(CharacterManifest manifest, List<string>? warnings = null)
        => new() { Success = true, Manifest = manifest, Warnings = warnings ?? new() };

    public static CharacterLoadResult Fail(params string[] errors)
        => new() { Success = false, Errors = errors.ToList() };
}
