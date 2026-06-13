namespace DesktopMascot.UI.Services;

public sealed class MascotCharacterProfile
{
    private static readonly Dictionary<string, string> DefaultStateImages = new()
    {
        ["Idle"] = "idle.png",
        ["Listening"] = "listening.png",
        ["Understanding"] = "thinking.png",
        ["ReadingContext"] = "reading.png",
        ["Planning"] = "planning.png",
        ["WaitingApproval"] = "waiting.png",
        ["Working"] = "working.png",
        ["MemoryConfirm"] = "memory.png",
        ["Reporting"] = "reporting.png",
        ["Completed"] = "completed.png",
        ["Error"] = "error.png"
    };

    public string Name { get; set; } = "小桌灵";
    public string Role { get; set; } = "桌面工作助手";
    public string AvatarText { get; set; } = "灵";
    public string Personality { get; set; } = "沉稳可靠";
    public string Catchphrase { get; set; } = "我在桌面待命，随时可以接任务。";
    public string AccentColor { get; set; } = "#2563EB";
    public string BackgroundColor { get; set; } = "#EEF6FF";
    public string ImageFolder { get; set; } = "assets/characters/default";
    public string AvatarImage { get; set; } = "avatar.png";
    public Dictionary<string, string> StateImages { get; set; } = new(DefaultStateImages);

    public MascotCharacterProfile Clone() => new()
    {
        Name = Name,
        Role = Role,
        AvatarText = AvatarText,
        Personality = Personality,
        Catchphrase = Catchphrase,
        AccentColor = AccentColor,
        BackgroundColor = BackgroundColor,
        ImageFolder = ImageFolder,
        AvatarImage = AvatarImage,
        StateImages = new Dictionary<string, string>(StateImages)
    };

    public void EnsureImageDefaults()
    {
        if (string.IsNullOrWhiteSpace(ImageFolder))
        {
            ImageFolder = "assets/characters/default";
        }

        if (string.IsNullOrWhiteSpace(AvatarImage))
        {
            AvatarImage = "avatar.png";
        }

        StateImages ??= new Dictionary<string, string>();

        foreach (var item in DefaultStateImages)
        {
            StateImages.TryAdd(item.Key, item.Value);
        }
    }
}
