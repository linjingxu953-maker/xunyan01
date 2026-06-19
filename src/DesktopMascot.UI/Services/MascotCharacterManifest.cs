using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace DesktopMascot.UI.Services;

public sealed class MascotCharacterManifest
{
    public string Schema { get; init; } = MascotCharacterManifestFactory.Schema;
    public string Version { get; init; } = "1.0";
    public string Slug { get; init; } = "feng-lin-yu-ren";
    public string Name { get; init; } = "枫林渔人";
    public string Kind { get; init; } = "desktop-assistant-character";
    public MascotCharacterPersonaManifest Persona { get; init; } = new();
    public MascotCharacterAppearanceManifest Appearance { get; init; } = new();
    public Dictionary<string, MascotCharacterStateManifest> States { get; init; } = [];
    public MascotCharacterAnimationManifest Animation { get; init; } = new();
    public MascotCharacterMetadataManifest Metadata { get; init; } = new();
}

public sealed class MascotCharacterPersonaManifest
{
    public string Role { get; init; } = "寻研01桌面助手";
    public string Description { get; init; } = string.Empty;
    public string Personality { get; init; } = string.Empty;
    public string ToneStyle { get; init; } = "友善";
    public string LanguageStyle { get; init; } = "标准";
    public string ReplyLength { get; init; } = "平衡";
    public bool UseEmoji { get; init; }
    public string Catchphrase { get; init; } = string.Empty;
    public IReadOnlyList<string> Traits { get; init; } = [];
    public string SystemPromptSuffix { get; init; } = string.Empty;
}

public sealed class MascotCharacterAppearanceManifest
{
    public string AvatarText { get; init; } = "枫";
    public string ImageFolder { get; init; } = "assets/characters/feng lin yu ren";
    public string AvatarImage { get; init; } = "avatar.png";
    public string AccentColor { get; init; } = "#2563EB";
    public string BackgroundColor { get; init; } = "#EEF6FF";
}

public sealed class MascotCharacterStateManifest
{
    public string DisplayName { get; init; } = string.Empty;
    public string Image { get; init; } = string.Empty;
    public string FallbackState { get; init; } = "Idle";
    public string? PetdexState { get; init; }
}

public sealed class MascotCharacterAnimationManifest
{
    public string PrimaryMode { get; init; } = "state-images";
    public Dictionary<string, string> LottieAnimations { get; init; } = [];
    public PetdexCompatibilityManifest PetdexCompatibility { get; init; } = new();
}

public sealed class PetdexCompatibilityManifest
{
    public bool Enabled { get; init; }
    public string Mode { get; init; } = "optional-import-only";
    public string SpriteSheet { get; init; } = "spritesheet.webp";
    public ManifestSize FrameSize { get; init; } = new(192, 208);
    public ManifestGrid Grid { get; init; } = new(8, 9);
    public Dictionary<string, PetdexStateManifest> StateMappings { get; init; } = new()
    {
        ["Idle"] = new("idle", 0, 6, 1100),
        ["Working"] = new("run", 2, 6, 1100),
        ["WaitingApproval"] = new("review", 4, 6, 1100),
        ["Completed"] = new("wave", 1, 6, 1100),
        ["Error"] = new("failed", 3, 6, 1100)
    };
}

public sealed record ManifestSize(int Width, int Height);

public sealed record ManifestGrid(int Rows, int Columns);

public sealed record PetdexStateManifest(string State, int Row, int Frames, int LoopMs);

public sealed class MascotCharacterMetadataManifest
{
    public string Author { get; init; } = "User";
    public string License { get; init; } = "User Provided";
    public IReadOnlyList<string> Tags { get; init; } = ["desktop-assistant", "xunyan"];
    public string Notes { get; init; } = "Petdex fields are optional compatibility metadata, not the primary character model.";
}

