namespace DesktopMascot.UI.ViewModels;

public sealed class ModelProviderOption
{
    public ModelProviderOption(
        string name,
        string displayName,
        string defaultEndpoint,
        string defaultModel,
        IReadOnlyList<string>? modelOptions = null)
    {
        Name = name;
        DisplayName = displayName;
        DefaultEndpoint = defaultEndpoint;
        DefaultModel = defaultModel;
        ModelOptions = modelOptions is { Count: > 0 } ? modelOptions : [defaultModel];
    }

    public string Name { get; }
    public string DisplayName { get; }
    public string DefaultEndpoint { get; }
    public string DefaultModel { get; }
    public IReadOnlyList<string> ModelOptions { get; }

    public override string ToString() => DisplayName;
}
