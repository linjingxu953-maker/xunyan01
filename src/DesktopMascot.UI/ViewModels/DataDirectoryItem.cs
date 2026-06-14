using CommunityToolkit.Mvvm.Input;

namespace DesktopMascot.UI.ViewModels;

public sealed class DataDirectoryItem
{
    public DataDirectoryItem(
        string title,
        string path,
        string description,
        string status,
        Action<DataDirectoryItem> openAction)
    {
        Title = title;
        Path = path;
        Description = description;
        Status = status;
        OpenCommand = new RelayCommand(() => openAction(this));
    }

    public string Title { get; }
    public string Path { get; }
    public string Description { get; }
    public string Status { get; }
    public IRelayCommand OpenCommand { get; }
}
