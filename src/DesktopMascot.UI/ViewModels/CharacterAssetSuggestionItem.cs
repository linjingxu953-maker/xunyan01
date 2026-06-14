using Avalonia.Media;

namespace DesktopMascot.UI.ViewModels;

public sealed class CharacterAssetSuggestionItem
{
    public CharacterAssetSuggestionItem(
        string stateKey,
        string displayName,
        string currentFileName,
        string suggestedFileName,
        string statusText,
        string matchReason,
        bool isAlreadyApplied,
        string statusColor)
    {
        StateKey = stateKey;
        DisplayName = displayName;
        CurrentFileName = currentFileName;
        SuggestedFileName = suggestedFileName;
        StatusText = statusText;
        MatchReason = matchReason;
        IsAlreadyApplied = isAlreadyApplied;
        StatusBrush = BrushFrom(statusColor);
    }

    public string StateKey { get; }
    public string DisplayName { get; }
    public string CurrentFileName { get; }
    public string SuggestedFileName { get; }
    public string StatusText { get; }
    public string MatchReason { get; }
    public bool IsAlreadyApplied { get; }
    public bool HasSuggestion => !string.IsNullOrWhiteSpace(SuggestedFileName);
    public bool HasNoSuggestion => !HasSuggestion;
    public IBrush StatusBrush { get; }

    public string SuggestedDisplay => HasSuggestion ? SuggestedFileName : "未找到匹配图片";

    private static IBrush BrushFrom(string color)
    {
        try
        {
            return new SolidColorBrush(Color.Parse(color));
        }
        catch
        {
            return new SolidColorBrush(Color.Parse("#667085"));
        }
    }
}
