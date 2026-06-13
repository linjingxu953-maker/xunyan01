using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace DesktopMascot.UI.Services;

public sealed class CharacterAssetPickerService : ICharacterAssetPickerService
{
    private static readonly FilePickerFileType ImageFileType = new("角色图片")
    {
        Patterns = ["*.png", "*.jpg", "*.jpeg", "*.bmp", "*.webp"],
        MimeTypes = ["image/png", "image/jpeg", "image/bmp", "image/webp"]
    };

    private readonly Func<Window?> _ownerFactory;

    public CharacterAssetPickerService(Func<Window?> ownerFactory)
    {
        _ownerFactory = ownerFactory;
    }

    public async Task<string?> PickImageFolderAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var owner = _ownerFactory();
        if (owner is null)
            return null;

        var folders = await owner.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "选择角色图片目录",
            AllowMultiple = false
        });

        ct.ThrowIfCancellationRequested();
        return folders.Count > 0 ? GetLocalPath(folders[0]) : null;
    }

    public async Task<string?> PickImageFileAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var owner = _ownerFactory();
        if (owner is null)
            return null;

        var files = await owner.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "选择角色图片",
            AllowMultiple = false,
            FileTypeFilter = [ImageFileType]
        });

        ct.ThrowIfCancellationRequested();
        return files.Count > 0 ? GetLocalPath(files[0]) : null;
    }

    private static string? GetLocalPath(IStorageItem item)
    {
        return item.Path.IsFile ? item.Path.LocalPath : item.Path.LocalPath;
    }
}
