using Avalonia.Media;

namespace DesktopMascot.UI.ViewModels;

public sealed class ComputerUseLogItem
{
    public ComputerUseLogItem(string title, string detail, string statusText, DateTime createdAt)
    {
        Title = title;
        Detail = detail;
        StatusText = statusText;
        TimeText = createdAt.ToLocalTime().ToString("HH:mm:ss");
    }

    public string Title { get; }
    public string Detail { get; }
    public string StatusText { get; }
    public string TimeText { get; }

    public IBrush DotBrush => StatusText switch
    {
        var text when ContainsAny(text, "失败", "拒绝", "停止", "异常") => BrushFrom("#F87171"),
        var text when ContainsAny(text, "待确认", "接管", "暂停") => BrushFrom("#FBBF24"),
        var text when ContainsAny(text, "完成", "已完成", "成功", "已授权") => BrushFrom("#34D399"),
        _ => BrushFrom("#60A5FA")
    };

    private static bool ContainsAny(string value, params string[] tokens) =>
        tokens.Any(token => value.Contains(token, StringComparison.OrdinalIgnoreCase));

    private static IBrush BrushFrom(string hex) => new SolidColorBrush(Color.Parse(hex));
}
