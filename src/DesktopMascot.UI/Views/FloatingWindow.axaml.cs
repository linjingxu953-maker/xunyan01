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

    /// <summary>
    /// 角色图标点击 - 展开对话框
    /// </summary>
    private void MascotIcon_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            if (DataContext is ViewModels.FloatingWindowViewModel vm)
            {
                vm.ExpandDialogCommand.Execute(null);
            }
        }
    }

    public void FocusInput()
    {
        ChatPanel.FocusInput();
    }
}
