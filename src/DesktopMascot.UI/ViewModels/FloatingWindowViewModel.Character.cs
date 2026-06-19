using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using DesktopMascot.Core.Enums;
using DesktopMascot.Core.Memory;
using DesktopMascot.UI.Services;

namespace DesktopMascot.UI.ViewModels;

/// <summary>FloatingWindowViewModel — 角色管理、外观和工具方法</summary>
public partial class FloatingWindowViewModel
{
    private static readonly Dictionary<string, CharacterAssetPreset> CharacterAssetPresets = new(StringComparer.OrdinalIgnoreCase)
    {
        ["default"] = new("妍", "寻研桌面助手", "妍", "#2563EB", "#EEF6FF"),
        ["yan"] = new("妍", "寻研桌面助手", "妍", "#2563EB", "#EEF6FF"),
        ["yue guang"] = new("月光", "寻研夜间助手", "月", "#7C3AED", "#F5F3FF"),
        ["feng lin yu ren"] = new("枫林渔人", "寻研桌面助手", "枫", "#047857", "#ECFDF5"),
        ["developer"] = new("码伴", "开发调试伙伴", "</>", "#0F766E", "#F0FDFA"),
        ["operator"] = new("桌管家", "桌面任务管家", "管", "#7C2D12", "#FFF7ED"),
        ["study"] = new("小研", "阅读研究助手", "研", "#7C3AED", "#F5F3FF")
    };

