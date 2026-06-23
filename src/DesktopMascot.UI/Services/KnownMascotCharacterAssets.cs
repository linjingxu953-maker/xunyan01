namespace DesktopMascot.UI.Services;

internal static class KnownMascotCharacterAssets
{
    private static readonly Dictionary<string, Preset> Presets = new(StringComparer.OrdinalIgnoreCase)
    {
        ["yan"] = new("微风", "寻研01桌面助手", "微", "#2563EB", "#EEF6FF"),
        ["yue guang"] = new("月光", "寻研01夜间助手", "月", "#7C3AED", "#F5F3FF"),
        ["feng lin yu ren"] = new("枫林渔人", "寻研01桌面助手", "枫", "#047857", "#ECFDF5")
    };

    public static MascotCharacterProfile? CreateProfile(string folderName, string imageFolder)
    {
        if (!Presets.TryGetValue(folderName, out var preset))
            return null;

        var profile = new MascotCharacterProfile
        {
            Name = preset.Name,
            Role = preset.Role,
            AvatarText = preset.AvatarText,
            Description = $"{preset.Name} 是寻研01的可切换桌面角色。",
            Personality = "沉稳可靠",
            ToneStyle = "友善",
            LanguageStyle = "标准",
            ReplyLength = "平衡",
            Catchphrase = $"我是{preset.Name}，可以继续接任务。",
            AccentColor = preset.AccentColor,
            BackgroundColor = preset.BackgroundColor,
            ImageFolder = imageFolder,
            AvatarImage = "avatar.png"
        };
        profile.EnsureImageDefaults();
        return profile;
    }

    public static string GetDirectorySortKey(DirectoryInfo directory)
    {
        var name = directory.Name;
        var knownIndex = name.ToLowerInvariant() switch
        {
            "feng lin yu ren" => 0,
            "yan" => 1,
            "yue guang" => 2,
            _ => 99
        };
        return $"{knownIndex:D2}-{name}";
    }

    private sealed record Preset(string Name, string Role, string AvatarText, string AccentColor, string BackgroundColor);
}
