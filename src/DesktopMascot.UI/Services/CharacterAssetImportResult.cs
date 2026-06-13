namespace DesktopMascot.UI.Services;

public sealed class CharacterAssetImportResult
{
    public bool Success { get; init; }
    public string DestinationFolder { get; init; } = string.Empty;
    public int CopiedCount { get; init; }
    public int FallbackCount { get; init; }
    public string Message { get; init; } = string.Empty;
    public MascotCharacterProfile? Profile { get; init; }
    public IReadOnlyList<string> MissingItems { get; init; } = [];
}
