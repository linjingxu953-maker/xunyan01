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
}
