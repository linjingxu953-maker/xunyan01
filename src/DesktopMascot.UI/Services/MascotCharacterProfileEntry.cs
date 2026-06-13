namespace DesktopMascot.UI.Services;

public sealed class MascotCharacterProfileEntry
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Role { get; init; } = string.Empty;
    public string ImageFolder { get; init; } = string.Empty;
    public string AvatarImagePath { get; init; } = string.Empty;
    public string AccentColor { get; init; } = "#2563EB";
    public bool IsActive { get; init; }
    public DateTime UpdatedAt { get; init; }

    public string UpdatedAtText => UpdatedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm");

    public override string ToString() => string.IsNullOrWhiteSpace(Role)
        ? Name
        : $"{Name} · {Role}";
}
