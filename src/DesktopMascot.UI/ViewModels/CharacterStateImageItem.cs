using CommunityToolkit.Mvvm.ComponentModel;

namespace DesktopMascot.UI.ViewModels;

public sealed partial class CharacterStateImageItem : ObservableObject
{
    public CharacterStateImageItem(string stateKey, string displayName, string fileName)
    {
        StateKey = stateKey;
        DisplayName = displayName;
        FileName = fileName;
    }

    public string StateKey { get; }
    public string DisplayName { get; }

    [ObservableProperty]
    private string _fileName;

    public override string ToString() => DisplayName;
}
