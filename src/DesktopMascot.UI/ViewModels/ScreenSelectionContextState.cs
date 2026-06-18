using DesktopMascot.UI.Services;
using System.Text.Json;

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

    public ScreenSelectionContextState WithStatus(string statusText, string? detailText = null)
    {
        if (!HasRegion)
            return this;

        return new ScreenSelectionContextState(
            hasRegion: true,
            title: Title,
            regionText: RegionText,
            sizeText: SizeText,
            statusText: Clean(statusText, StatusText),
            detailText: Clean(detailText, DetailText));
    }

    public ScreenSelectionContextState WithResult(bool success, string? content, string? error)
    {
        if (!HasRegion)
            return this;

        if (!success)
            return WithStatus("识别失败", Clean(error, Clean(content, "屏幕理解失败，可以重试。")));

        return WithStatus("识别完成", BuildReadableResultSummary(content));
    }

    private static string BuildReadableResultSummary(string? content)
    {
        var text = Clean(content, "屏幕区域识别完成。");

        try
        {
            using var document = JsonDocument.Parse(text);
            var root = document.RootElement;
            var identification = GetJsonString(root, "identification");
            var understanding = GetJsonString(root, "understanding");
            var extractedText = GetJsonString(root, "extractedText");

            if (!string.IsNullOrWhiteSpace(identification) && !string.IsNullOrWhiteSpace(understanding))
                return TrimDetail($"识别：{identification}；理解：{understanding}");
            if (!string.IsNullOrWhiteSpace(identification))
                return TrimDetail($"识别：{identification}");
            if (!string.IsNullOrWhiteSpace(understanding))
                return TrimDetail($"理解：{understanding}");
            if (!string.IsNullOrWhiteSpace(extractedText))
                return TrimDetail($"提取文本：{extractedText}");
        }
        catch (JsonException)
        {
            // Non-JSON LLM output is still useful as a short card summary.
        }

        return TrimDetail(text);
    }

    private static string GetJsonString(JsonElement root, string name)
    {
        return root.ValueKind == JsonValueKind.Object
            && root.TryGetProperty(name, out var value)
            && value.ValueKind == JsonValueKind.String
                ? value.GetString() ?? string.Empty
                : string.Empty;
    }

    private static string TrimDetail(string value)
    {
        var text = Clean(value, "屏幕区域识别完成。").ReplaceLineEndings(" ");
        return text.Length <= 140 ? text : $"{text[..140]}...";
    }

    private static string Clean(string? value, string fallback)
    {
        var text = value?.Trim();
        return string.IsNullOrWhiteSpace(text) ? fallback : text;
    }
}
