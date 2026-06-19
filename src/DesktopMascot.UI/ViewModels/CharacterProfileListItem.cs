using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using DesktopMascot.UI.Services;

namespace DesktopMascot.UI.ViewModels;

public sealed partial class CharacterProfileListItem : ObservableObject
{
    public CharacterProfileListItem(
        MascotCharacterProfileEntry entry,
        IImage? thumbnail,
        int availableStateImageCount = 0,
        int totalStateImageCount = 0,
        string missingStateImageText = "")
    {
        Entry = entry;
        Thumbnail = thumbnail;
        AccentBrush = BrushFrom(entry.AccentColor);
        AvailableStateImageCount = Math.Max(0, availableStateImageCount);
        TotalStateImageCount = Math.Max(0, totalStateImageCount);
        MissingStateImageText = missingStateImageText;
        StateImageStatusBrush = HasMissingStateImages ? BrushFrom("#FBBF24") : BrushFrom("#34D399");
    }

    public MascotCharacterProfileEntry Entry { get; }
    public string Id => Entry.Id;
    public string Name => Entry.Name;
    public string Role => Entry.Role;
    public string ImageFolder => Entry.ImageFolder;
    public string UpdatedAtText => Entry.UpdatedAtText;
    public bool IsActive => Entry.IsActive;
    public string ActiveText => IsActive ? "当前" : string.Empty;
    public bool HasThumbnail => Thumbnail is not null;
    public bool HasNoThumbnail => Thumbnail is null;
    public int AvailableStateImageCount { get; }
    public int TotalStateImageCount { get; }
    public bool HasMissingStateImages => TotalStateImageCount > 0 && AvailableStateImageCount < TotalStateImageCount;
    public string MissingStateImageText { get; }
    public string StateImageStatusText => TotalStateImageCount <= 0
        ? "状态图未扫描"
        : HasMissingStateImages
            ? $"{AvailableStateImageCount}/{TotalStateImageCount} · 缺 {TotalStateImageCount - AvailableStateImageCount}"
            : $"{AvailableStateImageCount}/{TotalStateImageCount} 已就绪";
    public IBrush AccentBrush { get; }
    public IBrush StateImageStatusBrush { get; }

    public string AvatarText
    {
        get
        {
            var text = string.IsNullOrWhiteSpace(Name) ? "角" : Name.Trim();
            return text.Length <= 2 ? text : text[..1];
        }
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasThumbnail))]
    [NotifyPropertyChangedFor(nameof(HasNoThumbnail))]
    private IImage? _thumbnail;

    private static IBrush BrushFrom(string color)
    {
        try
        {
            return new SolidColorBrush(Color.Parse(color));
        }
        catch
        {
            return new SolidColorBrush(Color.Parse("#2563EB"));
        }
    }
}
