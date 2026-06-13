using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using DesktopMascot.UI.Services;

namespace DesktopMascot.UI.ViewModels;

public sealed partial class CharacterProfileListItem : ObservableObject
{
    public CharacterProfileListItem(MascotCharacterProfileEntry entry, IImage? thumbnail)
    {
        Entry = entry;
        Thumbnail = thumbnail;
        AccentBrush = BrushFrom(entry.AccentColor);
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
    public IBrush AccentBrush { get; }

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
