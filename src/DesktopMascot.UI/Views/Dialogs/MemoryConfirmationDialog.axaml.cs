using Avalonia.Controls;
using DesktopMascot.Core.Memory;
using DesktopMascot.UI.ViewModels;

namespace DesktopMascot.UI.Views.Dialogs;

public partial class MemoryConfirmationDialog : Window
{
    private MemoryConfirmationDialogViewModel? _viewModel;

    public MemoryConfirmationDialog()
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

        _viewModel = DataContext as MemoryConfirmationDialogViewModel;

        if (_viewModel is not null)
        {
            _viewModel.CloseRequested += OnCloseRequested;
        }
    }

    private void OnCloseRequested(object? sender, MemoryDecision decision)
    {
        Close(decision);
    }
}
