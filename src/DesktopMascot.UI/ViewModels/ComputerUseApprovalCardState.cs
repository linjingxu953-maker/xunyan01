using Avalonia.Media;

namespace DesktopMascot.UI.ViewModels;

public sealed class ComputerUseApprovalCardState
{
    private ComputerUseApprovalCardState(string title, string description, string riskText)
    {
        Title = title;
        Description = description;
        RiskText = riskText;
    }

    public string Title { get; }
    public string Description { get; }
    public string RiskText { get; }

    public IBrush RiskBrush => RiskText switch
    {
        var text when ContainsAny(text, "高风险", "危险", "删除", "格式化", "命令执行") => BrushFrom("#F87171"),
        var text when ContainsAny(text, "文件", "写入", "中风险", "权限", "执行") => BrushFrom("#FBBF24"),
        _ => BrushFrom("#60A5FA")
    };

    public static ComputerUseApprovalCardState From(string? title, string? description, string? riskText) =>
        new(
            Clean(title, "等待用户确认"),
            Clean(description, "请确认后继续执行。"),
            Clean(riskText, "待确认"));

    private static string Clean(string? value, string fallback)
    {
        var text = value?.Trim();
        return string.IsNullOrWhiteSpace(text) ? fallback : text;
    }

    private static bool ContainsAny(string value, params string[] tokens) =>
        tokens.Any(token => value.Contains(token, StringComparison.OrdinalIgnoreCase));

    private static IBrush BrushFrom(string hex) => new SolidColorBrush(Color.Parse(hex));
}
