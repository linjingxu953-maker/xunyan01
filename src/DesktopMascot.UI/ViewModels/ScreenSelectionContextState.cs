using DesktopMascot.UI.Services;
using System.IO;
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
        detailText: "选择屏幕区域后会显示坐标和尺寸。",
        screenshotPath: string.Empty,
        suggestedActions: [],
        detailItems: []);

    private ScreenSelectionContextState(
        bool hasRegion,
        string title,
        string regionText,
        string sizeText,
        string statusText,
        string detailText,
        string screenshotPath,
        IReadOnlyList<ScreenContextActionItem> suggestedActions,
        IReadOnlyList<ScreenContextDetailItem> detailItems)
    {
        HasRegion = hasRegion;
        Title = title;
        RegionText = regionText;
        SizeText = sizeText;
        StatusText = statusText;
        DetailText = detailText;
        ScreenshotPath = screenshotPath;
        SuggestedActions = suggestedActions;
        DetailItems = detailItems;
    }

    public bool HasRegion { get; }
    public string Title { get; }
    public string RegionText { get; }
    public string SizeText { get; }
    public string StatusText { get; }
    public string DetailText { get; }
    public string ScreenshotPath { get; }
    public string ScreenshotFileName => string.IsNullOrWhiteSpace(ScreenshotPath)
        ? string.Empty
        : Path.GetFileName(ScreenshotPath);
    public bool HasScreenshotEvidence => !string.IsNullOrWhiteSpace(ScreenshotPath);
    public IReadOnlyList<ScreenContextActionItem> SuggestedActions { get; }
    public bool HasSuggestedActions => SuggestedActions.Count > 0;
    public IReadOnlyList<ScreenContextDetailItem> DetailItems { get; }
    public bool HasDetailItems => DetailItems.Count > 0;

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
            detailText: $"将把该屏幕区域交给视觉理解：({result.X}, {result.Y}) {result.Width}x{result.Height}",
            screenshotPath: string.Empty,
            suggestedActions: [],
            detailItems: []);
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
            detailText: Clean(detailText, DetailText),
            screenshotPath: ScreenshotPath,
            suggestedActions: SuggestedActions,
            detailItems: DetailItems);
    }

    public ScreenSelectionContextState WithResult(bool success, string? content, string? error)
    {
        if (!HasRegion)
            return this;

        if (!success)
        {
            var failureScreenshotPath = Clean(BuildScreenshotPath(content), ScreenshotPath);
            return new ScreenSelectionContextState(
                hasRegion: true,
                title: Title,
                regionText: RegionText,
                sizeText: SizeText,
                statusText: "识别失败",
                detailText: Clean(error, Clean(content, "屏幕理解失败，可以重试。")),
                screenshotPath: failureScreenshotPath,
                suggestedActions: [],
                detailItems: []);
        }

        var screenshotPath = Clean(BuildScreenshotPath(content), ScreenshotPath);
        return new ScreenSelectionContextState(
            hasRegion: true,
            title: Title,
            regionText: RegionText,
            sizeText: SizeText,
            statusText: "识别完成",
            detailText: BuildReadableResultSummary(content),
            screenshotPath: screenshotPath,
            suggestedActions: BuildSuggestedActions(content),
            detailItems: BuildDetailItems(content));
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

    private static string GetFirstJsonString(JsonElement root, params string[] names)
    {
        foreach (var name in names)
        {
            var value = GetJsonString(root, name);
            if (!string.IsNullOrWhiteSpace(value))
                return value;
        }

        return string.Empty;
    }

    private static string GetConfidenceText(JsonElement root)
    {
        if (!root.TryGetProperty("confidence", out var confidence))
            return string.Empty;

        if (confidence.ValueKind == JsonValueKind.Number && confidence.TryGetDouble(out var value))
        {
            var percent = value <= 1 ? value * 100 : value;
            return $"{Math.Round(percent)}%";
        }

        return confidence.ValueKind == JsonValueKind.String ? confidence.GetString() ?? string.Empty : string.Empty;
    }

    private static IReadOnlyList<ScreenContextDetailItem> BuildDetailItems(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return [];

        try
        {
            using var document = JsonDocument.Parse(content);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return [];

            var details = new List<ScreenContextDetailItem>();
            AddDetail(details, "识别", GetJsonString(root, "identification"));
            AddDetail(details, "理解", GetJsonString(root, "understanding"));
            AddDetail(details, "提取文本", GetJsonString(root, "extractedText"));
            AddDetail(details, "内容类型", GetJsonString(root, "contentType"));
            AddDetail(details, "置信度", GetConfidenceText(root));
            AddDetail(details, "截图", GetFirstJsonString(root, "screenshotPath", "screenPath", "imagePath", "capturePath", "previewPath"));
            AddDetail(details, "原始结果", GetJsonString(root, "rawResponse"));
            return details;
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static string BuildScreenshotPath(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return string.Empty;

        try
        {
            using var document = JsonDocument.Parse(content);
            var root = document.RootElement;
            return root.ValueKind == JsonValueKind.Object
                ? GetFirstJsonString(root, "screenshotPath", "screenPath", "imagePath", "capturePath", "previewPath")
                : string.Empty;
        }
        catch (JsonException)
        {
            return string.Empty;
        }
    }

    private static void AddDetail(ICollection<ScreenContextDetailItem> details, string label, string value)
    {
        var text = Clean(value, string.Empty).ReplaceLineEndings(" ");
        if (string.IsNullOrWhiteSpace(text))
            return;

        details.Add(new ScreenContextDetailItem(label, text.Length <= 240 ? text : $"{text[..240]}..."));
    }

    private static IReadOnlyList<ScreenContextActionItem> BuildSuggestedActions(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return [];

        try
        {
            using var document = JsonDocument.Parse(content);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return [];

            var actions = new List<ScreenContextActionItem>();
            AddSuggestions(root, actions);
            AddRecommendedActions(root, actions);
            return actions.Count <= 6 ? actions : actions.GetRange(0, 6);
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static void AddSuggestions(JsonElement root, ICollection<ScreenContextActionItem> actions)
    {
        if (!root.TryGetProperty("suggestions", out var suggestions) || suggestions.ValueKind != JsonValueKind.Array)
            return;

        foreach (var item in suggestions.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String)
                continue;

            var title = Clean(item.GetString(), string.Empty);
            if (string.IsNullOrWhiteSpace(title))
                continue;

            actions.Add(new ScreenContextActionItem(title, "建议", title));
        }
    }

    private static void AddRecommendedActions(JsonElement root, ICollection<ScreenContextActionItem> actions)
    {
        if (!root.TryGetProperty("recommendedActions", out var recommendedActions) || recommendedActions.ValueKind != JsonValueKind.Array)
            return;

        foreach (var item in recommendedActions.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
                continue;

            var name = GetJsonString(item, "name");
            var description = GetJsonString(item, "description");
            var actionType = Clean(GetJsonString(item, "actionType"), "动作");
            var riskLevel = Clean(GetJsonString(item, "riskLevel"), "low");
            var title = Clean(name, Clean(description, actionType));
            var prompt = Clean(description, title);
            actions.Add(new ScreenContextActionItem(title, $"{actionType} · {riskLevel}", prompt));
        }
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

public sealed class ScreenContextActionItem(string title, string kindText, string promptText)
{
    public string Title { get; } = title;
    public string KindText { get; } = kindText;
    public string PromptText { get; } = promptText;
}

public sealed class ScreenContextDetailItem(string label, string value)
{
    public string Label { get; } = label;
    public string Value { get; } = value;
}