    private static readonly HashSet<string> SupportedCharacterImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png",
        ".jpg",
        ".jpeg",
        ".bmp",
        ".webp"
    };

    private static readonly IReadOnlyList<CharacterStateAssetRequirement> CharacterStateAssetRequirements =
    [
        new(MascotState.Idle, "空闲", ["idle.png", "avatar.png", "pose.png", "站立.png"]),
        new(MascotState.Listening, "聆听", ["listening.png", "Listening：看向用户.png", "Listening:看向用户.png"]),
        new(MascotState.Understanding, "理解", ["thinking.png", "understanding.png", "Understanding：思考图.png", "Understanding:思考图.png"]),
        new(MascotState.ReadingContext, "读取", ["reading.png", "understanding.png", "Understanding：思考图.png", "Understanding:思考图.png"]),
        new(MascotState.Planning, "规划", ["planning.png", "thinking.png", "Understanding：思考图.png", "Understanding:思考图.png"]),
        new(MascotState.WaitingApproval, "确认", ["waiting.png", "WaitingApproval：提醒、举手图.png", "WaitingApproval:提醒、举手图.png"]),
        new(MascotState.Working, "工作", ["working.png", "Working：忙碌图.png", "Working:忙碌图.png"]),
        new(MascotState.MemoryConfirm, "记忆", ["memory.png", "WaitingApproval：提醒、举手图.png", "WaitingApproval:提醒、举手图.png"]),
        new(MascotState.Reporting, "汇报", ["reporting.png", "working.png", "Working：忙碌图.png", "Working:忙碌图.png"]),
        new(MascotState.Completed, "完成", ["completed.png", "Completed：开心、完成图.png", "Completed:开心图.png", "Completed:开心、完成图.png"]),
        new(MascotState.Error, "错误", ["error.png", "Error：困惑、错误图.png", "Error:困惑、错误图.png"])
    ];

    private void OnCharacterProfileChanged(object? sender, EventArgs e)
    {
        if (_isApplyingCharacterProfile) return;
        ApplyCharacterProfile(_characterStore.Load(), save: false);
        RefreshCharacterSwitchItems();
        CharacterSaveStatus = "角色外观已从设置中心更新。";
    }

    public void SetInlineSettingsOwner(Window? owner) => _inlineSettingsOwner = owner;

    public void SetCharacterAssetRootCandidates(IReadOnlyList<string>? rootCandidates)
    {
        _characterAssetRootCandidates = rootCandidates;
        RefreshCharacterSwitchItems();
    }

    public void SwitchCharacterProfile(CharacterProfileListItem? item)
    {
        if (item is null)
            return;

        var profile = item.Id.StartsWith("asset:", StringComparison.OrdinalIgnoreCase)
            ? BuildAssetCharacterProfile(item.Entry)
            : _characterStore.LoadProfile(item.Id) ?? BuildAssetCharacterProfile(item.Entry);

        ApplyCharacterProfile(profile, save: true);
        RefreshCharacterSwitchItems(item.Id);
        CharacterSaveStatus = $"已切换到 {CharacterName}。";
        StatusMessage = CharacterCatchphrase;
    }

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

    private void RefreshCharacterSwitchItems(string? selectedId = null)
    {
        CharacterSwitchItems.Clear();
        var seenFolders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in _characterStore.ListProfiles())
        {
            CharacterSwitchItems.Add(CreateCharacterProfileListItem(entry));
            if (!string.IsNullOrWhiteSpace(entry.ImageFolder))
                seenFolders.Add(NormalizeCharacterFolderKey(entry.ImageFolder));
        }

        foreach (var entry in EnumerateCharacterAssetEntries(seenFolders))
            CharacterSwitchItems.Add(CreateCharacterProfileListItem(entry));

        RefreshCharacterSwitchState();
    }

    private IEnumerable<MascotCharacterProfileEntry> EnumerateCharacterAssetEntries(ISet<string> seenFolders)
    {
        foreach (var charactersRoot in EnumerateCharacterAssetRoots())
        {
            DirectoryInfo root;
            try
            {
                root = new DirectoryInfo(charactersRoot);
            }
            catch
            {
                continue;
            }

            if (!root.Exists)
                continue;

            foreach (var directory in root.EnumerateDirectories().OrderBy(GetCharacterDirectorySortKey))
            {
                var folderName = directory.Name;
                var imageFolder = $"assets/characters/{folderName}";
                if (!seenFolders.Add(NormalizeCharacterFolderKey(imageFolder)))
                    continue;

                yield return CreateAssetEntry(directory, imageFolder);
            }
        }
    }

    private IEnumerable<string> EnumerateCharacterAssetRoots()
    {
        var roots = _characterAssetRootCandidates ?? EnumerateDefaultCharacterRootCandidates().ToArray();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var root in roots)
        {
            if (string.IsNullOrWhiteSpace(root))
                continue;

            var direct = Path.Combine(root, "assets", "characters");
            if (seen.Add(direct))
                yield return direct;

            var normalized = root.Replace('/', Path.DirectorySeparatorChar);
            if (Path.GetFileName(normalized).Equals("characters", StringComparison.OrdinalIgnoreCase) &&
                seen.Add(normalized))
            {
                yield return normalized;
            }
        }
    }

    private static IEnumerable<string> EnumerateDefaultCharacterRootCandidates()
    {
        yield return Environment.CurrentDirectory;

        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            yield return directory.FullName;
            directory = directory.Parent;
        }
    }

    private MascotCharacterProfileEntry CreateAssetEntry(DirectoryInfo directory, string imageFolder)
    {
        var profile = CreateAssetCharacterProfile(directory.Name, imageFolder);
        var avatarPath = ResolveFirstCharacterImagePath(directory.FullName, profile.AvatarImage)
            ?? ResolveFirstCharacterImagePath(directory.FullName, "idle.png")
            ?? Directory.EnumerateFiles(directory.FullName)
                .FirstOrDefault(file => SupportedCharacterImageExtensions.Contains(Path.GetExtension(file)))
            ?? string.Empty;

        return new MascotCharacterProfileEntry
        {
            Id = $"asset:{directory.Name}",
            Name = profile.Name,
            Role = profile.Role,
            ImageFolder = profile.ImageFolder,
            AvatarImagePath = avatarPath,
            AccentColor = profile.AccentColor,
            IsActive = IsCurrentCharacterAsset(profile),
            UpdatedAt = directory.LastWriteTimeUtc
        };
    }

    private CharacterProfileListItem CreateCharacterProfileListItem(MascotCharacterProfileEntry entry)
    {
        var profile = entry.Id.StartsWith("asset:", StringComparison.OrdinalIgnoreCase)
            ? BuildAssetCharacterProfile(entry)
            : _characterStore.LoadProfile(entry.Id);
        var readiness = ResolveCharacterStateImageReadiness(profile ?? BuildAssetCharacterProfile(entry));
        return new(
            entry,
            LoadCharacterProfileThumbnail(entry.AvatarImagePath),
            readiness.AvailableCount,
            readiness.TotalCount,
            readiness.MissingText);
    }

    private static IImage? LoadCharacterProfileThumbnail(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            return null;

        try
        {
            using var stream = File.OpenRead(filePath);
            return new Bitmap(stream);
        }
        catch
        {
            return null;
        }
    }

    private MascotCharacterProfile BuildAssetCharacterProfile(MascotCharacterProfileEntry entry)
    {
        var folderName = ExtractCharacterAssetFolderName(entry.ImageFolder);
        return CreateAssetCharacterProfile(folderName, entry.ImageFolder);
    }

    private static MascotCharacterProfile CreateAssetCharacterProfile(string folderName, string imageFolder)
    {
        var preset = CharacterAssetPresets.TryGetValue(folderName, out var matched)
            ? matched
            : new CharacterAssetPreset(FormatCharacterFolderName(folderName), "寻研桌面助手", FormatAvatarText(folderName), "#2563EB", "#EEF6FF");

        var profile = new MascotCharacterProfile
        {
            Name = preset.Name,
            Role = preset.Role,
            AvatarText = preset.AvatarText,
            Description = $"{preset.Name} 是寻研的可切换桌面角色。",
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

    private bool IsCurrentCharacterAsset(MascotCharacterProfile profile) =>
        string.Equals(NormalizeCharacterFolderKey(profile.ImageFolder), NormalizeCharacterFolderKey(CharacterImageFolder), StringComparison.OrdinalIgnoreCase)
        || string.Equals(profile.Name, CharacterName, StringComparison.OrdinalIgnoreCase);

    private void RefreshCharacterSwitchState()
    {
        OnPropertyChanged(nameof(HasCharacterSwitchItems));
        OnPropertyChanged(nameof(HasNoCharacterSwitchItems));
    }

    private static string? ResolveFirstCharacterImagePath(string folder, string imageFile)
    {
        if (string.IsNullOrWhiteSpace(folder) || string.IsNullOrWhiteSpace(imageFile))
            return null;

        var path = Path.Combine(folder, imageFile.Replace('/', Path.DirectorySeparatorChar));
        return File.Exists(path) && SupportedCharacterImageExtensions.Contains(Path.GetExtension(path))
            ? path
            : null;
    }

    private CharacterStateImageReadiness ResolveCharacterStateImageReadiness(MascotCharacterProfile profile)
    {
        profile.EnsureImageDefaults();
        var folder = ResolveCharacterImageFolder(profile.ImageFolder);
        var missing = new List<string>();
        var available = 0;

        foreach (var requirement in CharacterStateAssetRequirements)
        {
            var stateKey = requirement.State.ToString();
            var stateFile = profile.StateImages.TryGetValue(stateKey, out var mappedFile)
                ? mappedFile
                : string.Empty;
            if (folder is not null &&
                (IsCharacterImageAvailable(folder, stateFile) ||
                 requirement.Aliases.Any(alias => IsCharacterImageAvailable(folder, alias))))
            {
                available++;
                continue;
            }

            missing.Add(requirement.DisplayName);
        }

        return new CharacterStateImageReadiness(
            available,
            CharacterStateAssetRequirements.Count,
            missing.Count == 0 ? "状态图已完整。" : $"缺少：{string.Join("、", missing)}");
    }

    private string? ResolveCharacterImageFolder(string imageFolder)
    {
        if (string.IsNullOrWhiteSpace(imageFolder))
            return null;

        if (Path.IsPathRooted(imageFolder))
            return Directory.Exists(imageFolder) ? imageFolder : null;

        var normalized = imageFolder.Replace('/', Path.DirectorySeparatorChar);
        foreach (var root in _characterAssetRootCandidates ?? EnumerateDefaultCharacterRootCandidates().ToArray())
        {
            if (string.IsNullOrWhiteSpace(root))
                continue;

            var direct = Path.GetFullPath(Path.Combine(root, normalized));
            if (Directory.Exists(direct))
                return direct;

            var underCharacters = Path.GetFullPath(Path.Combine(root, "assets", "characters", normalized));
            if (Directory.Exists(underCharacters))
                return underCharacters;
        }

        return null;
    }

    private static bool IsCharacterImageAvailable(string folder, string imageFile) =>
        ResolveFirstCharacterImagePath(folder, imageFile) is not null;

    private static string NormalizeCharacterFolderKey(string value) =>
        (value ?? string.Empty).Trim().Replace('\\', '/').TrimEnd('/').ToLowerInvariant();

    private static string ExtractCharacterAssetFolderName(string imageFolder)
    {
        var normalized = (imageFolder ?? string.Empty).Replace('\\', '/').TrimEnd('/');
        var index = normalized.LastIndexOf('/');
        return index >= 0 ? normalized[(index + 1)..] : normalized;
    }

    private static string GetCharacterDirectorySortKey(DirectoryInfo directory)
    {
        var name = directory.Name;
        var knownIndex = name.ToLowerInvariant() switch
        {
            "yan" => 0,
            "yue guang" => 1,
            "feng lin yu ren" => 2,
            "developer" => 3,
            "operator" => 4,
            "study" => 5,
            "default" => 6,
            _ => 20
        };
        return $"{knownIndex:D2}-{name}";
    }

    private static string FormatCharacterFolderName(string folderName)
    {
        var text = string.IsNullOrWhiteSpace(folderName)
            ? "角色"
            : folderName.Replace('-', ' ').Replace('_', ' ').Trim();
        return string.Join(" ", text.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => part.Length == 0 ? part : char.ToUpperInvariant(part[0]) + part[1..]));
    }

    private static string FormatAvatarText(string folderName)
    {
        var name = FormatCharacterFolderName(folderName);
        return string.IsNullOrWhiteSpace(name) ? "角" : name[..1];
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

    private sealed record CharacterAssetPreset(string Name, string Role, string AvatarText, string AccentColor, string BackgroundColor);
    private sealed record CharacterStateAssetRequirement(MascotState State, string DisplayName, string[] Aliases);
    private sealed record CharacterStateImageReadiness(int AvailableCount, int TotalCount, string MissingText);
}
