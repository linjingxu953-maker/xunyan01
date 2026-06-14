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

    /// <summary>
    /// 历史项点击
    /// </summary>
    private void HistoryItem_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Border border && border.DataContext is ViewModels.TaskHistoryItem item)
        {
            if (DataContext is ViewModels.FloatingWindowViewModel vm)
            {
                // 加载历史对话
                vm.MessageItems.Clear();
                foreach (var msg in item.Messages)
                {
                    vm.MessageItems.Add(msg);
                }
            }
        }
    }

    /// <summary>
    /// 输入框按键处理
    /// </summary>
    private void InputBox_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || e.KeyModifiers.HasFlag(KeyModifiers.Shift))
            return;

        if (DataContext is ViewModels.FloatingWindowViewModel vm)
        {
            vm.SendMessageCommand.Execute(null);
        }

        e.Handled = true;
    }

    public void FocusInput()
    {
        // Focus will be handled by the ChatPanel when it becomes visible
    }
}
