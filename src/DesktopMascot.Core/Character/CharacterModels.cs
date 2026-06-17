using System.Text.Json;

namespace DesktopMascot.Core.Character;

/// <summary>
/// 角色包 Schema 版本
/// </summary>
public static class CharacterSchema
{
    public const string CurrentVersion = "xunyan.character.v1";
}

/// <summary>
/// 角色包 Manifest — 寻研自有格式（非 Petdex 格式）
/// </summary>
public class CharacterManifest
{
    public string Schema { get; set; } = CharacterSchema.CurrentVersion;
    public string Version { get; set; } = "1.0";
    public string Slug { get; set; } = "";
    public string Name { get; set; } = "";
    public string Kind { get; set; } = "desktop-assistant";
    public CharacterProfile Profile { get; set; } = new();
    public CharacterAppearance Appearance { get; set; } = new();
    public Dictionary<string, CharacterStateMapping> States { get; set; } = new();
    public CharacterAnimation Animation { get; set; } = new();
    public CharacterMetadata Metadata { get; set; } = new();
}

public class CharacterProfile
{
    public string Role { get; set; } = "";
    public string Description { get; set; } = "";
    public string Personality { get; set; } = "";
    public string ToneStyle { get; set; } = "Friendly";
    public string LanguageStyle { get; set; } = "Standard";
    public string ReplyLength { get; set; } = "Balanced";
    public bool UseEmoji { get; set; }
    public string Catchphrase { get; set; } = "";
    public List<string> Traits { get; set; } = new();
    public string SystemPromptSuffix { get; set; } = "";
}

public class CharacterAppearance
{
    public string AvatarText { get; set; } = "";
    public string ImageFolder { get; set; } = "";
    public string AvatarImage { get; set; } = "avatar.png";
    public string AccentColor { get; set; } = "#2563EB";
    public string BackgroundColor { get; set; } = "#EEF6FF";
}

/// <summary>
/// 角色状态映射 — 每个状态对应的显示名、图片、回退状态、可选 Petdex 映射
/// </summary>
public class CharacterStateMapping
{
    public string DisplayName { get; set; } = "";
    public string Image { get; set; } = "";
    public string FallbackState { get; set; } = "Idle";
    public string? PetdexState { get; set; }
}

/// <summary>
/// 动画配置 — 主模式为 state-images（静态图片切换），可选 Lottie 和 Petdex 精灵图
/// </summary>
public class CharacterAnimation
{
    public string PrimaryMode { get; set; } = "state-images";
    public Dictionary<string, string> LottieAnimations { get; set; } = new();
    public PetdexCompat PetdexCompatibility { get; set; } = new();
}

public class PetdexCompat
{
    public bool Enabled { get; set; }
    public string SpriteSheet { get; set; } = "spritesheet.webp";
    public int FrameWidth { get; set; } = 192;
    public int FrameHeight { get; set; } = 208;
    public int GridRows { get; set; } = 8;
    public int GridColumns { get; set; } = 9;
    public Dictionary<string, PetdexStateEntry> StateMappings { get; set; } = new();
}

public class PetdexStateEntry
{
    public string PetdexState { get; set; } = "";
    public int Row { get; set; }
    public int Frames { get; set; } = 6;
    public int LoopMs { get; set; } = 1100;
}

public class CharacterMetadata
{
    public string Author { get; set; } = "";
    public string License { get; set; } = "MIT";
    public List<string> Tags { get; set; } = new();
    public string Notes { get; set; } = "";
}

/// <summary>
/// 角色包验证结果
/// </summary>
public class CharacterValidationResult
{
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();

    public static CharacterValidationResult Success() => new() { IsValid = true };
    public static CharacterValidationResult Fail(params string[] errors) => new() { IsValid = false, Errors = errors.ToList() };
}
