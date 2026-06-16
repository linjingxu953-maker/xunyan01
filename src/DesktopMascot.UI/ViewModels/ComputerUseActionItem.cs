using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;

namespace DesktopMascot.UI.ViewModels;

public sealed partial class ComputerUseActionItem : ObservableObject
{
    public ComputerUseActionItem(string actionName, string target, string statusText, string detail, DateTime createdAt)
    {
        ActionName = actionName;
        Target = target;
        StatusText = statusText;
        Detail = detail;
        TimeText = createdAt.ToLocalTime().ToString("HH:mm:ss");
    }

    public string ActionName { get; }
    public string Target { get; }
    public string StatusText { get; }
    public string Detail { get; }
    public string TimeText { get; }

    [ObservableProperty]
    private bool _isCurrent;

    public IBrush StatusBrush => StatusText switch
    {
        var text when ContainsAny(text, "失败", "拒绝", "停止", "异常") => BrushFrom("#3A1117"),
        var text when ContainsAny(text, "待确认", "接管", "暂停") => BrushFrom("#3B2A0B"),
        var text when ContainsAny(text, "完成", "已完成", "成功") => BrushFrom("#0B2F24"),
        _ => BrushFrom("#102A43")
    };

    public IBrush StatusForeground => StatusText switch
    {
        var text when ContainsAny(text, "失败", "拒绝", "停止", "异常") => BrushFrom("#FCA5A5"),
        var text when ContainsAny(text, "待确认", "接管", "暂停") => BrushFrom("#FCD34D"),
        var text when ContainsAny(text, "完成", "已完成", "成功") => BrushFrom("#6EE7B7"),
        _ => BrushFrom("#93C5FD")
    };

    private static bool ContainsAny(string value, params string[] tokens) =>
        tokens.Any(token => value.Contains(token, StringComparison.OrdinalIgnoreCase));

    private static IBrush BrushFrom(string hex) => new SolidColorBrush(Color.Parse(hex));
}
