namespace DesktopMascot.UI.ViewModels;

public sealed class PermissionLevelOption
{
    public PermissionLevelOption(int level, string title, string description)
    {
        Level = level;
        Title = title;
        Description = description;
    }

    public int Level { get; }
    public string Title { get; }
    public string Description { get; }

    public override string ToString() => Title;
}
