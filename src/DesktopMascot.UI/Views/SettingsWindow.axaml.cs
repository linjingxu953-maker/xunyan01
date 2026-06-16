using Avalonia.Controls;
using Avalonia.Input;
using DesktopMascot.UI.ViewModels;

namespace DesktopMascot.UI.Views;

public partial class SettingsWindow : Window
{
    public SettingsWindow()
    {
        InitializeComponent();
        Opened += OnOpened;
    }

    private async void OnOpened(object? sender, EventArgs e)
    {
        if (DataContext is SettingsWindowViewModel vm)
        {
            await vm.LoadAsync();
        }
    }

    private void CharacterAssetDropZone_DragOver(object? sender, DragEventArgs e)
    {
        CharacterAssetDropHelper.SetDragEffect(e);
    }

    private void CharacterAssetDropZone_Drop(object? sender, DragEventArgs e)
    {
        if (DataContext is SettingsWindowViewModel vm)
        {
            vm.ApplyDroppedCharacterImageFiles(CharacterAssetDropHelper.GetImageFilePaths(e));
        }

        e.Handled = true;
    }
}
