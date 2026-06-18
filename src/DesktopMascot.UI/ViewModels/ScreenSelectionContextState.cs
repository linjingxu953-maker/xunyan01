using DesktopMascot.UI.Services;

namespace DesktopMascot.UI.ViewModels;

public sealed class ScreenSelectionContextState
{
    public static ScreenSelectionContextState Empty { get; } = new(
        hasRegion: false,
        title: "屏幕圈选",
        regionText: "暂无圈选区域",
        sizeText: "等待 Ctrl+Shift+S 或点击圈选",
        statusText: "待命",
        detailText: "选择屏幕区域后会显示坐标和尺寸。");

    private ScreenSelectionContextState(
        bool hasRegion,
        string title,
        string regionText,
        string sizeText,
        string statusText,
        string detailText)
    {
        HasRegion = hasRegion;
        Title = title;
        RegionText = regionText;
        SizeText = sizeText;
        StatusText = statusText;
        DetailText = detailText;
    }

    public bool HasRegion { get; }
    public string Title { get; }
    public string RegionText { get; }
    public string SizeText { get; }
    public string StatusText { get; }
    public string DetailText { get; }

    public static ScreenSelectionContextState From(ScreenSelectionResult? result, string? statusText = null)
    {
        if (result is not { HasRegion: true })
            return Empty;

        var sizeText = $"{result.Width} x {result.Height}";
        return new ScreenSelectionContextState(
            hasRegion: true,
            title: "屏幕圈选区域",
            regionText: $"屏幕坐标 {result.X}, {result.Y}",
            sizeText: sizeText,
            statusText: Clean(statusText, "等待视觉理解"),
            detailText: $"将把该屏幕区域交给视觉理解：({result.X}, {result.Y}) {result.Width}x{result.Height}");
    }

    private static string Clean(string? value, string fallback)
    {
        var text = value?.Trim();
        return string.IsNullOrWhiteSpace(text) ? fallback : text;
    }
}
