namespace DesktopMascot.UI.ViewModels;

public sealed class ModelProviderOption
{
    public ModelProviderOption(string name, string displayName, string defaultEndpoint, string defaultModel)
    {
        Name = name;
        DisplayName = displayName;
        DefaultEndpoint = defaultEndpoint;
        DefaultModel = defaultModel;
    }

    public string Name { get; }
    public string DisplayName { get; }
    public string DefaultEndpoint { get; }
    public string DefaultModel { get; }

    public override string ToString() => DisplayName;
}