public static class MascotCharacterManifestFactory
{
    public const string Schema = "xunyan.character.v1";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private static readonly Dictionary<string, string> PetdexStateHints = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Idle"] = "idle",
        ["Listening"] = "idle",
        ["Understanding"] = "review",
        ["ReadingContext"] = "review",
        ["Planning"] = "review",
        ["WaitingApproval"] = "review",
        ["Working"] = "run",
        ["MemoryConfirm"] = "review",
        ["Reporting"] = "wave",
        ["Completed"] = "wave",
        ["Error"] = "failed"
    };

    public static MascotCharacterManifest Create(
        MascotCharacterProfile profile,
        IReadOnlyDictionary<string, string> stateDisplayNames)
    {
        profile.EnsureImageDefaults();
        var slug = CreateSlug(profile.Name);

        return new MascotCharacterManifest
        {
            Slug = slug,
            Name = profile.Name,
            Persona = new MascotCharacterPersonaManifest
            {
                Role = profile.Role,
                Description = profile.Description,
                Personality = profile.Personality,
                ToneStyle = profile.ToneStyle,
                LanguageStyle = profile.LanguageStyle,
                ReplyLength = profile.ReplyLength,
                UseEmoji = profile.UseEmoji,
                Catchphrase = profile.Catchphrase,
                Traits = [..profile.PersonalityTraits],
                SystemPromptSuffix = profile.SystemPromptSuffix
            },
            Appearance = new MascotCharacterAppearanceManifest
            {
                AvatarText = profile.AvatarText,
                ImageFolder = profile.ImageFolder,
                AvatarImage = profile.AvatarImage,
                AccentColor = profile.AccentColor,
                BackgroundColor = profile.BackgroundColor
            },
            States = profile.StateImages.ToDictionary(
                item => item.Key,
                item => new MascotCharacterStateManifest
                {
                    DisplayName = stateDisplayNames.TryGetValue(item.Key, out var displayName)
                        ? displayName
                        : item.Key,
                    Image = item.Value,
                    FallbackState = string.Equals(item.Key, "Idle", StringComparison.OrdinalIgnoreCase)
                        ? "Idle"
                        : "Idle",
                    PetdexState = PetdexStateHints.GetValueOrDefault(item.Key)
                },
                StringComparer.OrdinalIgnoreCase)
        };
    }

    public static string ToJson(MascotCharacterManifest manifest)
    {
        return JsonSerializer.Serialize(manifest, SerializerOptions);
    }

    public static MascotCharacterManifest? FromJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        return JsonSerializer.Deserialize<MascotCharacterManifest>(json, SerializerOptions);
    }

    public static MascotCharacterProfile ToProfile(MascotCharacterManifest manifest, string? manifestPath = null)
    {
        var profile = new MascotCharacterProfile
        {
            Name = CleanText(manifest.Name, "枫林渔人"),
            Role = CleanText(manifest.Persona.Role, "寻研01桌面助手"),
            AvatarText = CleanText(manifest.Appearance.AvatarText, "枫"),
            Description = CleanText(manifest.Persona.Description, string.Empty),
            Personality = CleanText(manifest.Persona.Personality, "沉稳可靠"),
            ToneStyle = CleanText(manifest.Persona.ToneStyle, "友善"),
            LanguageStyle = CleanText(manifest.Persona.LanguageStyle, "标准"),
            ReplyLength = CleanText(manifest.Persona.ReplyLength, "平衡"),
            UseEmoji = manifest.Persona.UseEmoji,
            SystemPromptSuffix = manifest.Persona.SystemPromptSuffix ?? string.Empty,
            PersonalityTraits = [..manifest.Persona.Traits],
            Catchphrase = CleanText(manifest.Persona.Catchphrase, "我在桌面待命，随时可以接任务。"),
            AccentColor = CleanText(manifest.Appearance.AccentColor, "#2563EB"),
            BackgroundColor = CleanText(manifest.Appearance.BackgroundColor, "#EEF6FF"),
            ImageFolder = ResolveImportImageFolder(manifest.Appearance.ImageFolder, manifestPath),
            AvatarImage = CleanText(manifest.Appearance.AvatarImage, "avatar.png"),
            StateImages = manifest.States.ToDictionary(
                item => item.Key,
                item => CleanText(item.Value.Image, "avatar.png"),
                StringComparer.OrdinalIgnoreCase)
        };

        profile.EnsureImageDefaults();
        return profile;
    }

    public static string CreateSlug(string? name)
    {
        var text = string.IsNullOrWhiteSpace(name) ? "character" : name.Trim();
        if (text.Contains("微风", StringComparison.Ordinal))
        {
            return "wei-feng";
        }

        if (text.Contains("枫林渔人", StringComparison.Ordinal))
        {
            return "feng-lin-yu-ren";
        }

        var builder = new StringBuilder();
        var lastWasDash = false;
        foreach (var character in text)
        {
            if (character is >= 'A' and <= 'Z')
            {
                builder.Append(char.ToLowerInvariant(character));
                lastWasDash = false;
            }
            else if (character is >= 'a' and <= 'z' or >= '0' and <= '9')
            {
                builder.Append(character);
                lastWasDash = false;
            }
            else if (char.IsWhiteSpace(character) || character is '-' or '_')
            {
                if (!lastWasDash && builder.Length > 0)
                {
                    builder.Append('-');
                    lastWasDash = true;
                }
            }
        }

        var slug = builder.ToString().Trim('-');
        if (!string.IsNullOrWhiteSpace(slug))
        {
            return slug;
        }

        var bytes = Encoding.UTF8.GetBytes(text);
        var hash = Convert.ToHexString(bytes);
        return $"character-{hash[..Math.Min(hash.Length, 8)].ToLowerInvariant()}";
    }

    private static string ResolveImportImageFolder(string? imageFolder, string? manifestPath)
    {
        var folder = CleanText(imageFolder, "assets/characters/feng lin yu ren")
            .Replace('/', Path.DirectorySeparatorChar);

        if (Path.IsPathRooted(folder))
            return folder;

        var packageRoot = string.IsNullOrWhiteSpace(manifestPath)
            ? null
            : Path.GetDirectoryName(manifestPath);

        return string.IsNullOrWhiteSpace(packageRoot)
            ? folder
            : Path.GetFullPath(Path.Combine(packageRoot, folder));
    }

    private static string CleanText(string? value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    }
}
