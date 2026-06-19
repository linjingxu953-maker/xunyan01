namespace DesktopMascot.UI.Services;

public sealed class MascotCharacterPackageExportResult
{
    public bool Success { get; init; }
    public string PackageDirectory { get; init; } = string.Empty;
    public string ManifestPath { get; init; } = string.Empty;
    public int CopiedImageCount { get; init; }
    public IReadOnlyList<string> MissingItems { get; init; } = [];
    public MascotCharacterManifest Manifest { get; init; } = new();
    public string Message { get; init; } = string.Empty;
}
