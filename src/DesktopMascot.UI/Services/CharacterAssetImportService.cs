using System.Text;

namespace DesktopMascot.UI.Services;

public sealed class CharacterAssetImportService : ICharacterAssetImportService
{
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png",
        ".jpg",
        ".jpeg",
        ".bmp",
        ".webp"
    };

    private static readonly IReadOnlyDictionary<string, string> ImportedStateFileNames =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Idle"] = "idle",
            ["Listening"] = "listening",
            ["Understanding"] = "understanding",
            ["ReadingContext"] = "reading",
            ["Planning"] = "planning",
            ["WaitingApproval"] = "waiting",
            ["Working"] = "working",
            ["MemoryConfirm"] = "memory",
            ["Reporting"] = "reporting",
            ["Completed"] = "completed",
            ["Error"] = "error"
        };

    public CharacterAssetImportResult ImportToAppData(MascotCharacterProfile profile)
    {
        var importedProfile = profile.Clone();
        importedProfile.EnsureImageDefaults();

        var sourceFolder = ResolveFolder(importedProfile.ImageFolder);
        if (sourceFolder is null)
        {
            return new CharacterAssetImportResult
            {
                Message = "当前图片目录不可用，无法导入角色资源。"
            };
        }

        var destinationFolder = GetDestinationFolder(importedProfile.Name);
        Directory.CreateDirectory(destinationFolder);

        var missingItems = new List<string>();
        var copiedCount = 0;
        var fallbackCount = 0;

        var avatarFile = CopyImage(
            sourceFolder,
            importedProfile.AvatarImage,
            destinationFolder,
            "avatar");

        if (avatarFile is null)
        {
            missingItems.Add("头像");
        }
        else
        {
            copiedCount++;
            importedProfile.AvatarImage = avatarFile;
        }

        foreach (var item in importedProfile.StateImages.ToArray())
        {
            var targetBaseName = ImportedStateFileNames.TryGetValue(item.Key, out var stateFileName)
                ? stateFileName
                : ToSafeFileName(item.Key, "state");

            var stateFile = CopyImage(sourceFolder, item.Value, destinationFolder, targetBaseName);
            if (stateFile is not null)
            {
                copiedCount++;
                importedProfile.StateImages[item.Key] = stateFile;
                continue;
            }

            missingItems.Add(item.Key);
            if (!string.IsNullOrWhiteSpace(avatarFile))
            {
                fallbackCount++;
                importedProfile.StateImages[item.Key] = avatarFile;
            }
        }

        if (copiedCount == 0)
        {
            return new CharacterAssetImportResult
            {
                DestinationFolder = destinationFolder,
                MissingItems = missingItems,
                Message = "没有找到可导入的角色图片。"
            };
        }

        importedProfile.ImageFolder = destinationFolder;

        var fallbackText = fallbackCount > 0
            ? $"，{fallbackCount} 个状态使用头像回退"
            : string.Empty;

        return new CharacterAssetImportResult
        {
            Success = true,
            DestinationFolder = destinationFolder,
            CopiedCount = copiedCount,
            FallbackCount = fallbackCount,
            MissingItems = missingItems,
            Profile = importedProfile,
            Message = $"已导入 {copiedCount} 个角色图片到应用资源目录{fallbackText}。"
        };
    }

    private static string? CopyImage(string sourceFolder, string? imageFile, string destinationFolder, string targetBaseName)
    {
        var sourcePath = ResolveImagePath(sourceFolder, imageFile);
        if (sourcePath is null)
            return null;

        var extension = Path.GetExtension(sourcePath).ToLowerInvariant();
        var destinationFileName = $"{targetBaseName}{extension}";
        var destinationPath = Path.Combine(destinationFolder, destinationFileName);

        if (!IsSamePath(sourcePath, destinationPath))
        {
            File.Copy(sourcePath, destinationPath, overwrite: true);
        }

        return destinationFileName;
    }

    private static string? ResolveFolder(string folder)
    {
        if (string.IsNullOrWhiteSpace(folder))
            return null;

        if (Path.IsPathRooted(folder))
        {
            return Directory.Exists(folder) ? folder : null;
        }

        foreach (var root in EnumerateCandidateRoots())
        {
            var normalizedFolder = folder.Replace('/', Path.DirectorySeparatorChar);
            var direct = Path.GetFullPath(Path.Combine(root, normalizedFolder));
            if (Directory.Exists(direct))
                return direct;

            var underCharacters = Path.GetFullPath(Path.Combine(root, "assets", "characters", normalizedFolder));
            if (Directory.Exists(underCharacters))
                return underCharacters;
        }

        return null;
    }

    private static string? ResolveImagePath(string folder, string? imageFile)
    {
        if (string.IsNullOrWhiteSpace(imageFile))
            return null;

        var candidate = Path.IsPathRooted(imageFile)
            ? imageFile
            : Path.Combine(folder, imageFile.Replace('/', Path.DirectorySeparatorChar));

        if (!File.Exists(candidate))
            return null;

        return SupportedExtensions.Contains(Path.GetExtension(candidate))
            ? candidate
            : null;
    }

    private static string GetDestinationFolder(string characterName)
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        if (string.IsNullOrWhiteSpace(appData))
        {
            appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        }

        if (string.IsNullOrWhiteSpace(appData))
        {
            appData = Environment.CurrentDirectory;
        }

        return Path.Combine(
            appData,
            "DesktopAIMascot",
            "assets",
            "characters",
            ToSafeFileName(characterName, "custom-character"));
    }

    private static string ToSafeFileName(string? value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
            return fallback;

        var invalidChars = Path.GetInvalidFileNameChars();
        var builder = new StringBuilder();
        foreach (var c in value.Trim())
        {
            if (char.IsLetterOrDigit(c) || c is '-' or '_')
            {
                builder.Append(c);
            }
            else if (!invalidChars.Contains(c))
            {
                builder.Append('-');
            }
        }

        var fileName = builder.ToString().Trim('-');
        if (string.IsNullOrWhiteSpace(fileName))
            return fallback;

        return fileName.Length <= 40 ? fileName : fileName[..40];
    }

    private static bool IsSamePath(string left, string right)
    {
        try
        {
            return string.Equals(
                Path.GetFullPath(left),
                Path.GetFullPath(right),
                StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static IEnumerable<string> EnumerateCandidateRoots()
    {
        yield return Environment.CurrentDirectory;

        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            yield return directory.FullName;
            directory = directory.Parent;
        }
    }
}
