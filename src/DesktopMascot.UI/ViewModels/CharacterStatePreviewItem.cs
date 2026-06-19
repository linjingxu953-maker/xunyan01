using Avalonia.Media;
using DesktopMascot.UI.Services;

namespace DesktopMascot.UI.ViewModels;

public sealed class CharacterStatePreviewItem
{
    public CharacterStatePreviewItem(
        string stateKey,
        string displayName,
        string configuredFileName,
        CharacterImageResult result,
        bool usesConfiguredImage)
    {
        StateKey = stateKey;
        DisplayName = displayName;
        ConfiguredFileName = configuredFileName;
        Image = result.Image;
        FilePath = result.FilePath ?? string.Empty;
        HasImage = result.HasImage;
        UsesConfiguredImage = usesConfiguredImage;
        StatusText = !HasImage
            ? "缺失"
            : usesConfiguredImage
                ? "可用"
                : "回退";
        StatusBrush = BrushFrom(!HasImage
            ? "#DC2626"
            : usesConfiguredImage
                ? "#0F766E"
                : "#B45309");
    }

    public string StateKey { get; }
    public string DisplayName { get; }
    public string ConfiguredFileName { get; }
    public IImage? Image { get; }
    public string FilePath { get; }
    public bool HasImage { get; }
    public bool HasNoImage => !HasImage;
    public bool UsesConfiguredImage { get; }
    public bool UsesFallback => HasImage && !UsesConfiguredImage;
    public string StatusText { get; }
    public IBrush StatusBrush { get; }
    public string PreviewText => string.IsNullOrWhiteSpace(DisplayName) ? "图" : DisplayName[..1];
    public string ResolvedFileName => string.IsNullOrWhiteSpace(FilePath) ? "未解析" : Path.GetFileName(FilePath);
    public string DetailText =>
        $"配置：{(string.IsNullOrWhiteSpace(ConfiguredFileName) ? "未配置" : ConfiguredFileName)}；实际：{ResolvedFileName}";

    private static IBrush BrushFrom(string color)
    {
        try
        {
            return new SolidColorBrush(Color.Parse(color));
        }
        catch
        {
            return new SolidColorBrush(Color.Parse("#667085"));
        }
    }
}
