namespace DesktopMascot.UI.Services;

public sealed class ScreenSelectionResult
{
    public int X { get; init; }
    public int Y { get; init; }
    public int Width { get; init; }
    public int Height { get; init; }
    public bool IsConfirmed { get; init; }

    public bool HasRegion => IsConfirmed && Width > 0 && Height > 0;
    public string Summary => $"({X}, {Y}) {Width}x{Height}";
}
