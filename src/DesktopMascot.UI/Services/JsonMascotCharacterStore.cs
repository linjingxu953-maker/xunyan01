using System.Text.Json;

namespace DesktopMascot.UI.Services;

public sealed class JsonMascotCharacterStore : IMascotCharacterStore
{
    private readonly string _filePath;
    private readonly string _profilesDirectory;
    private static readonly HashSet<string> SupportedImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png",
        ".jpg",
        ".jpeg",
        ".bmp",
        ".webp"
    };

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true
    };

    public JsonMascotCharacterStore()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        if (string.IsNullOrWhiteSpace(appData))
        {
            appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        }

        _filePath = Path.Combine(appData, "DesktopAIMascot", "config", "character-profile.json");
        _profilesDirectory = Path.Combine(appData, "DesktopAIMascot", "config", "character-profiles");
    }

    public event EventHandler? ProfileChanged;

    public MascotCharacterProfile Load()
    {
        try
        {
            if (!File.Exists(_filePath))
                return new MascotCharacterProfile();

            var json = File.ReadAllText(_filePath);
            var profile = JsonSerializer.Deserialize<MascotCharacterProfile>(json, SerializerOptions)
                ?? new MascotCharacterProfile();
            ApplyCurrentBrandDefaults(profile);
            profile.EnsureImageDefaults();
            return profile;
        }
        catch
        {
            var profile = new MascotCharacterProfile();
            profile.EnsureImageDefaults();
            return profile;
        }
    }

    public void Save(MascotCharacterProfile profile)
    {
        try
        {
            profile.EnsureImageDefaults();
            ApplyCurrentBrandDefaults(profile);
            var directory = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(profile, SerializerOptions);
            File.WriteAllText(_filePath, json);
            ProfileChanged?.Invoke(this, EventArgs.Empty);
        }
        catch
        {
            // Character customization should never block app startup or shutdown.
        }
    }

    public IReadOnlyList<MascotCharacterProfileEntry> ListProfiles()
    {
        try
        {
            if (!Directory.Exists(_profilesDirectory))
                return [];

            var activeId = CreateProfileId(Load().Name);
            return Directory
                .EnumerateFiles(_profilesDirectory, "*.json")
                .Select(filePath => ReadProfileEntry(filePath, activeId))
                .OfType<MascotCharacterProfileEntry>()
                .OrderByDescending(x => x.UpdatedAt)
                .ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
        catch
        {
            return [];
        }
    }

    public MascotCharacterProfile? LoadProfile(string id)
    {
        try
        {
            var filePath = GetProfileFilePath(id);
            if (filePath is null || !File.Exists(filePath))
                return null;

            var json = File.ReadAllText(filePath);
            var profile = JsonSerializer.Deserialize<MascotCharacterProfile>(json, SerializerOptions);
            if (profile is not null)
            {
                ApplyCurrentBrandDefaults(profile);
            }
            profile?.EnsureImageDefaults();
            return profile;
        }
        catch
        {
            return null;
        }
    }

    public MascotCharacterProfileEntry SaveProfile(MascotCharacterProfile profile)
    {
        profile.EnsureImageDefaults();
        Directory.CreateDirectory(_profilesDirectory);

        var id = CreateProfileId(profile.Name);
        var filePath = Path.Combine(_profilesDirectory, $"{id}.json");
        var json = JsonSerializer.Serialize(profile, SerializerOptions);
        File.WriteAllText(filePath, json);

        return CreateEntry(id, profile, File.GetLastWriteTimeUtc(filePath), CreateProfileId(Load().Name));
    }

    public MascotCharacterProfileEntry SaveProfileAs(MascotCharacterProfile profile, string name)
    {
        var clone = profile.Clone();
        if (!string.IsNullOrWhiteSpace(name))
        {
            clone.Name = name.Trim();
        }

        return SaveProfile(clone);
    }

    public bool DeleteProfile(string id)
    {
        try
        {
            var filePath = GetProfileFilePath(id);
            if (filePath is null || !File.Exists(filePath))
                return false;

            File.Delete(filePath);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private MascotCharacterProfileEntry? ReadProfileEntry(string filePath, string activeId)
    {
        try
        {
            var json = File.ReadAllText(filePath);
            var profile = JsonSerializer.Deserialize<MascotCharacterProfile>(json, SerializerOptions);
            if (profile is null)
                return null;

            ApplyCurrentBrandDefaults(profile);
            profile.EnsureImageDefaults();
            var id = Path.GetFileNameWithoutExtension(filePath);
            return CreateEntry(id, profile, File.GetLastWriteTimeUtc(filePath), activeId);
        }
        catch
        {
            return null;
        }
    }

    private static MascotCharacterProfileEntry CreateEntry(
        string id,
        MascotCharacterProfile profile,
        DateTime updatedAt,
        string? activeId = null) => new()
        {
            Id = id,
            Name = profile.Name,
            Role = profile.Role,
            ImageFolder = profile.ImageFolder,
            AvatarImagePath = ResolveAvatarImagePath(profile),
            AccentColor = profile.AccentColor,
            IsActive = string.Equals(id, activeId, StringComparison.OrdinalIgnoreCase),
            UpdatedAt = updatedAt
        };

    private static string ResolveAvatarImagePath(MascotCharacterProfile profile)
    {
        var folder = ResolveCharacterFolder(profile.ImageFolder);
        if (folder is null)
            return string.Empty;

        var avatarImage = string.IsNullOrWhiteSpace(profile.AvatarImage)
            ? "avatar.png"
            : profile.AvatarImage;
        var candidate = Path.IsPathRooted(avatarImage)
            ? avatarImage
            : Path.Combine(folder, avatarImage.Replace('/', Path.DirectorySeparatorChar));

        return File.Exists(candidate) &&
               SupportedImageExtensions.Contains(Path.GetExtension(candidate))
            ? candidate
            : string.Empty;
    }

    private static string? ResolveCharacterFolder(string folder)
    {
        if (string.IsNullOrWhiteSpace(folder))
            return null;

        if (Path.IsPathRooted(folder))
        {
            return Directory.Exists(folder) ? folder : null;
        }

        var normalizedFolder = folder.Replace('/', Path.DirectorySeparatorChar);
        foreach (var root in EnumerateCharacterRootCandidates())
        {
            var direct = Path.GetFullPath(Path.Combine(root, normalizedFolder));
            if (Directory.Exists(direct))
                return direct;

            var underCharacters = Path.GetFullPath(Path.Combine(root, "assets", "characters", normalizedFolder));
            if (Directory.Exists(underCharacters))
                return underCharacters;
        }

        return null;
    }

    private static void ApplyCurrentBrandDefaults(MascotCharacterProfile profile)
    {
        if (string.Equals(profile.Name, "小桌灵", StringComparison.Ordinal))
            profile.Name = "妍";

        if (string.Equals(profile.Role, "桌面工作助手", StringComparison.Ordinal))
            profile.Role = "寻研桌面助手";

        if (string.Equals(profile.AvatarText, "灵", StringComparison.Ordinal))
            profile.AvatarText = "妍";
    }

    private static IEnumerable<string> EnumerateCharacterRootCandidates()
    {
        yield return Environment.CurrentDirectory;

        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            yield return directory.FullName;
            directory = directory.Parent;
        }
    }

    private string? GetProfileFilePath(string id)
    {
        var safeId = CreateProfileId(id);
        if (string.IsNullOrWhiteSpace(safeId))
            return null;

        return Path.Combine(_profilesDirectory, $"{safeId}.json");
    }

    private static string CreateProfileId(string? value)
    {
        var text = string.IsNullOrWhiteSpace(value) ? "custom-character" : value.Trim();
        var invalidChars = Path.GetInvalidFileNameChars();
        var chars = text
            .Select(c => char.IsLetterOrDigit(c) || c is '-' or '_' ? c : '-')
            .Where(c => !invalidChars.Contains(c))
            .ToArray();
        var id = new string(chars).Trim('-');

        if (string.IsNullOrWhiteSpace(id))
            id = "custom-character";

        return id.Length <= 48 ? id : id[..48];
    }
}
