namespace DesktopMascot.UI.ViewModels;

public sealed class ConfirmationDetailItem
{
    public ConfirmationDetailItem(string label, string value)
    {
        Label = label;
        Value = value;
    }

    public string Label { get; }
    public string Value { get; }
}
