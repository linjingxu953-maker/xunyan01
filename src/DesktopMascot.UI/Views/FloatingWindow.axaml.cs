using Avalonia.Controls;
using Avalonia.Input;

namespace DesktopMascot.UI.Views;

public partial class FloatingWindow : Window
{
    public FloatingWindow()
    {
        InitializeComponent();
    }

    /// <summary>
    /// 标题栏拖动
    /// </summary>
    private void TitleBar_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            BeginMoveDrag(e);
        }
    }

    public void FocusInput()
    {
        ChatPanel.FocusInput();
    }
}
