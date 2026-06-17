using DesktopMascot.Core.Character;

namespace DesktopMascot.Agent.Models;

/// <summary>
/// 角色包 Manifest → AgentPersonality 转换器
/// </summary>
public static class CharacterToPersonalityConverter
{
    public static AgentPersonality Convert(CharacterManifest manifest)
    {
        var profile = manifest.Profile ?? new CharacterProfile();

        return new AgentPersonality
        {
            Name = manifest.Name,
            Description = profile.Description ?? $"{manifest.Name} — {profile.Role}",
            Tone = ParseToneStyle(profile.ToneStyle),
            Language = ParseLanguageStyle(profile.LanguageStyle),
            Traits = profile.Traits?.ToList() ?? new List<string>(),
            Catchphrase = profile.Catchphrase,
            LengthPreference = ParseResponseLength(profile.ReplyLength),
            UseEmoji = profile.UseEmoji,
            CustomSystemPromptSuffix = profile.SystemPromptSuffix
        };
    }

    private static ToneStyle ParseToneStyle(string? value) => value?.ToLowerInvariant() switch
    {
        "friendly" or "友善" => ToneStyle.Friendly,
        "professional" or "专业" => ToneStyle.Professional,
        "casual" or "轻松" => ToneStyle.Casual,
        "cute" or "可爱" => ToneStyle.Cute,
        "calm" or "沉稳" => ToneStyle.Calm,
        "sarcastic" or "讽刺" => ToneStyle.Sarcastic,
        _ => ToneStyle.Friendly
    };

    private static LanguageStyle ParseLanguageStyle(string? value) => value?.ToLowerInvariant() switch
    {
        "standard" or "标准" => LanguageStyle.Standard,
        "concise" or "简洁" => LanguageStyle.Concise,
        "detailed" or "详细" => LanguageStyle.Detailed,
        "technical" or "技术" => LanguageStyle.Technical,
        "colloquial" or "口语" => LanguageStyle.Colloquial,
        _ => LanguageStyle.Standard
    };

    private static ResponseLength ParseResponseLength(string? value) => value?.ToLowerInvariant() switch
    {
        "short" or "简短" => ResponseLength.Short,
        "balanced" or "平衡" => ResponseLength.Balanced,
        "detailed" or "详细" => ResponseLength.Detailed,
        _ => ResponseLength.Balanced
    };
}
