using Avalonia.Controls;
using DesktopMascot.UI.ViewModels;

namespace DesktopMascot.UI.Views.Controls;

public partial class TaskDetailPanel : UserControl
{
    public TaskDetailPanel()
    {
        InitializeComponent();
    }

    private void UseScreenSuggestedAction_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is Button { Tag: ScreenContextActionItem action } && DataContext is FloatingWindowViewModel viewModel)
        {
            viewModel.UseScreenSuggestedAction(action);
        }

        e.Handled = true;
    }

    private void UseScreenScreenshotEvidence_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is FloatingWindowViewModel viewModel)
        {
            viewModel.UseScreenScreenshotEvidence();
        }

        e.Handled = true;
    }

    private void ToggleScreenScreenshotPreview_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is FloatingWindowViewModel viewModel)
        {
            viewModel.ToggleScreenScreenshotPreview();
        }

        e.Handled = true;
    }

    private async void CopyScreenScreenshotPath_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is FloatingWindowViewModel viewModel)
        {
            await viewModel.CopyScreenScreenshotPathAsync();
        }

        e.Handled = true;
    }
}
