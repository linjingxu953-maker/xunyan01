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

    public string Name { get; set; } = "妍";
    public string Role { get; set; } = "寻研桌面助手";
    public string AvatarText { get; set; } = "妍";
    public string Description { get; set; } = "主动理解屏幕与任务上下文，清晰地给出下一步。";
    public string Personality { get; set; } = "沉稳可靠";
    public string ToneStyle { get; set; } = "友善";
    public string LanguageStyle { get; set; } = "标准";
    public string ReplyLength { get; set; } = "平衡";
    public bool UseEmoji { get; set; }
    public string SystemPromptSuffix { get; set; } = string.Empty;
    public List<string> PersonalityTraits { get; set; } =
    [
        "可靠",
        "主动"
    ];
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
        Description = Description,
        Personality = Personality,
        ToneStyle = ToneStyle,
        LanguageStyle = LanguageStyle,
        ReplyLength = ReplyLength,
        UseEmoji = UseEmoji,
        SystemPromptSuffix = SystemPromptSuffix,
        PersonalityTraits = [..PersonalityTraits],
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
        PersonalityTraits ??= [];

        foreach (var item in DefaultStateImages)
        {
            StateImages.TryAdd(item.Key, item.Value);
        }
    }
}
