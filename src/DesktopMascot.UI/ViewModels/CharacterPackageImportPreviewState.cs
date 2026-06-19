using DesktopMascot.UI.Services;

namespace DesktopMascot.UI.ViewModels;

public sealed class CharacterPackageImportPreviewState
{
    private static readonly HashSet<string> SupportedImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png",
        ".jpg",
        ".jpeg",
        ".bmp",
        ".webp"
    };

    private CharacterPackageImportPreviewState(
        bool hasPreview,
        bool canImport,
        string manifestPath,
        string summaryText,
        string statusText,
        string missingStateImageText,
        string sourceText,
        string imageFolderText,
        MascotCharacterProfile? profile)
    {
        HasPreview = hasPreview;
        CanImport = canImport;
        ManifestPath = manifestPath;
        SummaryText = summaryText;
        StatusText = statusText;
        MissingStateImageText = missingStateImageText;
        SourceText = sourceText;
        ImageFolderText = imageFolderText;
        Profile = profile;
    }

    public static CharacterPackageImportPreviewState Empty { get; } = new(
        hasPreview: false,
        canImport: false,
        manifestPath: string.Empty,
        summaryText: "尚未选择角色包",
        statusText: "选择 character.manifest.json 后会在这里预览导入结果。",
        missingStateImageText: "未检查",
        sourceText: "来源：未选择",
        imageFolderText: "图片目录：未选择",
        profile: null);

    public bool HasPreview { get; }
    public bool CanImport { get; }
    public string ManifestPath { get; }
    public string SummaryText { get; }
    public string StatusText { get; }
    public string MissingStateImageText { get; }
    public string SourceText { get; }
    public string ImageFolderText { get; }
    public MascotCharacterProfile? Profile { get; }

    public static CharacterPackageImportPreviewState FromManifest(
        MascotCharacterManifest manifest,
        string manifestPath)
    {
        var profile = MascotCharacterManifestFactory.ToProfile(manifest, manifestPath);
        var canImport = string.Equals(
            manifest.Schema,
            MascotCharacterManifestFactory.Schema,
            StringComparison.OrdinalIgnoreCase);

        var availableStates = manifest.States
            .Where(item => IsImageAvailable(profile.ImageFolder, item.Value.Image))
            .Select(item => item.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var missingStateNames = manifest.States
            .Where(item => !availableStates.Contains(item.Key))
            .Select(item => string.IsNullOrWhiteSpace(item.Value.DisplayName) ? item.Key : item.Value.DisplayName.Trim())
            .ToArray();

        var statusText = canImport
            ? "可导入。导入后会复制图片资源，并把角色保存到本机角色库。"
            : $"暂不支持 {manifest.Schema} 作为主导入格式。请先转换为 {MascotCharacterManifestFactory.Schema}。";

        var missingText = missingStateNames.Length == 0
            ? "状态图完整"
            : $"缺少：{string.Join("、", missingStateNames)}";

        return new CharacterPackageImportPreviewState(
            hasPreview: true,
            canImport: canImport,
            manifestPath: manifestPath,
            summaryText: $"{CleanText(manifest.Name, "未命名")} · {CleanText(manifest.Schema, "未知格式")} · {availableStates.Count}/{manifest.States.Count} 状态图可用",
            statusText: statusText,
            missingStateImageText: missingText,
            sourceText: $"来源：{manifestPath}",
            imageFolderText: $"图片目录：{profile.ImageFolder}",
            profile: profile);
    }

    private static bool IsImageAvailable(string folder, string imageFile)
    {
        if (string.IsNullOrWhiteSpace(folder) || string.IsNullOrWhiteSpace(imageFile))
            return false;

        var candidate = Path.IsPathRooted(imageFile)
            ? imageFile
            : Path.Combine(folder, imageFile.Replace('/', Path.DirectorySeparatorChar));

        return File.Exists(candidate) &&
               SupportedImageExtensions.Contains(Path.GetExtension(candidate));
    }

    private static string CleanText(string? value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    }
}
