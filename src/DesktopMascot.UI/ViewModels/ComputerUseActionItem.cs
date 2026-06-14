namespace DesktopMascot.UI.ViewModels;

public sealed class ComputerUseActionItem
{
    public ComputerUseActionItem(string actionName, string target, string statusText, string detail, DateTime createdAt)
    {
        ActionName = actionName;
        Target = target;
        StatusText = statusText;
        Detail = detail;
        TimeText = createdAt.ToLocalTime().ToString("HH:mm:ss");
    }

    public string ActionName { get; }
    public string Target { get; }
    public string StatusText { get; }
    public string Detail { get; }
    public string TimeText { get; }
}
