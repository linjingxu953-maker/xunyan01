namespace DesktopMascot.UI.ViewModels;

public sealed class SettingsListItem
{
    public SettingsListItem(string title, string value, string description)
    {
        Title = title;
        Value = value;
        Description = description;
    }

    public string Title { get; }
    public string Value { get; }
    public string Description { get; }
}
