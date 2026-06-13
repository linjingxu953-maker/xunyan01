using Avalonia.Controls;
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
}
