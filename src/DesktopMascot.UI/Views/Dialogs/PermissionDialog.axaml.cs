using Avalonia.Controls;
using DesktopMascot.Core.Enums;
using DesktopMascot.UI.ViewModels;

namespace DesktopMascot.UI.Views.Dialogs;

public partial class PermissionDialog : Window
{
    private PermissionDialogViewModel? _viewModel;

    public PermissionDialog()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_viewModel is not null)
        {
            _viewModel.CloseRequested -= OnCloseRequested;
        }

        _viewModel = DataContext as PermissionDialogViewModel;

        if (_viewModel is not null)
        {
            _viewModel.CloseRequested += OnCloseRequested;
        }
    }

    private void OnCloseRequested(object? sender, PermissionDecision decision)
    {
        Close(decision);
    }
}
