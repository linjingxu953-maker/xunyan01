using Avalonia.Input;
using DesktopMascot.UI.ViewModels;

namespace DesktopMascot.UI.Views;

internal static class CharacterAssetDropHelper
{
    public static IReadOnlyList<string> GetImageFilePaths(DragEventArgs e)
    {
        var files = e.DataTransfer.TryGetFiles();
        if (files is null)
            return [];

        return files
            .Select(GetLocalPath)
            .Where(SettingsWindowViewModel.IsSupportedCharacterImageFile)
            .Select(path => path!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static void SetDragEffect(DragEventArgs e)
    {
        e.DragEffects = GetImageFilePaths(e).Count > 0
            ? DragDropEffects.Copy
            : DragDropEffects.None;
        e.Handled = true;
    }

    private static string? GetLocalPath(object storageItem)
    {
        var type = storageItem.GetType();
        var tryGetLocalPath = type.GetMethod("TryGetLocalPath", Type.EmptyTypes);
        if (tryGetLocalPath?.Invoke(storageItem, null) is string localPath &&
            !string.IsNullOrWhiteSpace(localPath))
        {
            return localPath;
        }

        if (type.GetProperty("Path")?.GetValue(storageItem) is Uri uri && uri.IsFile)
            return uri.LocalPath;

        return null;
    }
}
