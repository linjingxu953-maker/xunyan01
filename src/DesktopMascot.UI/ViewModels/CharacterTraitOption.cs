using CommunityToolkit.Mvvm.ComponentModel;

namespace DesktopMascot.UI.ViewModels;

public sealed partial class CharacterTraitOption : ObservableObject
{
    public CharacterTraitOption(string id, string title, string description, bool isSelected = false)
    {
        Id = id;
        Title = title;
        Description = description;
        IsSelected = isSelected;
    }

    public string Id { get; }
    public string Title { get; }
    public string Description { get; }

    [ObservableProperty]
    private bool _isSelected;
}
