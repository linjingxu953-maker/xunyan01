using Avalonia.Controls;
using Avalonia.Media;
using DesktopMascot.Core.Enums;
using DesktopMascot.Core.Memory;
using DesktopMascot.UI.Services;

namespace DesktopMascot.UI.ViewModels;

/// <summary>FloatingWindowViewModel — 角色管理、外观和工具方法</summary>
public partial class FloatingWindowViewModel
{
    private void OnCharacterProfileChanged(object? sender, EventArgs e)
    {
        if (_isApplyingCharacterProfile) return;
        ApplyCharacterProfile(_characterStore.Load(), save: false);
        CharacterSaveStatus = "角色外观已从设置中心更新。";
    }

    public void SetInlineSettingsOwner(Window? owner) => _inlineSettingsOwner = owner;

    // ── 角色外观加载/保存 ──
    private void ApplyCharacterProfile(MascotCharacterProfile profile, bool save)
    {
        profile.EnsureImageDefaults();
        _isApplyingCharacterProfile = true;
        try
        {
            CharacterName = CleanText(profile.Name, "妍", 12);
            CharacterRole = CleanText(profile.Role, "寻研桌面助手", 24);
            CharacterAvatarText = CleanText(profile.AvatarText, "妍", 4);
            CharacterDescription = CleanText(profile.Description, "主动理解屏幕与任务上下文，清晰地给出下一步。", 120);
            CharacterPersonality = CleanText(profile.Personality, "沉稳可靠", 12);
            CharacterToneStyle = CleanText(profile.ToneStyle, "友善", 12);
            CharacterLanguageStyle = CleanText(profile.LanguageStyle, "标准", 12);
            CharacterReplyLength = CleanText(profile.ReplyLength, "平衡", 12);
            CharacterUseEmoji = profile.UseEmoji;
            CharacterSystemPromptSuffix = CleanText(profile.SystemPromptSuffix, string.Empty, 500);
            CharacterCatchphrase = CleanText(profile.Catchphrase, "我在桌面待命，随时可以接任务。", 40);
            CharacterAccentColor = NormalizeHexColor(profile.AccentColor, "#2563EB");
            CharacterBackgroundColor = NormalizeHexColor(profile.BackgroundColor, "#EEF6FF");
            CharacterImageFolder = CleanPathText(profile.ImageFolder, "assets/characters/default", 160);
            CharacterAvatarImage = CleanPathText(profile.AvatarImage, "avatar.png", 80);
            _characterStateImages = new Dictionary<string, string>(profile.StateImages);
            _characterPersonalityTraits = profile.PersonalityTraits.Count == 0 ? ["可靠", "主动"] : [..profile.PersonalityTraits];
            RefreshCharacterBrushes();
            RefreshCharacterImage();
        }
        finally { _isApplyingCharacterProfile = false; }
        if (CurrentState == MascotState.Idle) StatusMessage = CharacterCatchphrase;
        if (save) _characterStore.Save(BuildCurrentCharacterProfile());
    }

    private MascotCharacterProfile BuildCurrentCharacterProfile() => new()
    {
        Name = CleanText(CharacterName, "妍", 12), Role = CleanText(CharacterRole, "寻研桌面助手", 24), AvatarText = CleanText(CharacterAvatarText, "妍", 4),
        Description = CleanText(CharacterDescription, "主动理解屏幕与任务上下文，清晰地给出下一步。", 120), Personality = CleanText(CharacterPersonality, "沉稳可靠", 12),
        ToneStyle = CleanText(CharacterToneStyle, "友善", 12), LanguageStyle = CleanText(CharacterLanguageStyle, "标准", 12), ReplyLength = CleanText(CharacterReplyLength, "平衡", 12),
        UseEmoji = CharacterUseEmoji, SystemPromptSuffix = CleanText(CharacterSystemPromptSuffix, string.Empty, 500),
        PersonalityTraits = [.._characterPersonalityTraits], Catchphrase = CleanText(CharacterCatchphrase, "我在桌面待命，随时可以接任务。", 40),
        AccentColor = NormalizeHexColor(CharacterAccentColor, "#2563EB"), BackgroundColor = NormalizeHexColor(CharacterBackgroundColor, "#EEF6FF"),
        ImageFolder = CleanPathText(CharacterImageFolder, "assets/characters/default", 160), AvatarImage = CleanPathText(CharacterAvatarImage, "avatar.png", 80),
        StateImages = new Dictionary<string, string>(_characterStateImages)
    };

    private void RefreshCharacterBrushes() { StateAccentBrush = GetAccentBrush(CurrentState); MascotBackgroundBrush = GetMascotBackgroundBrush(CurrentState); }

    private void RefreshCharacterImage()
    {
        var result = _characterImageService.Resolve(BuildCurrentCharacterProfile(), CurrentState);
        CharacterImageSource = result.Image; HasCharacterImage = result.HasImage; CharacterImageStatus = result.Message;
    }

    // ── 画刷 ──
    private IBrush GetAccentBrush(MascotState state) => state switch { MascotState.WaitingApproval => BrushFrom("#D97706"), MascotState.Completed => BrushFrom("#16A34A"), MascotState.Error => BrushFrom("#DC2626"), _ => BrushFrom(CharacterAccentColor) };
    private IBrush GetMascotBackgroundBrush(MascotState state) => state switch { MascotState.WaitingApproval => BrushFrom("#FFF7ED"), MascotState.Completed => BrushFrom("#F0FDF4"), MascotState.Error => BrushFrom("#FEF2F2"), _ => BrushFrom(CharacterBackgroundColor) };

    // ── 文本工具 ──
    private static string CleanText(string? value, string fallback, int maxLength) { var t = string.IsNullOrWhiteSpace(value) ? fallback : value.Trim(); return t.Length <= maxLength ? t : t[..maxLength]; }
    private static string CleanPathText(string? value, string fallback, int maxLength) => CleanText(value, fallback, maxLength).Replace('\\', '/');
    private static string NormalizeHexColor(string? value, string fallback) { if (string.IsNullOrWhiteSpace(value)) return fallback; var c = value.Trim(); if (c is not ['#', ..] || c.Length != 7) return fallback; try { Color.Parse(c); return c.ToUpperInvariant(); } catch { return fallback; } }
    private static IBrush BrushFrom(string hex) => new SolidColorBrush(Color.Parse(hex));
}
