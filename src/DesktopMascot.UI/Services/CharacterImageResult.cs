using Avalonia.Media;

namespace DesktopMascot.UI.Services;

public sealed class CharacterImageResult
{
    public IImage? Image { get; init; }
    public string? FilePath { get; init; }
    public string Message { get; init; } = string.Empty;
    public bool HasImage => Image is not null;
}
