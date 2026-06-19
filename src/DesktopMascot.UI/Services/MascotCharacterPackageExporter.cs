using System.Text;

namespace DesktopMascot.UI.Services;

public static class MascotCharacterPackageExporter
{
    private const string AssetFolderName = "assets";

    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png",
        ".jpg",
        ".jpeg",
        ".bmp",
        ".webp"
    };

    public static async Task<MascotCharacterPackageExportResult> ExportAsync(
        MascotCharacterManifest manifest,
        string exportRoot,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var packageDirectory = Path.Combine(exportRoot, CleanPathSegment(manifest.Slug, "character"));
        var assetDirectory = Path.Combine(packageDirectory, AssetFolderName);
        Directory.CreateDirectory(assetDirectory);

        var copiedImages = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var missingItems = new List<string>();
        var sourceFolder = ResolveFolder(manifest.Appearance.ImageFolder);

        var exportedAvatarImage = CopyImage(
            sourceFolder,
            manifest.Appearance.AvatarImage,
            assetDirectory,
            copiedImages);

        if (exportedAvatarImage is null)
        {
            missingItems.Add("头像");
            exportedAvatarImage = manifest.Appearance.AvatarImage;
        }

        var exportedStates = new Dictionary<string, MascotCharacterStateManifest>(StringComparer.OrdinalIgnoreCase);
        foreach (var state in manifest.States)
        {
            ct.ThrowIfCancellationRequested();

            var exportedImage = CopyImage(sourceFolder, state.Value.Image, assetDirectory, copiedImages);
            if (exportedImage is null)
            {
                missingItems.Add(string.IsNullOrWhiteSpace(state.Value.DisplayName)
                    ? state.Key
                    : state.Value.DisplayName);
                exportedImage = state.Value.Image;
            }

            exportedStates[state.Key] = new MascotCharacterStateManifest
            {
                DisplayName = state.Value.DisplayName,
                Image = exportedImage,
                FallbackState = state.Value.FallbackState,
                PetdexState = state.Value.PetdexState
            };
        }

        var exportedManifest = CloneForPackage(manifest, exportedAvatarImage, exportedStates);
        var manifestPath = Path.Combine(packageDirectory, "character.manifest.json");
        var json = MascotCharacterManifestFactory.ToJson(exportedManifest);
        await File.WriteAllTextAsync(manifestPath, json, ct);

        var missingText = missingItems.Count > 0
            ? $"，缺少 {missingItems.Count} 个资源"
            : string.Empty;

        return new MascotCharacterPackageExportResult
        {
            Success = true,
            PackageDirectory = packageDirectory,
            ManifestPath = manifestPath,
            CopiedImageCount = copiedImages.Count,
            MissingItems = missingItems,
            Manifest = exportedManifest,
            Message = $"已导出角色包：{manifestPath}，复制 {copiedImages.Count} 个图片资源{missingText}。"
        };
    }

    private static MascotCharacterManifest CloneForPackage(
        MascotCharacterManifest manifest,
        string avatarImage,
        Dictionary<string, MascotCharacterStateManifest> states) => new()
        {
            Schema = manifest.Schema,
            Version = manifest.Version,
            Slug = manifest.Slug,
            Name = manifest.Name,
            Kind = manifest.Kind,
            Persona = manifest.Persona,
            Appearance = new MascotCharacterAppearanceManifest
            {
                AvatarText = manifest.Appearance.AvatarText,
                ImageFolder = AssetFolderName,
                AvatarImage = avatarImage,
                AccentColor = manifest.Appearance.AccentColor,
                BackgroundColor = manifest.Appearance.BackgroundColor
            },
            States = states,
            Animation = manifest.Animation,
            Metadata = manifest.Metadata
        };

    private static string? CopyImage(
        string? sourceFolder,
        string? imageFile,
        string assetDirectory,
        Dictionary<string, string> copiedImages)
    {
        var sourcePath = ResolveImagePath(sourceFolder, imageFile);
        if (sourcePath is null)
            return null;

        var sourceKey = Path.GetFullPath(sourcePath);
        if (copiedImages.TryGetValue(sourceKey, out var existingFileName))
            return existingFileName;

        var targetFileName = CreateUniqueFileName(
            assetDirectory,
            Path.GetFileName(sourcePath),
            copiedImages.Values);
        var targetPath = Path.Combine(assetDirectory, targetFileName);
        File.Copy(sourcePath, targetPath, overwrite: true);
        copiedImages[sourceKey] = targetFileName;
        return targetFileName;
    }

    private static string? ResolveFolder(string folder)
    {
        if (string.IsNullOrWhiteSpace(folder))
            return null;

        if (Path.IsPathRooted(folder))
            return Directory.Exists(folder) ? folder : null;

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

    private static string? ResolveImagePath(string? sourceFolder, string? imageFile)
    {
        if (string.IsNullOrWhiteSpace(sourceFolder) || string.IsNullOrWhiteSpace(imageFile))
            return null;

        var candidate = Path.IsPathRooted(imageFile)
            ? imageFile
            : Path.Combine(sourceFolder, imageFile.Replace('/', Path.DirectorySeparatorChar));

        if (!File.Exists(candidate))
            return null;

        return SupportedExtensions.Contains(Path.GetExtension(candidate))
            ? candidate
            : null;
    }

    private static string CreateUniqueFileName(
        string assetDirectory,
        string fileName,
        IEnumerable<string> reservedFileNames)
    {
        var safeFileName = CleanFileName(fileName, "image.png");
        var extension = Path.GetExtension(safeFileName);
        var baseName = Path.GetFileNameWithoutExtension(safeFileName);
        var reserved = reservedFileNames.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var candidate = safeFileName;
        var index = 2;

        while (reserved.Contains(candidate) || File.Exists(Path.Combine(assetDirectory, candidate)))
        {
            candidate = $"{baseName}-{index}{extension}";
            index++;
        }

        return candidate;
    }

    private static string CleanFileName(string? value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
            return fallback;

        var fileName = Path.GetFileName(value.Trim());
        var invalidChars = Path.GetInvalidFileNameChars();
        var builder = new StringBuilder();
        foreach (var character in fileName)
        {
            builder.Append(invalidChars.Contains(character) ? '-' : character);
        }

        var safe = builder.ToString().Trim('-', '.', ' ');
        return string.IsNullOrWhiteSpace(safe) ? fallback : safe;
    }

    private static string CleanPathSegment(string? value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
            return fallback;

        var invalidChars = Path.GetInvalidFileNameChars();
        var builder = new StringBuilder();
        foreach (var character in value.Trim())
        {
            if (char.IsLetterOrDigit(character) || character is '-' or '_')
            {
                builder.Append(character);
            }
            else if (!invalidChars.Contains(character))
            {
                builder.Append('-');
            }
        }

        var segment = builder.ToString().Trim('-');
        return string.IsNullOrWhiteSpace(segment) ? fallback : segment;
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
