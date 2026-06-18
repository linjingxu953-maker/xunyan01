namespace DesktopMascot.UI.ViewModels;

public sealed class ScreenScreenshotPreviewCardState
{
    private ScreenScreenshotPreviewCardState(
        string path,
        string fileName,
        bool canCopyPath,
        bool canTogglePreview,
        bool showExpandedPreview)
    {
        Path = path;
        FileName = fileName;
        CanCopyPath = canCopyPath;
        CanTogglePreview = canTogglePreview;
        ShowExpandedPreview = showExpandedPreview;
    }

    public string Path { get; }
    public string FileName { get; }
    public bool CanCopyPath { get; }
    public bool CanTogglePreview { get; }
    public bool ShowExpandedPreview { get; }
    public string ToggleText => ShowExpandedPreview ? "收起大图" : "查看大图";

    public static ScreenScreenshotPreviewCardState From(ScreenSelectionContextState context, bool isExpanded)
    {
        var canTogglePreview = context.HasScreenshotPreview;
        return new ScreenScreenshotPreviewCardState(
            context.ScreenshotPath,
            context.ScreenshotFileName,
            context.HasScreenshotEvidence,
            canTogglePreview,
            canTogglePreview && isExpanded);
    }
}
