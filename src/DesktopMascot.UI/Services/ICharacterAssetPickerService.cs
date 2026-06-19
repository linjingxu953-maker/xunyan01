namespace DesktopMascot.UI.Services;

public interface ICharacterAssetPickerService
{
    Task<string?> PickImageFolderAsync(CancellationToken ct = default);
    Task<string?> PickImageFileAsync(CancellationToken ct = default);
    Task<string?> PickMemoryImportFileAsync(CancellationToken ct = default);
    Task<string?> PickCharacterManifestFileAsync(CancellationToken ct = default);
}
