using Avalonia.Controls;
using Avalonia.Input;
using DesktopMascot.UI.ViewModels;

namespace DesktopMascot.UI.Views.Controls;

public partial class ChatPanel : UserControl
{
    public ChatPanel()
    {
        InitializeComponent();
    }

    public void FocusInput()
    {
        InputBox.Focus();
    }

    private void Header_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;

        if (TopLevel.GetTopLevel(this) is Window window)
        {
            window.BeginMoveDrag(e);
        }
    }

    private void InputBox_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || e.KeyModifiers.HasFlag(KeyModifiers.Shift))
            return;

        if (DataContext is FloatingWindowViewModel vm)
        {
            vm.SendMessageCommand.Execute(null);
        }

        e.Handled = true;
    }
}
