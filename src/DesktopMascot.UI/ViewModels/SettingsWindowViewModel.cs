using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DesktopMascot.Core.Configuration;
using DesktopMascot.Core.Enums;
using DesktopMascot.Core.Memory;
using DesktopMascot.Core.Security;
using DesktopMascot.Core.Storage;
using DesktopMascot.UI.Services;

namespace DesktopMascot.UI.ViewModels;

public sealed partial class SettingsWindowViewModel : ObservableObject
{
    private static readonly HashSet<string> SupportedCharacterImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png",
        ".jpg",
        ".jpeg",
        ".bmp",
        ".webp"
    };

    private static readonly Dictionary<string, string[]> CharacterStateMatchKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Idle"] = ["idle", "default", "stand", "standing", "avatar", "站立", "空闲", "默认"],
        ["Listening"] = ["listening", "listen", "user", "look", "聆听", "听", "看向用户"],
        ["Understanding"] = ["understanding", "thinking", "think", "idea", "理解", "思考"],
        ["ReadingContext"] = ["reading", "read", "context", "scan", "读取", "上下文"],
        ["Planning"] = ["planning", "plan", "thinking", "map", "规划", "计划", "思考"],
        ["WaitingApproval"] = ["waiting", "approval", "approve", "wait", "hand", "提醒", "举手", "等待", "确认"],
        ["Working"] = ["working", "work", "busy", "coding", "执行", "工作", "忙碌"],
        ["MemoryConfirm"] = ["memory", "remember", "save", "记忆", "保存"],
        ["Reporting"] = ["reporting", "report", "pose", "summary", "汇报", "报告"],
        ["Completed"] = ["completed", "complete", "done", "happy", "success", "完成", "开心", "成功"],
        ["Error"] = ["error", "failed", "fail", "confused", "warning", "错误", "困惑", "失败", "异常"]
    };

    private static readonly string[] AvatarMatchKeywords =
    [
        "avatar",
        "face",
        "default",
        "stand",
        "standing",
        "idle",
        "头像",
        "站立",
        "默认"
    ];

    private readonly IConfigurationManager _configurationManager;
    private readonly ISettingsDiagnosticsService _diagnosticsService;
    private readonly IOnboardingWindowService _onboardingWindowService;
    private readonly IMascotCharacterStore _characterStore;
    private readonly ICharacterImageService _characterImageService;
    private readonly ICharacterAssetImportService _characterAssetImportService;
    private readonly ICharacterAssetPickerService _characterAssetPickerService;
    private readonly IGlobalHotkeyService _hotkeyService;
    private readonly IPermissionManager? _permissionManager;
    private readonly IAuditLogStore? _auditLogStore;
    private readonly IMemoryStore? _memoryStore;
    private readonly ITaskHistoryStore? _taskHistoryStore;
    private readonly ITaskResultActionService? _taskResultActionService;
    private bool _isApplyingProvider;
    private bool _isApplyingCharacterProfile;
    private AppSettings _settings = new();
    private PermissionSettings _permissionSettings = new();
    private List<AuditLogEntry> _recentAuditLogs = [];
    private int _auditLogTotalCount;
    private MemoryStatistics? _memoryStatistics;
    private List<MemoryEntry> _recentMemoryEntries = [];
    private List<MemoryEntry> _memoryBrowserEntries = [];
    private readonly List<string> _pendingMemoryClearIds = [];
    private bool _isMemoryClearConfirmationPending;
    private TaskHistoryStatistics? _taskHistoryStatistics;
    private List<TaskHistoryRecord> _taskHistoryBrowserRecords = [];
    private string _selectedTaskHistoryCleanupFilter = "全部";
    private bool _isTaskHistoryCleanupConfirmationPending;

    [ObservableProperty] private string _selectedSectionId = "overview";
    [ObservableProperty] private ModelProviderOption? _selectedProvider;
    [ObservableProperty] private string _apiKey = string.Empty;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsApiKeyHidden))]
    [NotifyPropertyChangedFor(nameof(ApiKeyVisibilityButtonText))]
    private bool _isApiKeyVisible;
    [ObservableProperty] private string _apiEndpoint = string.Empty;
    [ObservableProperty] private string _modelName = string.Empty;
    [ObservableProperty] private string _modelSettingsStatus = "模型配置会保存到本机配置目录。";
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsModelConnectionFailed))]
    private bool _hasModelConnectionResult;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsModelConnectionFailed))]
    private bool _isModelConnectionSuccessful;
    [ObservableProperty] private string _modelConnectionResultTitle = "尚未测试";
    [ObservableProperty] private string _modelConnectionResultDetail = "填写 Provider、API Key、Base URL 和模型名后，可以先测试连接再保存。";
    [ObservableProperty] private string _modelConnectionResultMeta = "等待测试";
    [ObservableProperty] private bool _isMimoCodeEnabled;
    [ObservableProperty] private string _mimoCodeExecutablePath = "mimo";
    [ObservableProperty] private string _mimoCodeWorkspacePath = string.Empty;
    [ObservableProperty] private string _mimoCodeModelConfigMode = "AppProvider";
    [ObservableProperty] private string _mimoCodeStatus = "Mimo Code 接入配置会保存到本机配置目录。模型调用仍使用用户自己的 API。";
    [ObservableProperty] private PermissionLevelOption? _selectedAutoApproveLevel;
    [ObservableProperty] private string _auditLogRetentionDays = "30";
    [ObservableProperty] private string _permissionSettingsStatus = "权限页已预留给 M29，当前仅保存本地策略配置。";
    [ObservableProperty] private bool _isMemoryEnabled = true;
    [ObservableProperty] private string _memorySettingsStatus = "M30 记忆确认接口已就绪，当前页面保存记忆开关并预留队列入口。";
    [ObservableProperty] private string _pendingMemoryDraftContent = string.Empty;
    [ObservableProperty] private string _memorySearchText = string.Empty;
    [ObservableProperty] private string _selectedMemoryFilter = "全部";
    [ObservableProperty] private string _memoryClearButtonText = "清理";
    [ObservableProperty] private string _memoryEditDraftContent = string.Empty;
    [ObservableProperty] private string _memoryEditDraftTags = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsMemoryBrowserReadMode))]
    private bool _isMemoryBrowserEditMode;

    [ObservableProperty] private string _taskHistorySettingsStatus = "任务历史会从 ITaskHistoryStore 读取，支持搜索、导出和删除。";
    [ObservableProperty] private string _taskHistorySearchText = string.Empty;
    [ObservableProperty] private string _selectedTaskHistoryStatusFilter = "全部";
    [ObservableProperty] private string _taskHistoryCleanupButtonText = "清理旧记录";
    [ObservableProperty] private string _hotkeySettingsStatus = "快捷键状态会在应用启动后显示。";
    [ObservableProperty] private string _chatHotkeyText = string.Empty;
    [ObservableProperty] private string _screenSelectionHotkeyText = string.Empty;
    [ObservableProperty] private string _dataSettingsStatus = "本机数据目录用于配置、日志、记忆、任务历史和角色资源。";
    [ObservableProperty] private string _dataStorageSummary = "正在等待刷新。";
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(VoiceConfigurationSummary))]
    [NotifyPropertyChangedFor(nameof(TtsPreviewText))]
    private string _ttsVoice = "默认女声";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(VoiceConfigurationSummary))]
    [NotifyPropertyChangedFor(nameof(SpeechRecognitionLanguageDescription))]
    private string _speechRecognitionLanguage = "zh-CN";

    [ObservableProperty] private string _voiceSettingsStatus = "语音配置会保存到本机，录音和播放服务接入后会直接读取。";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPendingMemoryReviewSelected))]
    [NotifyPropertyChangedFor(nameof(HasNoPendingMemoryReviewSelected))]
    private PendingMemoryReviewItem? _selectedPendingMemoryReview;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsMemoryBrowserItemSelected))]
    [NotifyPropertyChangedFor(nameof(HasNoMemoryBrowserItemSelected))]
    private MemoryBrowserItem? _selectedMemoryBrowserItem;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsTaskHistoryBrowserItemSelected))]
    [NotifyPropertyChangedFor(nameof(HasNoTaskHistoryBrowserItemSelected))]
    private TaskHistoryBrowserItem? _selectedTaskHistoryBrowserItem;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotBusy))]
    private bool _isBusy;

    [ObservableProperty] private string _runtimeOverviewStatus = "正在读取运行态配置。";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotModelSectionSelected))]
    private bool _isModelSectionSelected;

    [ObservableProperty] private bool _isOverviewSectionSelected = true;
    [ObservableProperty] private bool _isMimoCodeSectionSelected;
    [ObservableProperty] private bool _isPermissionSectionSelected;
    [ObservableProperty] private bool _isMemorySectionSelected;
    [ObservableProperty] private bool _isTaskHistorySectionSelected;
    [ObservableProperty] private bool _isHotkeySectionSelected;
    [ObservableProperty] private bool _isDataSectionSelected;
    [ObservableProperty] private bool _isAppearanceSectionSelected;
    [ObservableProperty] private string _characterName = "妍";
    [ObservableProperty] private string _characterRole = "寻研桌面助手";
    [ObservableProperty] private string _characterAvatarText = "妍";
    [ObservableProperty] private string _characterDescription = "主动理解屏幕与任务上下文，清晰地给出下一步。";
    [ObservableProperty] private string _characterPersonality = "沉稳可靠";
    [ObservableProperty] private string _characterToneStyle = "友善";
    [ObservableProperty] private string _characterLanguageStyle = "标准";
    [ObservableProperty] private string _characterReplyLength = "平衡";
    [ObservableProperty] private bool _characterUseEmoji;
    [ObservableProperty] private string _characterSystemPromptSuffix = string.Empty;
    [ObservableProperty] private string _characterStylePreview = "点击预览后会用当前角色设定生成一段示例回复。";
    [ObservableProperty] private string _characterCatchphrase = "我在桌面待命，随时可以接任务。";
    [ObservableProperty] private string _characterAccentColor = "#2563EB";
    [ObservableProperty] private string _characterBackgroundColor = "#EEF6FF";
    [ObservableProperty] private string _characterImageFolder = "assets/characters/default";
    [ObservableProperty] private string _characterAvatarImage = "avatar.png";
    [ObservableProperty] private string _characterImageStatus = "未找到角色图片时会使用文字头像。";
    [ObservableProperty] private string _characterAssetWarningText = "选择目录后可逐状态检查图片。";
    [ObservableProperty] private string _characterSaveStatus = "角色配置会自动保存在本机。";
    [ObservableProperty] private string _characterLibraryStatus = "角色库会保存多个可切换的角色档案。";
    [ObservableProperty] private string _characterAssetSuggestionStatus = "扫描图片目录后会生成状态图匹配建议。";
    [ObservableProperty] private string _characterManifestStatus = "角色包格式以寻研主格式为准，Petdex 仅作为可选兼容信息。";
    [ObservableProperty] private string _characterManifestPreview = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasCharacterAvatarImageSuggestion))]
    private string _characterAvatarImageSuggestion = string.Empty;

    [ObservableProperty] private string _characterStatePreviewStatus = "状态图会在这里批量校验。";
    [ObservableProperty] private IImage? _characterImageSource;
    [ObservableProperty] private IBrush _characterAccentBrush = BrushFrom("#2563EB");
    [ObservableProperty] private IBrush _characterBackgroundBrush = BrushFrom("#EEF6FF");

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNoCharacterImage))]
    private bool _hasCharacterImage;

    [ObservableProperty] private CharacterStateImageItem? _selectedCharacterStateImage;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelectedCharacterProfile))]
    private CharacterProfileListItem? _selectedCharacterProfile;

    [ObservableProperty] private string _characterProfileNameDraft = string.Empty;

    public SettingsWindowViewModel(
        IConfigurationManager configurationManager,
        ISettingsDiagnosticsService diagnosticsService,
        IOnboardingWindowService onboardingWindowService,
        IMascotCharacterStore characterStore,
        ICharacterImageService characterImageService,
        ICharacterAssetImportService characterAssetImportService,
        ICharacterAssetPickerService characterAssetPickerService,
        IGlobalHotkeyService hotkeyService,
        IPermissionManager? permissionManager = null,
        IAuditLogStore? auditLogStore = null,
        IMemoryStore? memoryStore = null,
        ITaskHistoryStore? taskHistoryStore = null,
        ITaskResultActionService? taskResultActionService = null)
    {
        _configurationManager = configurationManager;
        _diagnosticsService = diagnosticsService;
        _onboardingWindowService = onboardingWindowService;
        _characterStore = characterStore;
        _characterImageService = characterImageService;
        _characterAssetImportService = characterAssetImportService;
        _characterAssetPickerService = characterAssetPickerService;
        _hotkeyService = hotkeyService;
        _permissionManager = permissionManager;
        _auditLogStore = auditLogStore;
        _memoryStore = memoryStore;
        _taskHistoryStore = taskHistoryStore;
        _taskResultActionService = taskResultActionService;

        Sections =
        [
            new SettingsSectionItem("overview", "总览", "运行态验收"),
            new SettingsSectionItem("model", "模型", "Provider、API Key、模型名"),
            new SettingsSectionItem("mimoCode", "Mimo Code", "本机代码能力接入"),
            new SettingsSectionItem("permission", "权限", "确认策略与审计"),
            new SettingsSectionItem("memory", "记忆", "记忆保存与检索"),
            new SettingsSectionItem("history", "任务历史", "记录、结果与工具调用"),
            new SettingsSectionItem("hotkey", "快捷键", "唤起与桌面操作"),
            new SettingsSectionItem("data", "日志/数据", "本地目录与日志"),
            new SettingsSectionItem("appearance", "角色外观", "小人形象与主题")
        ];

        Providers = new ObservableCollection<ModelProviderOption>(ModelProviderCatalog.CreateDefaults());
        MemoryFilterOptions = ["全部", "用户偏好", "项目信息", "技能流程", "任务历史", "仅待确认"];
        TaskHistoryStatusFilterOptions = ["全部", "已完成", "失败", "已取消", "进行中"];
        CharacterToneStyleOptions = ["友善", "专业", "轻松", "可爱", "沉稳", "讽刺"];
        CharacterLanguageStyleOptions = ["标准", "简洁", "详细", "技术", "口语"];
        CharacterReplyLengthOptions = ["短", "平衡", "详细"];
        TtsVoiceOptions = ["默认女声", "温柔女声", "清晰男声", "沉稳旁白", "活泼少女"];
        SpeechRecognitionLanguageOptions = ["zh-CN", "zh-HK", "en-US", "ja-JP", "ko-KR"];
        CharacterTraitOptions =
        [
            new CharacterTraitOption("reliable", "可靠", "优先给出可执行结论", true),
            new CharacterTraitOption("proactive", "主动", "会补充下一步建议", true),
            new CharacterTraitOption("patient", "耐心", "解释更照顾上下文"),
            new CharacterTraitOption("strict", "严谨", "更重视验证和边界"),
            new CharacterTraitOption("warm", "温和", "语气更柔和"),
            new CharacterTraitOption("direct", "直接", "减少铺垫，直达结论")
        ];

        PermissionAutoApproveLevels =
        [
            new PermissionLevelOption(0, "每次确认", "默认策略。所有需要权限的操作都弹窗确认。"),
            new PermissionLevelOption(1, "低风险自动批准", "仅允许低风险读取和无副作用操作自动通过。"),
            new PermissionLevelOption(2, "常规操作自动批准", "为常见工具调用减少打断，高风险仍需确认。"),
            new PermissionLevelOption(3, "中等风险自动批准", "适合受信任项目，高风险写入和命令仍建议确认。"),
            new PermissionLevelOption(4, "高风险前确认", "仅在明显危险或不可逆操作前打断。"),
            new PermissionLevelOption(5, "尽量少打断", "多数操作自动通过，仅保留最高风险确认。"),
            new PermissionLevelOption(6, "全部自动批准", "仅建议在本地测试环境使用。")
        ];

        PermissionReadinessItems = [];
        RuntimeOverviewItems = [];
        PermissionRequestTypeItems = [];
        PermissionAuditItems = [];
        MimoCodeReadinessItems = [];
        MemoryReadinessItems = [];
        MemoryStatsItems = [];
        MemoryActionItems = [];
        MemoryBrowserItems = [];
        TaskHistoryStatsItems = [];
        TaskHistoryBrowserItems = [];
        TaskHistoryEventItems = [];
        TaskHistoryToolCallItems = [];
        HotkeyItems = [];
        DataDirectoryItems = [];
        PendingMemoryReviews = [];
        CharacterProfiles = [];
        CharacterAssetSuggestions = [];
        CharacterStatePreviewItems = [];
        CharacterManifestFormatItems = [];
        CharacterStateImageItems =
        [
            new CharacterStateImageItem("Idle", "空闲 / 默认", "站立.png"),
            new CharacterStateImageItem("Listening", "聆听中", "Listening：看向用户.png"),
            new CharacterStateImageItem("Understanding", "理解中", "Understanding：思考图.png"),
            new CharacterStateImageItem("ReadingContext", "读取上下文", "站立.png"),
            new CharacterStateImageItem("Planning", "规划中", "Understanding：思考图.png"),
            new CharacterStateImageItem("WaitingApproval", "等待确认", "WaitingApproval：提醒、举手图.png"),
            new CharacterStateImageItem("Working", "工作中", "Working：忙碌图.png"),
            new CharacterStateImageItem("MemoryConfirm", "记忆确认", "站立.png"),
            new CharacterStateImageItem("Reporting", "汇报中", "pose.png"),
            new CharacterStateImageItem("Completed", "完成", "Completed：开心、完成图.png"),
            new CharacterStateImageItem("Error", "出错", "Error：困惑、错误图.png")
        ];
        foreach (var item in CharacterStateImageItems)
        {
            item.PropertyChanged += OnCharacterStateImageItemChanged;
        }

        PendingMemoryReviews.CollectionChanged += (_, _) =>
        {
            RefreshPendingMemoryReviewState();
            RefreshMemoryCards();
        };
        MemoryBrowserItems.CollectionChanged += (_, _) => RefreshMemoryBrowserState();
        TaskHistoryBrowserItems.CollectionChanged += (_, _) => RefreshTaskHistoryBrowserState();
        CharacterProfiles.CollectionChanged += (_, _) => RefreshCharacterProfileState();
        CharacterAssetSuggestions.CollectionChanged += (_, _) => RefreshCharacterAssetSuggestionState();

        SelectedProvider = Providers[0];
        SelectedAutoApproveLevel = PermissionAutoApproveLevels[0];
        SelectedCharacterStateImage = CharacterStateImageItems[0];
        ApplyProviderDefaultsIfEmpty();
        ApplyCharacterProfile(_characterStore.Load(), save: false);
        RefreshCharacterProfiles();
        LoadHotkeyTextFromService();
        RefreshMimoCodeCards();
        RefreshPermissionCards();
        RefreshMemoryCards();
        RefreshTaskHistoryCards();
        RefreshHotkeyCards();
        RefreshDataDirectoryItems();
        RefreshRuntimeOverviewCards();
        RefreshCharacterManifestPreviewCore();
    }

    public ObservableCollection<SettingsSectionItem> Sections { get; }
    public ObservableCollection<ModelProviderOption> Providers { get; }
    public ObservableCollection<string> MemoryFilterOptions { get; }
    public ObservableCollection<string> TaskHistoryStatusFilterOptions { get; }
    public ObservableCollection<PermissionLevelOption> PermissionAutoApproveLevels { get; }
    public ObservableCollection<string> CharacterToneStyleOptions { get; }
    public ObservableCollection<string> CharacterLanguageStyleOptions { get; }
    public ObservableCollection<string> CharacterReplyLengthOptions { get; }
    public ObservableCollection<string> TtsVoiceOptions { get; }
    public ObservableCollection<string> SpeechRecognitionLanguageOptions { get; }
    public ObservableCollection<CharacterTraitOption> CharacterTraitOptions { get; }
    public ObservableCollection<SettingsListItem> RuntimeOverviewItems { get; }
    public ObservableCollection<SettingsListItem> MimoCodeReadinessItems { get; }
    public ObservableCollection<SettingsListItem> PermissionReadinessItems { get; }
    public ObservableCollection<SettingsListItem> PermissionRequestTypeItems { get; }
    public ObservableCollection<SettingsListItem> PermissionAuditItems { get; }
    public ObservableCollection<SettingsListItem> MemoryReadinessItems { get; }
    public ObservableCollection<SettingsListItem> MemoryStatsItems { get; }
    public ObservableCollection<SettingsListItem> MemoryActionItems { get; }
    public ObservableCollection<MemoryBrowserItem> MemoryBrowserItems { get; }
    public ObservableCollection<SettingsListItem> TaskHistoryStatsItems { get; }
    public ObservableCollection<TaskHistoryBrowserItem> TaskHistoryBrowserItems { get; }
    public ObservableCollection<SettingsListItem> TaskHistoryEventItems { get; }
    public ObservableCollection<SettingsListItem> TaskHistoryToolCallItems { get; }
    public ObservableCollection<SettingsListItem> HotkeyItems { get; }
    public ObservableCollection<DataDirectoryItem> DataDirectoryItems { get; }
    public ObservableCollection<PendingMemoryReviewItem> PendingMemoryReviews { get; }
    public ObservableCollection<CharacterProfileListItem> CharacterProfiles { get; }
    public ObservableCollection<CharacterAssetSuggestionItem> CharacterAssetSuggestions { get; }
    public ObservableCollection<CharacterStateImageItem> CharacterStateImageItems { get; }
    public ObservableCollection<CharacterStatePreviewItem> CharacterStatePreviewItems { get; }
    public ObservableCollection<SettingsListItem> CharacterManifestFormatItems { get; }
    public bool IsApiKeyHidden => !IsApiKeyVisible;
    public string ApiKeyVisibilityButtonText => IsApiKeyVisible ? "隐藏" : "显示";
    public bool IsModelConnectionFailed => HasModelConnectionResult && !IsModelConnectionSuccessful;
    public string VoiceConfigurationSummary =>
        $"TTS：{CleanText(TtsVoice, "默认女声", 32)}；识别语言：{CleanText(SpeechRecognitionLanguage, "zh-CN", 16)}";

    public string TtsPreviewText =>
        $"预览文案：你好，我是寻研。当前会使用 {CleanText(TtsVoice, "默认女声", 32)} 的语音配置。";

    public string SpeechRecognitionLanguageDescription => SpeechRecognitionLanguage switch
    {
        "zh-CN" => "普通话识别，适合中文桌面任务和日常问答。",
        "zh-HK" => "粤语/繁体中文场景预留，实际效果取决于后续 STT Provider。",
        "en-US" => "英文识别，适合英文网页、代码和学习任务。",
        "ja-JP" => "日语识别预留。",
        "ko-KR" => "韩语识别预留。",
        _ => "自定义识别语言，实际支持范围取决于后续 STT Provider。"
    };

    public bool IsNotModelSectionSelected => !IsModelSectionSelected;
    public bool HasPendingMemoryReviews => PendingMemoryReviews.Count > 0;
    public bool HasNoPendingMemoryReviews => !HasPendingMemoryReviews;
    public bool IsPendingMemoryReviewSelected => SelectedPendingMemoryReview is not null;
    public bool HasNoPendingMemoryReviewSelected => !IsPendingMemoryReviewSelected;
    public bool HasMemoryBrowserItems => MemoryBrowserItems.Count > 0;
    public bool HasNoMemoryBrowserItems => !HasMemoryBrowserItems;
    public bool IsMemoryBrowserItemSelected => SelectedMemoryBrowserItem is not null;
    public bool HasNoMemoryBrowserItemSelected => !IsMemoryBrowserItemSelected;
    public bool IsMemoryBrowserReadMode => !IsMemoryBrowserEditMode;
    public bool HasTaskHistoryBrowserItems => TaskHistoryBrowserItems.Count > 0;
    public bool HasNoTaskHistoryBrowserItems => !HasTaskHistoryBrowserItems;
    public bool IsTaskHistoryBrowserItemSelected => SelectedTaskHistoryBrowserItem is not null;
    public bool HasNoTaskHistoryBrowserItemSelected => !IsTaskHistoryBrowserItemSelected;
    public bool HasTaskHistoryEvents => TaskHistoryEventItems.Count > 0;
    public bool HasNoTaskHistoryEvents => !HasTaskHistoryEvents;
    public bool HasTaskHistoryToolCalls => TaskHistoryToolCallItems.Count > 0;
    public bool HasNoTaskHistoryToolCalls => !HasTaskHistoryToolCalls;
    public bool HasNoCharacterImage => !HasCharacterImage;
    public bool HasCharacterProfiles => CharacterProfiles.Count > 0;
    public bool HasNoCharacterProfiles => !HasCharacterProfiles;
    public bool HasSelectedCharacterProfile => SelectedCharacterProfile is not null;
    public bool HasCharacterAssetSuggestions => CharacterAssetSuggestions.Count > 0;
    public bool HasNoCharacterAssetSuggestions => !HasCharacterAssetSuggestions;
    public bool HasCharacterAvatarImageSuggestion => !string.IsNullOrWhiteSpace(CharacterAvatarImageSuggestion);
    public bool HasCharacterStatePreviews => CharacterStatePreviewItems.Count > 0;
    public bool IsNotBusy => !IsBusy;

    public async Task LoadAsync(CancellationToken ct = default)
    {
        IsBusy = true;

        try
        {
            _settings = await _configurationManager.GetAppSettingsAsync(ct);
            ApiKey = _settings.ApiKey;
            ApiEndpoint = _settings.ApiEndpoint;
            ModelName = _settings.ModelName;
            _isApplyingProvider = true;
            try
            {
                SelectedProvider = Providers.FirstOrDefault(x => x.Name == _settings.ProviderName) ??
                                   InferProvider(_settings.ApiEndpoint) ??
                                   Providers[0];
            }
            finally
            {
                _isApplyingProvider = false;
            }
            IsMimoCodeEnabled = _settings.MimoCodeEnabled;
            MimoCodeExecutablePath = _settings.MimoCodeExecutablePath;
            MimoCodeWorkspacePath = _settings.MimoCodeWorkspaceDirectory;
            MimoCodeModelConfigMode = string.IsNullOrWhiteSpace(_settings.MimoCodeModelConfigMode)
                ? "AppProvider"
                : _settings.MimoCodeModelConfigMode;
            IsMemoryEnabled = _settings.MemoryEnabled;
            TtsVoice = string.IsNullOrWhiteSpace(_settings.TtsVoice) ? "默认女声" : _settings.TtsVoice;
            SpeechRecognitionLanguage = string.IsNullOrWhiteSpace(_settings.SpeechRecognitionLanguage)
                ? "zh-CN"
                : _settings.SpeechRecognitionLanguage;

            _permissionSettings = await _configurationManager.GetPermissionSettingsAsync(ct);
            SelectedAutoApproveLevel =
                PermissionAutoApproveLevels.FirstOrDefault(x => x.Level == _permissionSettings.AutoApproveLevel) ??
                PermissionAutoApproveLevels[0];
            AuditLogRetentionDays = _permissionSettings.AuditLogRetentionDays.ToString();

            ModelSettingsStatus = "已加载本机模型配置。";
            MimoCodeStatus = "已加载 Mimo Code 接入配置。模型调用不会使用内置 Key。";
            PermissionSettingsStatus = "已加载本机权限策略。正在读取权限审计数据。";
            MemorySettingsStatus = "已加载本机记忆开关。正在读取记忆统计。";
            VoiceSettingsStatus = "已加载本机语音配置。";
            ApplyCharacterProfile(_characterStore.Load(), save: false);
            CharacterSaveStatus = "已加载本机角色外观配置。";
            RefreshCharacterProfiles();
            LoadHotkeyTextFromService();
            RefreshMimoCodeCards();
            await RefreshPermissionSnapshotAsync(ct);
            await RefreshMemorySnapshotAsync(ct);
            await RefreshTaskHistorySnapshotAsync(ct);
            RefreshHotkeyCards();
            RefreshDataDirectoryItems();
            RefreshRuntimeOverviewCards();
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void SelectSection(string? sectionId)
    {
        SelectSectionById(sectionId);
    }

    public void SelectSectionById(string? sectionId)
    {
        if (string.IsNullOrWhiteSpace(sectionId))
            return;

        SelectedSectionId = sectionId;
        IsOverviewSectionSelected = sectionId == "overview";
        IsModelSectionSelected = sectionId == "model";
        IsMimoCodeSectionSelected = sectionId == "mimoCode";
        IsPermissionSectionSelected = sectionId == "permission";
        IsMemorySectionSelected = sectionId == "memory";
        IsTaskHistorySectionSelected = sectionId == "history";
        IsHotkeySectionSelected = sectionId == "hotkey";
        IsDataSectionSelected = sectionId == "data";
        IsAppearanceSectionSelected = sectionId == "appearance";
    }

    [RelayCommand]
    private void UseProviderDefaults()
    {
        if (SelectedProvider is null)
            return;

        ApiEndpoint = SelectedProvider.DefaultEndpoint;
        ModelName = SelectedProvider.DefaultModel;
        ModelSettingsStatus = $"已填入 {SelectedProvider.DisplayName} 的默认端点和模型名。";
        RefreshRuntimeOverviewCards();
    }

    [RelayCommand]
    private void ToggleApiKeyVisibility()
    {
        IsApiKeyVisible = !IsApiKeyVisible;
    }

    [RelayCommand]
    private async Task SaveModelSettings()
    {
        if (SelectedProvider is null)
            return;

        if (string.IsNullOrWhiteSpace(ApiEndpoint) || string.IsNullOrWhiteSpace(ModelName))
        {
            ModelSettingsStatus = "Base URL 和模型名不能为空。";
            return;
        }

        IsBusy = true;

        try
        {
            _settings.ProviderName = SelectedProvider.Name;
            _settings.ApiKey = ApiKey.Trim();
            _settings.ApiEndpoint = ApiEndpoint.Trim();
            _settings.ModelName = ModelName.Trim();
            _settings.TtsVoice = CleanText(TtsVoice, "默认女声", 32);
            _settings.SpeechRecognitionLanguage = CleanText(SpeechRecognitionLanguage, "zh-CN", 16);
            await _configurationManager.SaveAppSettingsAsync(_settings);

            ModelSettingsStatus = string.IsNullOrWhiteSpace(_settings.ApiKey)
                ? "已保存模型配置，但 API Key 为空。"
                : $"已保存 {_settings.ProviderName} / {_settings.ModelName}。";
            VoiceSettingsStatus = $"已保存语音配置：{_settings.TtsVoice} / {_settings.SpeechRecognitionLanguage}。";
            RefreshRuntimeOverviewCards();
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void PreviewTtsVoice()
    {
        VoiceSettingsStatus =
            $"{TtsPreviewText} TTS 服务接入后会播放这段预览；当前先保存选择和展示状态。";
    }

    [RelayCommand]
    private async Task SaveVoiceSettings()
    {
        IsBusy = true;

        try
        {
            _settings.TtsVoice = CleanText(TtsVoice, "默认女声", 32);
            _settings.SpeechRecognitionLanguage = CleanText(SpeechRecognitionLanguage, "zh-CN", 16);
            await _configurationManager.SaveAppSettingsAsync(_settings);

            VoiceSettingsStatus = $"已保存语音配置：{VoiceConfigurationSummary}。聊天区麦克风和朗读按钮会按该配置展示状态。";
            RefreshRuntimeOverviewCards();
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task TestConnection()
    {
        if (SelectedProvider is null)
        {
            ModelSettingsStatus = "请先选择 Provider。";
            SetModelConnectionResult(false, "配置未完成", "请选择 Provider 后再测试连接。", "本地校验");
            return;
        }

        if (string.IsNullOrWhiteSpace(ApiEndpoint) || string.IsNullOrWhiteSpace(ModelName))
        {
            ModelSettingsStatus = "请先填写 Base URL 和模型名。";
            SetModelConnectionResult(false, "配置未完成", "Base URL 和模型名不能为空。", "本地校验");
            return;
        }

        if (string.IsNullOrWhiteSpace(ApiKey) && SelectedProvider.Name != "Local")
        {
            ModelSettingsStatus = "当前 API Key 为空。远程 Provider 需要用户自己的 API Key。";
            SetModelConnectionResult(false, "缺少 API Key", "远程 Provider 不会使用内置 Key，需要用户填写自己的 API Key。", "本地校验");
            return;
        }

        IsBusy = true;
        ModelSettingsStatus = $"正在测试 {SelectedProvider.DisplayName}...";
        HasModelConnectionResult = false;

        try
        {
            var result = await _diagnosticsService.TestModelConnectionAsync(
                new ModelConnectionTestRequest(
                    SelectedProvider.Name,
                    ApiEndpoint,
                    ModelName,
                    ApiKey));

            SetModelConnectionResult(
                result.Success,
                result.Success ? "连接测试通过" : "连接测试失败",
                result.Detail,
                $"{SelectedProvider.DisplayName} / {ModelName}");
            ModelSettingsStatus = $"{result.Message} {result.Detail}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ReopenOnboarding()
    {
        IsBusy = true;

        try
        {
            await _onboardingWindowService.ShowOnboardingWindowAsync();
            ModelSettingsStatus = "已打开首次启动向导，可重新选择内置 Agent / Mimo Code 和模型配置。";
        }
        catch (Exception ex)
        {
            ModelSettingsStatus = $"打开首次启动向导失败：{ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task SaveMimoCodeSettings()
    {
        if (string.IsNullOrWhiteSpace(MimoCodeExecutablePath))
        {
            MimoCodeStatus = "Mimo Code 可执行路径不能为空。";
            return;
        }

        IsBusy = true;

        try
        {
            _settings.MimoCodeEnabled = IsMimoCodeEnabled;
            _settings.MimoCodeExecutablePath = MimoCodeExecutablePath.Trim();
            _settings.MimoCodeWorkspaceDirectory = MimoCodeWorkspacePath.Trim();
            _settings.MimoCodeModelConfigMode = NormalizeMimoCodeModelMode(MimoCodeModelConfigMode);
            await _configurationManager.SaveAppSettingsAsync(_settings);

            MimoCodeStatus = IsMimoCodeEnabled
                ? $"已保存 Mimo Code 接入配置，模型来源：{GetMimoCodeModelModeText(_settings.MimoCodeModelConfigMode)}。"
                : "已保存 Mimo Code 配置，但当前未启用。";
            RefreshMimoCodeCards();
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void UseAppProviderForMimoCode()
    {
        MimoCodeModelConfigMode = "AppProvider";
        MimoCodeStatus = "已选择使用寻研设置中心的 Provider/API Key。";
        RefreshMimoCodeCards();
    }

    [RelayCommand]
    private void UseMimoLocalConfig()
    {
        MimoCodeModelConfigMode = "MimoLocalConfig";
        MimoCodeStatus = "已选择使用用户本机已有的 Mimo Code 配置。";
        RefreshMimoCodeCards();
    }

    [RelayCommand]
    private async Task TestMimoCodeConnection()
    {
        IsBusy = true;
        MimoCodeStatus = "正在检测 Mimo Code 本机配置...";

        try
        {
            var result = await _diagnosticsService.TestMimoCodeAsync(
                new MimoCodeConnectionTestRequest(
                    IsMimoCodeEnabled,
                    MimoCodeExecutablePath,
                    MimoCodeWorkspacePath,
                    NormalizeMimoCodeModelMode(MimoCodeModelConfigMode),
                    SelectedProvider?.Name ?? _settings.ProviderName,
                    ApiEndpoint,
                    ModelName,
                    ApiKey));

            MimoCodeStatus = result.Success
                ? $"{result.Message} {result.Detail}"
                : $"{result.Message} {result.Detail}";
            RefreshMimoCodeCards();
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task SavePermissionSettings()
    {
        if (SelectedAutoApproveLevel is null)
            return;

        if (!int.TryParse(AuditLogRetentionDays, out var retentionDays) ||
            retentionDays is < 1 or > 3650)
        {
            PermissionSettingsStatus = "审计日志保留天数需要在 1 到 3650 之间。";
            return;
        }

        IsBusy = true;

        try
        {
            _permissionSettings.AutoApproveLevel = SelectedAutoApproveLevel.Level;
            _permissionSettings.AuditLogRetentionDays = retentionDays;
            await _configurationManager.SavePermissionSettingsAsync(_permissionSettings);

            PermissionSettingsStatus =
                $"已保存权限策略：{SelectedAutoApproveLevel.Title}，审计日志保留 {retentionDays} 天。";
            await RefreshPermissionSnapshotAsync();
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task RefreshPermissionStatus()
    {
        IsBusy = true;

        try
        {
            await RefreshPermissionSnapshotAsync();
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task OpenPermissionAudit()
    {
        IsBusy = true;

        try
        {
            await RefreshPermissionSnapshotAsync();
            PermissionSettingsStatus = _recentAuditLogs.Count == 0
                ? "权限审计服务已连接，当前还没有审计记录。"
                : $"已显示最近 {_recentAuditLogs.Count} 条权限审计记录。";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void ManagePermanentPermissions()
    {
        if (_permissionSettings.PermanentPermissions.Count == 0)
        {
            PermissionSettingsStatus = "当前配置里没有永久授权项；运行时永久授权列表尚未通过 IPermissionManager 暴露。";
            return;
        }

        var preview = string.Join("、", _permissionSettings.PermanentPermissions
            .Take(3)
            .Select(x => $"{x.Key} L{x.Value}"));
        PermissionSettingsStatus =
            $"当前配置含 {_permissionSettings.PermanentPermissions.Count} 项永久授权：{preview}。撤销入口后续会接二次确认。";
    }

    [RelayCommand]
    private async Task SaveMemorySettings()
    {
        IsBusy = true;

        try
        {
            _settings.MemoryEnabled = IsMemoryEnabled;
            await _configurationManager.SaveAppSettingsAsync(_settings);

            MemorySettingsStatus = IsMemoryEnabled
                ? "已开启记忆功能。保存长期记忆前会通过 IMemoryConfirmationPrompt 请求确认。"
                : "已关闭记忆功能。后续工具链应跳过新记忆保存。";
            await RefreshMemorySnapshotAsync();
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task RefreshMemoryStatus()
    {
        IsBusy = true;

        try
        {
            await RefreshMemorySnapshotAsync();
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void RefreshHotkeyStatus()
    {
        LoadHotkeyTextFromService();
        RefreshHotkeyCards();
    }

    [RelayCommand]
    private void SaveHotkeySettings()
    {
        if (!HotkeyGesture.TryParse(ChatHotkeyText, out var chatHotkey, out var chatError))
        {
            HotkeySettingsStatus = $"聊天唤起快捷键无效：{chatError}";
            return;
        }

        if (!HotkeyGesture.TryParse(ScreenSelectionHotkeyText, out var screenSelectionHotkey, out var screenSelectionError))
        {
            HotkeySettingsStatus = $"屏幕圈选快捷键无效：{screenSelectionError}";
            return;
        }

        var result = _hotkeyService.UpdateHotkeys(chatHotkey, screenSelectionHotkey);
        LoadHotkeyTextFromService();
        RefreshHotkeyCards();
        HotkeySettingsStatus = result.Message;
    }

    [RelayCommand]
    private void ResetHotkeySettings()
    {
        var result = _hotkeyService.ResetHotkeys();
        LoadHotkeyTextFromService();
        RefreshHotkeyCards();
        HotkeySettingsStatus = result.Message;
    }

    [RelayCommand]
    private void RefreshDataDirectories()
    {
        RefreshDataDirectoryItems();
        DataSettingsStatus = "已刷新本机数据目录状态。";
    }

    [RelayCommand]
    private void OpenDataRootDirectory()
    {
        OpenDirectory(GetCoreDataRoot(), "核心数据根目录");
    }

    [RelayCommand]
    private void ClearDataCache()
    {
        var cacheDirectory = GetCacheDirectory();
        Directory.CreateDirectory(cacheDirectory);

        var removedFiles = 0;
        var removedDirectories = 0;
        foreach (var file in Directory.EnumerateFiles(cacheDirectory, "*", SearchOption.AllDirectories).ToArray())
        {
            try
            {
                File.Delete(file);
                removedFiles++;
            }
            catch
            {
                // Cache cleanup is best effort; locked files can be cleared later.
            }
        }

        foreach (var directory in Directory
                     .EnumerateDirectories(cacheDirectory, "*", SearchOption.AllDirectories)
                     .OrderByDescending(path => path.Length)
                     .ToArray())
        {
            try
            {
                Directory.Delete(directory, recursive: false);
                removedDirectories++;
            }
            catch
            {
                // Non-empty or locked directories are left in place.
            }
        }

        RefreshDataDirectoryItems();
        DataSettingsStatus = $"已清理缓存目录：删除 {removedFiles} 个文件、{removedDirectories} 个空目录。";
    }

    [RelayCommand]
    private async Task OpenMemoryBrowser()
    {
        IsBusy = true;

        try
        {
            await RefreshMemorySnapshotAsync();
            MemorySettingsStatus = _memoryBrowserEntries.Count == 0
                ? "记忆存储已连接，当前还没有已保存记忆。"
                : $"已加载 {_memoryBrowserEntries.Count} 条记忆，可继续搜索、筛选和查看详情。";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task SearchMemoryBrowser()
    {
        ResetMemoryClearConfirmation();
        ResetMemoryEditMode();

        if (_memoryStore is null)
        {
            MemorySettingsStatus = "IMemoryStore 未注入，暂时无法搜索记忆。";
            return;
        }

        IsBusy = true;

        try
        {
            var query = MemorySearchText.Trim();
            var memoryType = ResolveMemoryFilterType(SelectedMemoryFilter);
            var result = await _memoryStore.SearchAsync(query, memoryType, 100);
            var entries = result.Entries
                .Where(entry => SelectedMemoryFilter != "仅待确认" || !entry.IsConfirmed)
                .OrderByDescending(entry => entry.UpdatedAt)
                .ToList();

            PopulateMemoryBrowserItems(entries);
            MemorySettingsStatus = entries.Count == 0
                ? "没有找到匹配的记忆。"
                : $"已匹配 {entries.Count} 条记忆。";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ResetMemoryBrowser()
    {
        ResetMemoryClearConfirmation();
        ResetMemoryEditMode();
        MemorySearchText = string.Empty;
        SelectedMemoryFilter = "全部";

        IsBusy = true;

        try
        {
            await RefreshMemorySnapshotAsync();
            MemorySettingsStatus = "已恢复默认记忆视图。";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ExportMemory()
    {
        if (_memoryStore is null)
        {
            MemorySettingsStatus = "IMemoryStore 未注入，暂时无法导出记忆。";
            return;
        }

        IsBusy = true;

        try
        {
            var data = await _memoryStore.ExportAsync();
            var exportDirectory = Path.Combine(GetLocalDataRoot(), "Exports");
            Directory.CreateDirectory(exportDirectory);
            var exportPath = Path.Combine(exportDirectory, $"memories-{DateTime.Now:yyyyMMdd-HHmmss}.json");
            await File.WriteAllTextAsync(exportPath, data);
            MemorySettingsStatus = $"已导出记忆到 {exportPath}。";
            await RefreshMemorySnapshotAsync();
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ImportMemory()
    {
        ResetMemoryClearConfirmation();
        ResetMemoryEditMode();

        if (_memoryStore is null)
        {
            MemorySettingsStatus = "IMemoryStore 未注入，暂时无法导入记忆。";
            return;
        }

        var filePath = await _characterAssetPickerService.PickMemoryImportFileAsync();
        if (string.IsNullOrWhiteSpace(filePath))
        {
            MemorySettingsStatus = "已取消记忆导入。";
            return;
        }

        if (!File.Exists(filePath))
        {
            MemorySettingsStatus = $"导入文件不存在：{filePath}";
            return;
        }

        IsBusy = true;

        try
        {
            var data = await File.ReadAllTextAsync(filePath);
            if (string.IsNullOrWhiteSpace(data))
            {
                MemorySettingsStatus = "导入文件为空，未写入记忆。";
                return;
            }

            var imported = await _memoryStore.ImportAsync(data);
            await RefreshMemorySnapshotAsync();
            MemorySettingsStatus = imported == 0
                ? $"导入完成，但文件中没有新增记忆：{filePath}"
                : $"已导入 {imported} 条记忆：{filePath}";
        }
        catch (Exception ex)
        {
            MemorySettingsStatus = $"记忆导入失败：{CleanText(ex.Message, "未知错误", 120)}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ClearMemory()
    {
        ResetMemoryEditMode();

        if (_memoryStore is null)
        {
            MemorySettingsStatus = "IMemoryStore 未注入，暂时无法清理记忆。";
            return;
        }

        if (!_isMemoryClearConfirmationPending)
        {
            _pendingMemoryClearIds.Clear();
            _pendingMemoryClearIds.AddRange(MemoryBrowserItems.Select(item => item.Id));

            if (_pendingMemoryClearIds.Count == 0)
            {
                MemorySettingsStatus = "当前记忆浏览器里没有可清理条目。";
                return;
            }

            _isMemoryClearConfirmationPending = true;
            MemoryClearButtonText = "确认清理";
            MemorySettingsStatus =
                $"再次点击“确认清理”会删除当前浏览器中的 {_pendingMemoryClearIds.Count} 条记忆。切换搜索或重置会取消本次确认。";
            return;
        }

        var ids = _pendingMemoryClearIds.Distinct(StringComparer.Ordinal).ToList();
        if (ids.Count == 0)
        {
            ResetMemoryClearConfirmation();
            MemorySettingsStatus = "没有可清理的记忆条目。";
            return;
        }

        IsBusy = true;
        var deleted = 0;
        var failed = 0;

        try
        {
            foreach (var id in ids)
            {
                try
                {
                    if (await _memoryStore.DeleteAsync(id))
                    {
                        deleted++;
                    }
                    else
                    {
                        failed++;
                    }
                }
                catch
                {
                    failed++;
                }
            }

            await RefreshMemorySnapshotAsync();
            MemorySettingsStatus = failed == 0
                ? $"已清理 {deleted} 条记忆。"
                : $"已清理 {deleted} 条记忆，{failed} 条未能删除。";
        }
        finally
        {
            ResetMemoryClearConfirmation();
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task DeleteSelectedMemory()
    {
        ResetMemoryClearConfirmation();
        ResetMemoryEditMode();

        if (SelectedMemoryBrowserItem is null)
        {
            MemorySettingsStatus = "请先选择一条记忆。";
            return;
        }

        if (_memoryStore is null)
        {
            MemorySettingsStatus = "IMemoryStore 未注入，暂时无法删除记忆。";
            return;
        }

        IsBusy = true;

        try
        {
            var deleted = await _memoryStore.DeleteAsync(SelectedMemoryBrowserItem.Id);
            MemorySettingsStatus = deleted
                ? $"已删除记忆：{SelectedMemoryBrowserItem.Key}。"
                : $"没有找到记忆：{SelectedMemoryBrowserItem.Key}。";

            await RefreshMemorySnapshotAsync();
            if (!string.IsNullOrWhiteSpace(MemorySearchText) || SelectedMemoryFilter != "全部")
            {
                await SearchMemoryBrowser();
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task RefreshTaskHistoryStatus()
    {
        ResetTaskHistoryCleanupConfirmation();
        IsBusy = true;

        try
        {
            await RefreshTaskHistorySnapshotAsync();
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task SearchTaskHistoryBrowser()
    {
        ResetTaskHistoryCleanupConfirmation();

        if (_taskHistoryStore is null)
        {
            TaskHistorySettingsStatus = "ITaskHistoryStore 未注入，暂时无法搜索任务历史。";
            return;
        }

        IsBusy = true;

        try
        {
            var query = TaskHistorySearchText.Trim();
            var records = string.IsNullOrWhiteSpace(query)
                ? await _taskHistoryStore.GetRecentTasksAsync(120)
                : (await _taskHistoryStore.SearchTasksAsync(query, 120)).Records;
            var filtered = ApplyTaskHistoryStatusFilter(records, SelectedTaskHistoryStatusFilter)
                .OrderByDescending(item => item.CreatedAt)
                .Take(120)
                .ToList();

            PopulateTaskHistoryBrowserItems(filtered);
            TaskHistorySettingsStatus = filtered.Count == 0
                ? "没有找到匹配的任务历史。"
                : $"已匹配 {filtered.Count} 条任务历史。";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ResetTaskHistoryBrowser()
    {
        ResetTaskHistoryCleanupConfirmation();
        TaskHistorySearchText = string.Empty;
        SelectedTaskHistoryStatusFilter = "全部";

        IsBusy = true;

        try
        {
            await RefreshTaskHistorySnapshotAsync();
            TaskHistorySettingsStatus = "已恢复默认任务历史视图。";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ExportTaskHistory()
    {
        if (_taskHistoryStore is null)
        {
            TaskHistorySettingsStatus = "ITaskHistoryStore 未注入，暂时无法导出任务历史。";
            return;
        }

        IsBusy = true;

        try
        {
            var data = await _taskHistoryStore.ExportAsync();
            var exportDirectory = Path.Combine(GetLocalDataRoot(), "Exports");
            Directory.CreateDirectory(exportDirectory);
            var exportPath = Path.Combine(exportDirectory, $"task-history-{DateTime.Now:yyyyMMdd-HHmmss}.json");
            await File.WriteAllTextAsync(exportPath, data);
            TaskHistorySettingsStatus = $"已导出任务历史到 {exportPath}。";
            await RefreshTaskHistorySnapshotAsync();
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task CopySelectedTaskResult()
    {
        if (SelectedTaskHistoryBrowserItem is null)
        {
            TaskHistorySettingsStatus = "请先选择一条任务历史。";
            return;
        }

        if (_taskResultActionService is null)
        {
            TaskHistorySettingsStatus = "结果动作服务未注入，暂时无法复制。";
            return;
        }

        var copied = await _taskResultActionService.CopyToClipboardAsync(SelectedTaskHistoryBrowserItem.Result);
        TaskHistorySettingsStatus = copied
            ? $"已复制任务结果：{SelectedTaskHistoryBrowserItem.Title}。"
            : "复制失败：当前没有可用剪贴板或结果为空。";
    }

    [RelayCommand]
    private async Task CopySelectedTaskHistory()
    {
        if (SelectedTaskHistoryBrowserItem is null)
        {
            TaskHistorySettingsStatus = "请先选择一条任务历史。";
            return;
        }

        if (_taskResultActionService is null)
        {
            TaskHistorySettingsStatus = "结果动作服务未注入，暂时无法复制。";
            return;
        }

        var copied = await _taskResultActionService.CopyToClipboardAsync(CreateTaskHistoryExportText(SelectedTaskHistoryBrowserItem));
        TaskHistorySettingsStatus = copied
            ? $"已复制完整任务记录：{SelectedTaskHistoryBrowserItem.Title}。"
            : "复制失败：当前没有可用剪贴板。";
    }

    [RelayCommand]
    private async Task SaveSelectedTaskResult()
    {
        if (SelectedTaskHistoryBrowserItem is null)
        {
            TaskHistorySettingsStatus = "请先选择一条任务历史。";
            return;
        }

        if (_taskResultActionService is null)
        {
            TaskHistorySettingsStatus = "结果动作服务未注入，暂时无法保存任务结果。";
            return;
        }

        IsBusy = true;

        try
        {
            var exportPath = await _taskResultActionService.SaveResultAsync(
                SelectedTaskHistoryBrowserItem.Title,
                CreateTaskHistoryExportText(SelectedTaskHistoryBrowserItem));
            TaskHistorySettingsStatus = $"已保存任务结果到 {exportPath}。";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task DeleteSelectedTaskHistory()
    {
        ResetTaskHistoryCleanupConfirmation();

        if (SelectedTaskHistoryBrowserItem is null)
        {
            TaskHistorySettingsStatus = "请先选择一条任务历史。";
            return;
        }

        if (_taskHistoryStore is null)
        {
            TaskHistorySettingsStatus = "ITaskHistoryStore 未注入，暂时无法删除任务历史。";
            return;
        }

        IsBusy = true;

        try
        {
            var title = SelectedTaskHistoryBrowserItem.Title;
            var deleted = await _taskHistoryStore.DeleteTaskAsync(SelectedTaskHistoryBrowserItem.Id);
            TaskHistorySettingsStatus = deleted
                ? $"已删除任务历史：{title}。"
                : $"没有找到任务历史：{title}。";
            await RefreshTaskHistorySnapshotAsync();

            if (!string.IsNullOrWhiteSpace(TaskHistorySearchText) || SelectedTaskHistoryStatusFilter != "全部")
            {
                await SearchTaskHistoryBrowser();
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task CleanupOldTaskHistory()
    {
        if (_taskHistoryStore is null)
        {
            TaskHistorySettingsStatus = "ITaskHistoryStore 未注入，暂时无法清理任务历史。";
            return;
        }

        if (!_isTaskHistoryCleanupConfirmationPending)
        {
            _isTaskHistoryCleanupConfirmationPending = true;
            _selectedTaskHistoryCleanupFilter = SelectedTaskHistoryStatusFilter;
            TaskHistoryCleanupButtonText = "确认清理";
            TaskHistorySettingsStatus = "再次点击“确认清理”会删除 30 天以前的任务历史。切换筛选或重置会取消本次确认。";
            return;
        }

        IsBusy = true;

        try
        {
            var removed = await _taskHistoryStore.CleanupAsync(30);
            ResetTaskHistoryCleanupConfirmation();
            await RefreshTaskHistorySnapshotAsync();
            TaskHistorySettingsStatus = removed == 0
                ? "没有需要清理的 30 天以前任务历史。"
                : $"已清理 {removed} 条 30 天以前的任务历史。";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void EditSelectedMemory()
    {
        ResetMemoryClearConfirmation();

        if (SelectedMemoryBrowserItem is null)
        {
            MemorySettingsStatus = "请先选择一条记忆。";
            return;
        }

        MemoryEditDraftContent = SelectedMemoryBrowserItem.Content;
        MemoryEditDraftTags = SelectedMemoryBrowserItem.Tags == "无标签"
            ? string.Empty
            : SelectedMemoryBrowserItem.Tags;
        IsMemoryBrowserEditMode = true;
        MemorySettingsStatus = $"正在编辑记忆：{SelectedMemoryBrowserItem.Key}。";
    }

    [RelayCommand]
    private void CancelMemoryEdit()
    {
        ResetMemoryEditMode();
        MemorySettingsStatus = SelectedMemoryBrowserItem is null
            ? "已取消记忆编辑。"
            : $"已取消编辑：{SelectedMemoryBrowserItem.Key}。";
    }

    [RelayCommand]
    private async Task SaveEditedMemory()
    {
        ResetMemoryClearConfirmation();

        if (SelectedMemoryBrowserItem is null)
        {
            MemorySettingsStatus = "请先选择一条记忆。";
            return;
        }

        if (_memoryStore is null)
        {
            MemorySettingsStatus = "IMemoryStore 未注入，暂时无法编辑记忆。";
            return;
        }

        if (string.IsNullOrWhiteSpace(MemoryEditDraftContent))
        {
            MemorySettingsStatus = "记忆内容不能为空。";
            return;
        }

        var selectedId = SelectedMemoryBrowserItem.Id;
        var selectedKey = SelectedMemoryBrowserItem.Key;
        IsBusy = true;

        try
        {
            var entry = await _memoryStore.GetByIdAsync(selectedId);
            if (entry is null)
            {
                MemorySettingsStatus = $"没有找到记忆：{selectedKey}。";
                await RefreshMemorySnapshotAsync();
                return;
            }

            entry.Content = MemoryEditDraftContent.Trim();
            entry.Tags = ParseMemoryTags(MemoryEditDraftTags);
            entry.IsConfirmed = true;
            await _memoryStore.SaveAsync(entry);

            ResetMemoryEditMode();
            await RefreshMemorySnapshotAsync();
            if (!string.IsNullOrWhiteSpace(MemorySearchText) || SelectedMemoryFilter != "全部")
            {
                await SearchMemoryBrowser();
            }

            SelectedMemoryBrowserItem =
                MemoryBrowserItems.FirstOrDefault(item => item.Id == selectedId) ?? SelectedMemoryBrowserItem;
            MemorySettingsStatus = $"已保存记忆编辑：{selectedKey}。";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ConfirmPendingMemoryReview()
    {
        if (SelectedPendingMemoryReview is null)
        {
            MemorySettingsStatus = "请先选择一条待确认记忆。";
            return;
        }

        if (_memoryStore is null)
        {
            MemorySettingsStatus = "IMemoryStore 未注入，暂时无法确认记忆。";
            return;
        }

        IsBusy = true;

        try
        {
            var saved = await _memoryStore.ConfirmAsync(SelectedPendingMemoryReview.Id);
            MemorySettingsStatus = saved
                ? $"已确认并保存记忆：{SelectedPendingMemoryReview.Key}。"
                : $"没有找到待确认记忆：{SelectedPendingMemoryReview.Key}。";
            await RefreshMemorySnapshotAsync();
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void TempOnlyPendingMemoryReview()
    {
        if (SelectedPendingMemoryReview is null)
        {
            MemorySettingsStatus = "请先选择一条待确认记忆。";
            return;
        }

        MemorySettingsStatus =
            $"仅本次有效入口已预留：{SelectedPendingMemoryReview.Key}。接入队列数据后会返回 MemoryDecision.TempOnly。";
    }

    [RelayCommand]
    private void RejectPendingMemoryReview()
    {
        if (SelectedPendingMemoryReview is null)
        {
            MemorySettingsStatus = "请先选择一条待确认记忆。";
            return;
        }

        MemorySettingsStatus =
            $"拒绝记忆需要删除或归档语义，当前不直接移除：{SelectedPendingMemoryReview.Key}。后续接二次确认后开放。";
    }

    [RelayCommand]
    private async Task SaveEditedPendingMemoryReview()
    {
        if (SelectedPendingMemoryReview is null)
        {
            MemorySettingsStatus = "请先选择一条待确认记忆。";
            return;
        }

        if (string.IsNullOrWhiteSpace(PendingMemoryDraftContent))
        {
            MemorySettingsStatus = "编辑后的记忆内容不能为空。";
            return;
        }

        if (_memoryStore is null)
        {
            MemorySettingsStatus = "IMemoryStore 未注入，暂时无法保存编辑后的记忆。";
            return;
        }

        IsBusy = true;

        try
        {
            var entry = await _memoryStore.GetByIdAsync(SelectedPendingMemoryReview.Id);
            if (entry is null)
            {
                MemorySettingsStatus = $"没有找到待编辑记忆：{SelectedPendingMemoryReview.Key}。";
                return;
            }

            entry.Content = PendingMemoryDraftContent.Trim();
            entry.IsConfirmed = true;
            await _memoryStore.SaveAsync(entry);
            MemorySettingsStatus = $"已编辑并确认记忆：{entry.Key}。";
            await RefreshMemorySnapshotAsync();
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void SaveCharacter()
    {
        var profile = BuildCurrentCharacterProfile();
        ApplyCharacterProfile(profile, save: true);
        RefreshCharacterProfiles();
        CharacterSaveStatus = $"已保存 {CharacterName} 的角色外观和语气设定。";
    }

    [RelayCommand]
    private void PreviewCharacterStyle()
    {
        var traits = GetSelectedCharacterTraitText();
        var lengthHint = CharacterReplyLength switch
        {
            "短" => "我会直接给你最短可执行结论。",
            "详细" => "我会补齐背景、步骤、风险和验证方式。",
            _ => "我会先给结论，再列出必要步骤。"
        };
        var emojiHint = CharacterUseEmoji ? "需要时我会少量使用 emoji。" : "我会避免使用 emoji。";

        CharacterStylePreview =
            $"{CharacterName}：收到。我会用{CharacterToneStyle}、{CharacterLanguageStyle}的方式回应你，保持{traits}的性格。{lengthHint}{emojiHint}";
        CharacterSaveStatus = "已生成当前角色语气预览。";
    }

    [RelayCommand]
    private void SaveCharacterProfile()
    {
        var entry = _characterStore.SaveProfile(BuildCurrentCharacterProfile());
        CharacterProfileNameDraft = entry.Name;
        RefreshCharacterProfiles(entry.Id);
        CharacterLibraryStatus = $"已保存角色档案：{entry.Name}。";
    }

    [RelayCommand]
    private void SaveCharacterProfileAs()
    {
        var profileName = CleanText(CharacterProfileNameDraft, CharacterName, 24);
        var entry = _characterStore.SaveProfileAs(BuildCurrentCharacterProfile(), profileName);
        CharacterProfileNameDraft = entry.Name;
        RefreshCharacterProfiles(entry.Id);
        CharacterLibraryStatus = $"已另存角色档案：{entry.Name}。";
    }

    [RelayCommand]
    private void DuplicateSelectedCharacterProfile()
    {
        if (SelectedCharacterProfile is null)
        {
            CharacterLibraryStatus = "请先选择一个角色档案。";
            return;
        }

        var profile = _characterStore.LoadProfile(SelectedCharacterProfile.Id);
        if (profile is null)
        {
            CharacterLibraryStatus = "所选角色档案不可用。";
            RefreshCharacterProfiles();
            return;
        }

        var duplicateName = CreateUniqueDuplicateProfileName(profile.Name);
        var entry = _characterStore.SaveProfileAs(profile, duplicateName);
        CharacterProfileNameDraft = entry.Name;
        RefreshCharacterProfiles(entry.Id);
        CharacterLibraryStatus = $"已复制角色档案：{entry.Name}。";
    }

    [RelayCommand]
    private void RenameSelectedCharacterProfile()
    {
        if (SelectedCharacterProfile is null)
        {
            CharacterLibraryStatus = "请先选择一个角色档案。";
            return;
        }

        var nextName = CleanText(CharacterProfileNameDraft, SelectedCharacterProfile.Name, 24);
        if (string.IsNullOrWhiteSpace(nextName))
        {
            CharacterLibraryStatus = "角色档案名称不能为空。";
            return;
        }

        var oldId = SelectedCharacterProfile.Id;
        var wasActive = SelectedCharacterProfile.IsActive;
        var profile = _characterStore.LoadProfile(oldId);
        if (profile is null)
        {
            CharacterLibraryStatus = "所选角色档案不可用。";
            RefreshCharacterProfiles();
            return;
        }

        var entry = _characterStore.SaveProfileAs(profile, nextName);
        if (!string.Equals(entry.Id, oldId, StringComparison.OrdinalIgnoreCase))
        {
            _characterStore.DeleteProfile(oldId);
        }

        if (wasActive)
        {
            profile.Name = entry.Name;
            ApplyCharacterProfile(profile, save: true);
        }

        CharacterProfileNameDraft = entry.Name;
        RefreshCharacterProfiles(entry.Id);
        CharacterLibraryStatus = $"已重命名角色档案：{entry.Name}。";
    }

    [RelayCommand]
    private void LoadSelectedCharacterProfile()
    {
        if (SelectedCharacterProfile is null)
        {
            CharacterLibraryStatus = "请先选择一个角色档案。";
            return;
        }

        var profile = _characterStore.LoadProfile(SelectedCharacterProfile.Id);
        if (profile is null)
        {
            CharacterLibraryStatus = "所选角色档案不可用。";
            RefreshCharacterProfiles();
            return;
        }

        ApplyCharacterProfile(profile, save: true);
        RefreshCharacterProfiles(SelectedCharacterProfile.Id);
        CharacterLibraryStatus = $"已载入角色档案：{CharacterName}。";
        CharacterSaveStatus = "当前小人外观已切换。";
    }

    [RelayCommand]
    private void DeleteSelectedCharacterProfile()
    {
        if (SelectedCharacterProfile is null)
        {
            CharacterLibraryStatus = "请先选择一个角色档案。";
            return;
        }

        var name = SelectedCharacterProfile.Name;
        var deleted = _characterStore.DeleteProfile(SelectedCharacterProfile.Id);
        RefreshCharacterProfiles();
        CharacterLibraryStatus = deleted
            ? $"已删除角色档案：{name}。"
            : "所选角色档案删除失败或已不存在。";
    }

    [RelayCommand]
    private void RefreshCharacterProfileLibrary()
    {
        RefreshCharacterProfiles();
        CharacterLibraryStatus = "已刷新角色库。";
    }

    [RelayCommand]
    private void ResetCharacter()
    {
        ApplyCharacterProfile(new MascotCharacterProfile(), save: true);
        CharacterSaveStatus = "已恢复默认角色外观。";
    }

    [RelayCommand]
    private void UseCharacterPreset(string? presetId)
    {
        var profile = presetId switch
        {
            "developer" => new MascotCharacterProfile
            {
                Name = "码伴",
                Role = "开发调试伙伴",
                AvatarText = "</>",
                Personality = "直接严谨",
                Catchphrase = "我会优先帮你定位问题和验证结果。",
                AccentColor = "#0F766E",
                BackgroundColor = "#F0FDFA",
                ImageFolder = "assets/characters/developer"
            },
            "operator" => new MascotCharacterProfile
            {
                Name = "桌管家",
                Role = "桌面任务管家",
                AvatarText = "管",
                Personality = "有序高效",
                Catchphrase = "我会把任务拆清楚，再一步步执行。",
                AccentColor = "#7C2D12",
                BackgroundColor = "#FFF7ED",
                ImageFolder = "assets/characters/operator"
            },
            "study" => new MascotCharacterProfile
            {
                Name = "小研",
                Role = "阅读研究助手",
                AvatarText = "研",
                Personality = "耐心清晰",
                Catchphrase = "我会帮你提炼重点、整理脉络。",
                AccentColor = "#7C3AED",
                BackgroundColor = "#F5F3FF",
                ImageFolder = "assets/characters/study"
            },
            _ => new MascotCharacterProfile()
        };

        ApplyCharacterProfile(profile, save: false);
        CharacterSaveStatus = $"已切换到 {CharacterName}，点击保存后生效。";
    }

    [RelayCommand]
    private void UseDesktopCharacterImages()
    {
        CharacterImageFolder = @"C:\Users\wgmo\Desktop\人物图片";
        CharacterAvatarImage = "站立.png";
        SetStateImage("Idle", "站立.png");
        SetStateImage("Listening", "Listening：看向用户.png");
        SetStateImage("Understanding", "Understanding：思考图.png");
        SetStateImage("ReadingContext", "站立.png");
        SetStateImage("Planning", "Understanding：思考图.png");
        SetStateImage("WaitingApproval", "WaitingApproval：提醒、举手图.png");
        SetStateImage("Working", "Working：忙碌图.png");
        SetStateImage("MemoryConfirm", "站立.png");
        SetStateImage("Reporting", "pose.png");
        SetStateImage("Completed", "Completed：开心、完成图.png");
        SetStateImage("Error", "Error：困惑、错误图.png");
        RefreshCharacterImagePreview();
        RefreshCharacterAssetSuggestions();
        CharacterSaveStatus = "已填入桌面人物图片目录和状态图映射，点击保存后生效。";
    }

    [RelayCommand]
    private void ChooseCharacterAccentColor(string? color)
    {
        if (string.IsNullOrWhiteSpace(color))
            return;

        CharacterAccentColor = color;
        CharacterBackgroundColor = color switch
        {
            "#0F766E" => "#F0FDFA",
            "#7C2D12" => "#FFF7ED",
            "#7C3AED" => "#F5F3FF",
            "#C026D3" => "#FDF4FF",
            "#DC2626" => "#FEF2F2",
            "#2563EB" => "#EEF6FF",
            _ => "#F9FAFB"
        };

        RefreshCharacterBrushes();
    }

    [RelayCommand]
    private void RefreshCharacterPreview()
    {
        RefreshCharacterImagePreview();
        CharacterSaveStatus = "已刷新角色图片预览。";
    }

    [RelayCommand]
    private void RefreshCharacterStatePreview()
    {
        RefreshCharacterImagePreview();
        CharacterStatePreviewStatus = "已检查全部状态图。";
    }

    [RelayCommand]
    private void RefreshCharacterManifestPreview()
    {
        RefreshCharacterManifestPreviewCore("已刷新寻研角色包清单预览。");
    }

    [RelayCommand]
    private async Task ExportCharacterManifest()
    {
        try
        {
            var manifest = BuildCurrentCharacterManifest();
            var json = MascotCharacterManifestFactory.ToJson(manifest);
            var exportDirectory = Path.Combine(GetLocalDataRoot(), "Exports", "Characters", manifest.Slug);
            Directory.CreateDirectory(exportDirectory);

            var exportPath = Path.Combine(exportDirectory, "character.manifest.json");
            await File.WriteAllTextAsync(exportPath, json);

            CharacterManifestPreview = json;
            RefreshCharacterManifestFormatItems(manifest);
            CharacterManifestStatus = $"已导出寻研角色包清单：{exportPath}";
        }
        catch (Exception ex)
        {
            CharacterManifestStatus = $"导出失败：{ex.Message}";
        }
    }

    [RelayCommand]
    private void ScanCharacterAssetSuggestions()
    {
        RefreshCharacterAssetSuggestions();
    }

    [RelayCommand]
    private void ApplyCharacterAssetSuggestions()
    {
        if (CharacterAssetSuggestions.Count == 0)
        {
            RefreshCharacterAssetSuggestions();
        }

        var appliedCount = 0;
        foreach (var suggestion in CharacterAssetSuggestions.Where(x => x.HasSuggestion && !x.IsAlreadyApplied))
        {
            SetStateImage(suggestion.StateKey, suggestion.SuggestedFileName);
            appliedCount++;
        }

        var appliedAvatar = false;
        if (!string.IsNullOrWhiteSpace(CharacterAvatarImageSuggestion) &&
            !string.Equals(CharacterAvatarImage, CharacterAvatarImageSuggestion, StringComparison.OrdinalIgnoreCase))
        {
            CharacterAvatarImage = CharacterAvatarImageSuggestion;
            appliedAvatar = true;
        }

        RefreshCharacterImagePreview();
        RefreshCharacterAssetSuggestions();
        CharacterSaveStatus = appliedCount == 0 && !appliedAvatar
            ? "没有可应用的新图片匹配。"
            : $"已应用 {appliedCount} 个状态图匹配{(appliedAvatar ? "，并更新头像图片" : string.Empty)}，点击保存后生效。";
    }

    public static bool IsSupportedCharacterImageFile(string? filePath)
    {
        return !string.IsNullOrWhiteSpace(filePath) &&
               File.Exists(filePath) &&
               SupportedCharacterImageExtensions.Contains(Path.GetExtension(filePath));
    }

    public void ApplyDroppedCharacterImageFiles(IEnumerable<string>? filePaths)
    {
        var files = NormalizeDroppedCharacterImageFiles(filePaths);
        if (files.Count == 0)
        {
            CharacterSaveStatus = "拖入的文件里没有可用图片。支持 png、jpg、jpeg、bmp、webp。";
            return;
        }

        if (files.Count == 1)
        {
            var file = files[0];
            var selectedState = SelectedCharacterStateImage;
            if (selectedState is null)
            {
                ApplyPickedImageFile(file, fileName => CharacterAvatarImage = fileName);
                CharacterSaveStatus = "已拖入图片并应用为头像，点击保存后生效。";
            }
            else
            {
                ApplyPickedImageFile(file, fileName => selectedState.FileName = fileName);
                CharacterSaveStatus = $"已拖入图片并应用到 {selectedState.DisplayName}，点击保存后生效。";
            }

            RefreshCharacterAssetSuggestions();
            return;
        }

        var directories = files
            .Select(Path.GetDirectoryName)
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (directories.Length == 1)
        {
            CharacterImageFolder = directories[0]!;
            RefreshCharacterImagePreview();
            RefreshCharacterAssetSuggestions();
            CharacterSaveStatus = $"已接收 {files.Count} 张角色图片，已切换到拖入目录并生成匹配建议。";
            return;
        }

        CharacterSaveStatus = $"已接收 {files.Count} 张图片，但来自多个目录。请先放到同一目录后再拖入。";
    }

    [RelayCommand]
    private async Task PickCharacterImageFolder()
    {
        var folder = await _characterAssetPickerService.PickImageFolderAsync();
        if (string.IsNullOrWhiteSpace(folder))
            return;

        CharacterImageFolder = folder;
        RefreshCharacterImagePreview();
        RefreshCharacterAssetSuggestions();
        CharacterSaveStatus = "已选择角色图片目录，点击保存后生效。";
    }

    [RelayCommand]
    private async Task PickCharacterAvatarImage()
    {
        var filePath = await _characterAssetPickerService.PickImageFileAsync();
        if (string.IsNullOrWhiteSpace(filePath))
            return;

        ApplyPickedImageFile(filePath, fileName => CharacterAvatarImage = fileName);
        CharacterSaveStatus = "已选择头像图片，点击保存后生效。";
    }

    [RelayCommand]
    private async Task PickSelectedStateImage()
    {
        if (SelectedCharacterStateImage is null)
        {
            CharacterSaveStatus = "请先选择一个状态。";
            return;
        }

        var filePath = await _characterAssetPickerService.PickImageFileAsync();
        if (string.IsNullOrWhiteSpace(filePath))
            return;

        ApplyPickedImageFile(filePath, fileName => SelectedCharacterStateImage.FileName = fileName);
        CharacterSaveStatus = $"已为 {SelectedCharacterStateImage.DisplayName} 选择图片，点击保存后生效。";
    }

    [RelayCommand]
    private void ImportCharacterAssets()
    {
        var result = _characterAssetImportService.ImportToAppData(BuildCurrentCharacterProfile());
        if (!result.Success || result.Profile is null)
        {
            CharacterSaveStatus = result.Message;
            return;
        }

        ApplyCharacterProfile(result.Profile, save: true);
        CharacterSaveStatus = result.Message;
        CharacterAssetWarningText = result.FallbackCount > 0
            ? $"{result.Message} 缺少专用图片的状态已使用头像回退。"
            : "导入完成，当前角色图片已改为应用资源目录。";
    }

    partial void OnSelectedProviderChanged(ModelProviderOption? value)
    {
        if (value is null || _isApplyingProvider)
            return;

        ApplyProviderDefaults(overwriteExisting: true);
        ClearModelConnectionResult();
        ModelSettingsStatus = $"已切换到 {value.DisplayName}，并填入默认端点和模型。";
    }

    partial void OnApiKeyChanged(string value)
    {
        ClearModelConnectionResult();
    }

    partial void OnApiEndpointChanged(string value)
    {
        ClearModelConnectionResult();
    }

    partial void OnModelNameChanged(string value)
    {
        ClearModelConnectionResult();
    }

    partial void OnTtsVoiceChanged(string value)
    {
        VoiceSettingsStatus = $"语音配置已变更，尚未保存：{VoiceConfigurationSummary}。";
        RefreshRuntimeOverviewCards();
    }

    partial void OnSpeechRecognitionLanguageChanged(string value)
    {
        VoiceSettingsStatus = $"识别语言已变更，尚未保存：{SpeechRecognitionLanguageDescription}";
        RefreshRuntimeOverviewCards();
    }

    partial void OnSelectedPendingMemoryReviewChanged(PendingMemoryReviewItem? value)
    {
        PendingMemoryDraftContent = value?.Content ?? string.Empty;
    }

    partial void OnSelectedMemoryBrowserItemChanged(MemoryBrowserItem? value)
    {
        ResetMemoryEditMode();

        if (value is null)
            return;

        MemorySettingsStatus = $"已选中记忆：{value.Key}。";
    }

    partial void OnMemorySearchTextChanged(string value)
    {
        ResetMemoryClearConfirmation();
        ResetMemoryEditMode();
    }

    partial void OnSelectedMemoryFilterChanged(string value)
    {
        ResetMemoryClearConfirmation();
        ResetMemoryEditMode();
    }

    partial void OnSelectedTaskHistoryBrowserItemChanged(TaskHistoryBrowserItem? value)
    {
        PopulateSelectedTaskHistoryDetails();

        if (value is not null)
        {
            TaskHistorySettingsStatus = $"已选中任务历史：{value.Title}。";
        }
    }

    partial void OnTaskHistorySearchTextChanged(string value)
    {
        ResetTaskHistoryCleanupConfirmation();
    }

    partial void OnSelectedTaskHistoryStatusFilterChanged(string value)
    {
        if (_isTaskHistoryCleanupConfirmationPending &&
            !string.Equals(value, _selectedTaskHistoryCleanupFilter, StringComparison.Ordinal))
        {
            ResetTaskHistoryCleanupConfirmation();
        }
    }

    partial void OnSelectedCharacterProfileChanged(CharacterProfileListItem? value)
    {
        CharacterProfileNameDraft = value?.Name ?? CharacterName;
    }

    private void ApplyProviderDefaultsIfEmpty()
    {
        ApplyProviderDefaults(overwriteExisting: false);
    }

    private void ApplyProviderDefaults(bool overwriteExisting)
    {
        if (SelectedProvider is null)
            return;

        _isApplyingProvider = true;

        try
        {
            if (overwriteExisting || string.IsNullOrWhiteSpace(ApiEndpoint))
            {
                ApiEndpoint = SelectedProvider.DefaultEndpoint;
            }

            if (overwriteExisting || string.IsNullOrWhiteSpace(ModelName))
            {
                ModelName = SelectedProvider.DefaultModel;
            }
        }
        finally
        {
            _isApplyingProvider = false;
        }
    }

    private void SetModelConnectionResult(bool success, string title, string detail, string meta)
    {
        IsModelConnectionSuccessful = success;
        ModelConnectionResultTitle = CleanText(title, success ? "连接测试通过" : "连接测试失败", 80);
        ModelConnectionResultDetail = CleanText(detail, "没有返回详细信息。", 500);
        ModelConnectionResultMeta = CleanText(meta, "测试连接", 120);
        HasModelConnectionResult = true;
        RefreshRuntimeOverviewCards();
    }

    private void ClearModelConnectionResult()
    {
        HasModelConnectionResult = false;
        IsModelConnectionSuccessful = false;
        ModelConnectionResultTitle = "尚未测试";
        ModelConnectionResultDetail = "当前配置变更后需要重新测试连接。";
        ModelConnectionResultMeta = "等待测试";
        RefreshRuntimeOverviewCards();
    }

    private ModelProviderOption? InferProvider(string endpoint)
    {
        if (string.IsNullOrWhiteSpace(endpoint))
            return null;

        return Providers.FirstOrDefault(x =>
            !string.IsNullOrWhiteSpace(x.DefaultEndpoint) &&
            endpoint.Contains(new Uri(x.DefaultEndpoint).Host, StringComparison.OrdinalIgnoreCase));
    }

    private void RefreshMimoCodeCards()
    {
        MimoCodeReadinessItems.Clear();
        MimoCodeReadinessItems.Add(new SettingsListItem(
            "接入状态",
            IsMimoCodeEnabled ? "已启用" : "未启用",
            "开启后寻研可以把代码任务交给本机 Mimo Code connector。"));
        MimoCodeReadinessItems.Add(new SettingsListItem(
            "模型 API",
            GetMimoCodeModelModeText(MimoCodeModelConfigMode),
            "不会随应用内置模型 Key，用户必须在本机配置自己的 Provider/API Key。"));
        MimoCodeReadinessItems.Add(new SettingsListItem(
            "开源协议",
            "MIT",
            "集成或分发时需要保留 Mimo Code 的版权声明和许可证文本。"));
        MimoCodeReadinessItems.Add(new SettingsListItem(
            "权限确认",
            "走寻研确认体系",
            "文件写入、命令执行和记忆保存仍通过当前权限/记忆弹窗确认。"));
        MimoCodeReadinessItems.Add(new SettingsListItem(
            "后续对接",
            "等待 connector",
            "后续只需要把启动、任务输入、事件流和结果返回挂到这些配置上。"));
        RefreshRuntimeOverviewCards();
    }

    private static string NormalizeMimoCodeModelMode(string value)
    {
        return value == "MimoLocalConfig" ? "MimoLocalConfig" : "AppProvider";
    }

    private static string GetMimoCodeModelModeText(string value)
    {
        return NormalizeMimoCodeModelMode(value) == "MimoLocalConfig"
            ? "使用 Mimo Code 本机配置"
            : "使用寻研 Provider/API Key";
    }

    private void RefreshHotkeyCards()
    {
        HotkeyItems.Clear();

        HotkeyItems.Add(new SettingsListItem(
            "聊天唤起",
            $"{_hotkeyService.DisplayText} / {GetHotkeyRegistrationText(_hotkeyService.IsDefaultHotkeyRegistered)}",
            _hotkeyService.IsDefaultHotkeyRegistered
                ? "在桌面任意位置唤起小人并打开输入面板。"
                : "未能注册，通常是组合键被其他软件占用，或当前平台不支持全局热键。"));

        HotkeyItems.Add(new SettingsListItem(
            "屏幕圈选",
            $"{_hotkeyService.ScreenSelectionDisplayText} / {GetHotkeyRegistrationText(_hotkeyService.IsScreenSelectionHotkeyRegistered)}",
            _hotkeyService.IsScreenSelectionHotkeyRegistered
                ? "在桌面任意位置进入屏幕区域圈选，并把区域交给屏幕理解任务。"
                : "未能注册，可能与系统或截图工具热键冲突。"));

        HotkeyItems.Add(new SettingsListItem(
            "冲突提示",
            "保存前检查",
            "保存时会检查格式、重复组合键和 Windows 注册冲突；失败时会保留上一次可用配置。"));

        HotkeySettingsStatus = GetHotkeyStatusText();
        RefreshRuntimeOverviewCards();
    }

    private void LoadHotkeyTextFromService()
    {
        ChatHotkeyText = _hotkeyService.DisplayText;
        ScreenSelectionHotkeyText = _hotkeyService.ScreenSelectionDisplayText;
    }

    private string GetHotkeyStatusText()
    {
        if (_hotkeyService.IsDefaultHotkeyRegistered && _hotkeyService.IsScreenSelectionHotkeyRegistered)
        {
            return "快捷键已注册，可在桌面直接使用。";
        }

        if (_hotkeyService.IsDefaultHotkeyRegistered || _hotkeyService.IsScreenSelectionHotkeyRegistered)
        {
            return "部分快捷键未注册，请检查是否被其他软件占用。";
        }

        return "快捷键未注册，可能是非 Windows 环境、权限限制或组合键冲突。";
    }

    private static string GetHotkeyRegistrationText(bool isRegistered)
    {
        return isRegistered ? "已注册" : "未注册";
    }

    private void RefreshDataDirectoryItems()
    {
        DataDirectoryItems.Clear();

        var coreRoot = GetCoreDataRoot();
        var uiRoot = GetUiDataRoot();
        var localRoot = GetLocalDataRoot();

        var directories = new[]
        {
            CreateDataDirectoryItem(
                "核心数据",
                coreRoot,
                "App/Core/Agent 层配置、日志、记忆和任务历史的根目录。"),
            CreateDataDirectoryItem(
                "模型与权限配置",
                Path.Combine(coreRoot, "config"),
                "保存 Provider、API Key、权限策略和用户偏好。"),
            CreateDataDirectoryItem(
                "日志与审计",
                Path.Combine(coreRoot, "logs"),
                "应用日志和权限审计日志默认写入这里。"),
            CreateDataDirectoryItem(
                "任务历史",
                Path.Combine(coreRoot, "db"),
                "任务历史、工具调用记录等持久化数据。"),
            CreateDataDirectoryItem(
                "记忆数据",
                Path.Combine(coreRoot, "memory"),
                "长期记忆和记忆确认后的本地存储。"),
            CreateDataDirectoryItem(
                "UI 配置",
                Path.Combine(uiRoot, "config"),
                "窗口位置、快捷键和角色档案等 UI 层配置。"),
            CreateDataDirectoryItem(
                "角色资源",
                Path.Combine(uiRoot, "assets", "characters"),
                "导入后的稳定角色图片资源目录。"),
            CreateDataDirectoryItem(
                "任务结果",
                Path.Combine(localRoot, "TaskResults"),
                "复制/保存任务结果时生成的 Markdown 文件。"),
            CreateDataDirectoryItem(
                "本地缓存",
                GetCacheDirectory(),
                "可清理的临时缓存目录；不会删除配置、记忆或日志。")
        };

        foreach (var item in directories)
        {
            DataDirectoryItems.Add(item);
        }

        var totalBytes = directories.Sum(item => TryGetDirectorySize(item.Path));
        DataStorageSummary = $"已跟踪 {directories.Length} 个目录，合计约 {FormatBytes(totalBytes)}。";
        DataSettingsStatus = "数据页已加载。清理操作只会处理本地缓存目录。";
        RefreshRuntimeOverviewCards();
    }

    private DataDirectoryItem CreateDataDirectoryItem(string title, string path, string description)
    {
        Directory.CreateDirectory(path);
        return new DataDirectoryItem(
            title,
            path,
            description,
            GetDirectoryStatus(path),
            item => OpenDirectory(item.Path, item.Title));
    }

    private void OpenDirectory(string path, string label)
    {
        try
        {
            Directory.CreateDirectory(path);
            Process.Start(new ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true
            });
            DataSettingsStatus = $"已打开{label}：{path}";
        }
        catch (Exception ex)
        {
            DataSettingsStatus = $"打开{label}失败：{ex.Message}";
        }
    }

    private static string GetDirectoryStatus(string path)
    {
        if (!Directory.Exists(path))
            return "目录不存在";

        try
        {
            var fileCount = Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories).Count();
            var size = TryGetDirectorySize(path);
            return $"{fileCount} 个文件 / {FormatBytes(size)}";
        }
        catch
        {
            return "目录可用，统计受限";
        }
    }

    private static long TryGetDirectorySize(string path)
    {
        try
        {
            if (!Directory.Exists(path))
                return 0;

            return Directory
                .EnumerateFiles(path, "*", SearchOption.AllDirectories)
                .Sum(file =>
                {
                    try
                    {
                        return new FileInfo(file).Length;
                    }
                    catch
                    {
                        return 0;
                    }
                });
        }
        catch
        {
            return 0;
        }
    }

    private static string FormatBytes(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB"];
        var value = Math.Max(0, bytes);
        var unitIndex = 0;
        var display = (double)value;
        while (display >= 1024 && unitIndex < units.Length - 1)
        {
            display /= 1024;
            unitIndex++;
        }

        return unitIndex == 0
            ? $"{value} {units[unitIndex]}"
            : $"{display:0.##} {units[unitIndex]}";
    }

    private static string GetCoreDataRoot() => Path.Combine(GetRoamingAppDataRoot(), "DesktopMascot");

    private static string GetUiDataRoot() => Path.Combine(GetRoamingAppDataRoot(), "DesktopAIMascot");

    private static string GetLocalDataRoot() => Path.Combine(GetLocalAppDataRoot(), "DesktopMascot");

    private static string GetCacheDirectory() => Path.Combine(GetLocalDataRoot(), "Cache");

    private static string GetRoamingAppDataRoot()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return string.IsNullOrWhiteSpace(appData)
            ? Environment.CurrentDirectory
            : appData;
    }

    private static string GetLocalAppDataRoot()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return string.IsNullOrWhiteSpace(localAppData)
            ? GetRoamingAppDataRoot()
            : localAppData;
    }

    private async Task RefreshPermissionSnapshotAsync(CancellationToken ct = default)
    {
        var logs = new List<AuditLogEntry>();

        if (_permissionManager is not null)
        {
            logs.AddRange(await _permissionManager.GetAuditLogsAsync(50, ct));
        }

        if (_auditLogStore is not null)
        {
            logs.AddRange(await _auditLogStore.GetLogsAsync(50, ct));
            _auditLogTotalCount = await _auditLogStore.GetTotalCountAsync(ct);
        }

        _recentAuditLogs = logs
            .GroupBy(CreateAuditLogKey)
            .Select(group => group.OrderByDescending(item => item.Timestamp).First())
            .OrderByDescending(item => item.Timestamp)
            .Take(8)
            .ToList();

        if (_auditLogTotalCount < _recentAuditLogs.Count)
        {
            _auditLogTotalCount = _recentAuditLogs.Count;
        }

        PermissionSettingsStatus = _permissionManager is null && _auditLogStore is null
            ? "权限服务未注入，当前只能保存本地策略配置。"
            : _recentAuditLogs.Count == 0
                ? "权限服务已连接，当前暂无审计记录。"
                : $"已读取 {_recentAuditLogs.Count} 条最近审计记录；总记录约 {_auditLogTotalCount} 条。";
        RefreshPermissionCards();
    }

    private async Task RefreshMemorySnapshotAsync(CancellationToken ct = default)
    {
        if (_memoryStore is null)
        {
            _memoryStatistics = null;
            _recentMemoryEntries = [];
            _memoryBrowserEntries = [];
            PendingMemoryReviews.Clear();
            MemoryBrowserItems.Clear();
            MemorySettingsStatus = "IMemoryStore 未注入，当前只能保存记忆开关。";
            RefreshMemoryCards();
            RefreshPendingMemoryReviewState();
            RefreshMemoryBrowserState();
            return;
        }

        _memoryStatistics = await _memoryStore.GetStatisticsAsync(ct);
        var result = await _memoryStore.SearchAsync(string.Empty, null, 200, ct);
        _memoryBrowserEntries = result.Entries
            .OrderByDescending(item => item.UpdatedAt)
            .ToList();
        _recentMemoryEntries = result.Entries
            .OrderByDescending(item => item.UpdatedAt)
            .Take(10)
            .ToList();

        PopulateMemoryBrowserItems(_memoryBrowserEntries.Take(40));
        PopulatePendingMemoryReviews(result.Entries
            .Where(item => !item.IsConfirmed)
            .OrderByDescending(item => item.UpdatedAt)
            .Take(20));

        MemorySettingsStatus = _memoryStatistics.TotalCount == 0
            ? "记忆存储已连接，当前还没有长期记忆。"
            : $"已读取 {_memoryStatistics.TotalCount} 条记忆，待确认 {_memoryStatistics.UnconfirmedCount} 条。";
        RefreshMemoryCards();
        RefreshPendingMemoryReviewState();
        RefreshMemoryBrowserState();
    }

    private void PopulateMemoryBrowserItems(IEnumerable<MemoryEntry> entries)
    {
        var selectedId = SelectedMemoryBrowserItem?.Id;
        MemoryBrowserItems.Clear();

        foreach (var entry in entries)
        {
            MemoryBrowserItems.Add(CreateMemoryBrowserItem(entry));
        }

        SelectedMemoryBrowserItem = !string.IsNullOrWhiteSpace(selectedId)
            ? MemoryBrowserItems.FirstOrDefault(item => item.Id == selectedId)
            : MemoryBrowserItems.FirstOrDefault();
    }

    private void PopulatePendingMemoryReviews(IEnumerable<MemoryEntry> entries)
    {
        var selectedId = SelectedPendingMemoryReview?.Id;
        PendingMemoryReviews.Clear();

        foreach (var entry in entries)
        {
            PendingMemoryReviews.Add(CreatePendingMemoryReviewItem(entry));
        }

        SelectedPendingMemoryReview = !string.IsNullOrWhiteSpace(selectedId)
            ? PendingMemoryReviews.FirstOrDefault(item => item.Id == selectedId)
            : PendingMemoryReviews.FirstOrDefault();
    }

    private static PendingMemoryReviewItem CreatePendingMemoryReviewItem(MemoryEntry entry)
    {
        var tags = entry.Tags.Count == 0
            ? "无标签"
            : string.Join("、", entry.Tags.Select(item => $"{item.Key}:{item.Value}"));

        return new PendingMemoryReviewItem(
            entry.Id,
            GetMemoryTypeText(entry.Type),
            entry.Key,
            string.IsNullOrWhiteSpace(entry.Source) ? "IMemoryStore" : entry.Source,
            "来自记忆存储的未确认条目。",
            FormatTime(entry.CreatedAt),
            entry.IsConfirmed ? "已确认" : "待确认",
            entry.Content,
            tags,
            entry.ExpiresAt.HasValue ? FormatTime(entry.ExpiresAt.Value) : "长期");
    }

    private static MemoryBrowserItem CreateMemoryBrowserItem(MemoryEntry entry)
    {
        var tags = entry.Tags.Count == 0
            ? "无标签"
            : string.Join("、", entry.Tags.Select(item => $"{item.Key}:{item.Value}"));

        return new MemoryBrowserItem(
            entry.Id,
            GetMemoryTypeText(entry.Type),
            entry.Key,
            string.IsNullOrWhiteSpace(entry.Source) ? "IMemoryStore" : entry.Source,
            entry.IsConfirmed ? "已确认" : "待确认",
            string.IsNullOrWhiteSpace(entry.Content) ? "无内容" : entry.Content.Trim(),
            tags,
            FormatTime(entry.UpdatedAt),
            entry.ExpiresAt.HasValue ? FormatTime(entry.ExpiresAt.Value) : "长期");
    }

    private static MemoryType? ResolveMemoryFilterType(string? filter) => filter switch
    {
        "用户偏好" => MemoryType.User,
        "项目信息" => MemoryType.Project,
        "技能流程" => MemoryType.Skill,
        "任务历史" => MemoryType.History,
        _ => null
    };

    private static Dictionary<string, string> ParseMemoryTags(string? value)
    {
        var tags = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(value))
            return tags;

        var parts = value.Split(['、', ',', '，', ';', '；', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        foreach (var part in parts)
        {
            var text = part.Trim();
            if (string.IsNullOrWhiteSpace(text))
                continue;

            var separatorIndex = text.IndexOf(':');
            if (separatorIndex < 0)
            {
                separatorIndex = text.IndexOf('：');
            }

            var key = separatorIndex > 0 ? text[..separatorIndex].Trim() : text;
            var tagValue = separatorIndex > 0 ? text[(separatorIndex + 1)..].Trim() : "true";
            if (string.IsNullOrWhiteSpace(key))
                continue;

            tags[key] = string.IsNullOrWhiteSpace(tagValue) ? "true" : tagValue;
        }

        return tags;
    }

    private async Task RefreshTaskHistorySnapshotAsync(CancellationToken ct = default)
    {
        if (_taskHistoryStore is null)
        {
            _taskHistoryStatistics = null;
            _taskHistoryBrowserRecords = [];
            TaskHistoryStatsItems.Clear();
            TaskHistoryBrowserItems.Clear();
            TaskHistoryEventItems.Clear();
            TaskHistoryToolCallItems.Clear();
            TaskHistorySettingsStatus = "ITaskHistoryStore 未注入，当前无法读取任务历史。";
            RefreshTaskHistoryCards();
            RefreshTaskHistoryBrowserState();
            RefreshTaskHistoryDetailState();
            return;
        }

        _taskHistoryStatistics = await _taskHistoryStore.GetStatisticsAsync(ct);
        var records = await _taskHistoryStore.GetRecentTasksAsync(120, ct);
        var ordered = records
            .OrderByDescending(item => item.CreatedAt)
            .Take(80)
            .ToList();

        PopulateTaskHistoryBrowserItems(ordered);
        TaskHistorySettingsStatus = _taskHistoryStatistics.TotalTasks == 0
            ? "任务历史存储已连接，当前还没有任务记录。"
            : $"已读取 {_taskHistoryStatistics.TotalTasks} 条任务历史，当前显示 {TaskHistoryBrowserItems.Count} 条。";
        RefreshTaskHistoryCards();
        RefreshTaskHistoryBrowserState();
        RefreshTaskHistoryDetailState();
    }

    private void PopulateTaskHistoryBrowserItems(IEnumerable<TaskHistoryRecord> records)
    {
        var selectedId = SelectedTaskHistoryBrowserItem?.Id;
        _taskHistoryBrowserRecords = records.ToList();
        TaskHistoryBrowserItems.Clear();

        foreach (var record in _taskHistoryBrowserRecords)
        {
            TaskHistoryBrowserItems.Add(CreateTaskHistoryBrowserItem(record));
        }

        SelectedTaskHistoryBrowserItem = !string.IsNullOrWhiteSpace(selectedId)
            ? TaskHistoryBrowserItems.FirstOrDefault(item => item.Id == selectedId)
            : TaskHistoryBrowserItems.FirstOrDefault();

        PopulateSelectedTaskHistoryDetails();
    }

    private void PopulateSelectedTaskHistoryDetails()
    {
        TaskHistoryEventItems.Clear();
        TaskHistoryToolCallItems.Clear();

        if (SelectedTaskHistoryBrowserItem is null)
        {
            RefreshTaskHistoryDetailState();
            return;
        }

        var record = _taskHistoryBrowserRecords.FirstOrDefault(item => item.Id == SelectedTaskHistoryBrowserItem.Id);
        if (record is null)
        {
            RefreshTaskHistoryDetailState();
            return;
        }

        foreach (var item in record.Events.OrderBy(item => item.Timestamp).TakeLast(40))
        {
            TaskHistoryEventItems.Add(new SettingsListItem(
                FormatTime(item.Timestamp),
                string.IsNullOrWhiteSpace(item.State) ? $"{item.Progress}%" : item.State,
                CleanText(item.Message, "无事件内容", 180)));
        }

        foreach (var toolCall in record.ToolCalls.OrderByDescending(item => item.Timestamp).Take(30))
        {
            TaskHistoryToolCallItems.Add(new SettingsListItem(
                toolCall.ToolName,
                toolCall.Success ? "成功" : "失败",
                CleanText(toolCall.Error ?? toolCall.Result ?? toolCall.Arguments, "无工具调用详情", 180)));
        }

        RefreshTaskHistoryDetailState();
    }

    private static TaskHistoryBrowserItem CreateTaskHistoryBrowserItem(TaskHistoryRecord record)
    {
        var transcript = CreateTaskTranscript(record);
        return new TaskHistoryBrowserItem(
            record.Id,
            CleanText(record.Title, "未命名任务", 80),
            FormatTaskType(record.Type),
            FormatTaskStatus(record.Status),
            FormatTime(record.CreatedAt),
            FormatDuration(record.Duration),
            CleanText(record.Input, "无输入", 400),
            CleanText(record.Result, "无结果", 800),
            CleanText(record.Error, "无", 500),
            $"{record.Events.Count} 条",
            $"{record.ToolCalls.Count} 次",
            transcript);
    }

    private static IEnumerable<TaskHistoryRecord> ApplyTaskHistoryStatusFilter(
        IEnumerable<TaskHistoryRecord> records,
        string? filter)
    {
        return filter switch
        {
            "已完成" => records.Where(item => item.Status == AppTaskStatus.Completed),
            "失败" => records.Where(item => item.Status == AppTaskStatus.Failed),
            "已取消" => records.Where(item => item.Status == AppTaskStatus.Cancelled),
            "进行中" => records.Where(item => item.Status is AppTaskStatus.Created or AppTaskStatus.Running),
            _ => records
        };
    }

    private static string CreateTaskTranscript(TaskHistoryRecord record)
    {
        var messages = record.Events
            .Where(item => !string.IsNullOrWhiteSpace(item.Message))
            .OrderBy(item => item.Timestamp)
            .TakeLast(20)
            .Select(item =>
            {
                var role = item.State switch
                {
                    "user" => "用户",
                    "assistant" => "妍",
                    _ => string.IsNullOrWhiteSpace(item.State) ? "事件" : item.State
                };
                return $"{FormatTime(item.Timestamp)} {role}: {item.Message.Trim()}";
            })
            .ToList();

        if (messages.Count > 0)
        {
            return CleanText(string.Join(Environment.NewLine, messages), "无对话记录", 1200);
        }

        var fallback = string.Join(
            Environment.NewLine,
            new[]
            {
                $"用户: {CleanText(record.Input, "无输入", 500)}",
                $"妍: {CleanText(record.Result ?? record.Error, "暂无结果", 700)}"
            });
        return CleanText(fallback, "无对话记录", 1200);
    }

    private static string CreateTaskHistoryExportText(TaskHistoryBrowserItem item)
    {
        return string.Join(
            Environment.NewLine,
            [
                $"标题：{item.Title}",
                $"类型：{item.Type}",
                $"状态：{item.Status}",
                $"创建时间：{item.CreatedAt}",
                $"耗时：{item.Duration}",
                string.Empty,
                "用户输入：",
                item.Input,
                string.Empty,
                "任务结果：",
                item.Result,
                string.Empty,
                "错误：",
                item.Error,
                string.Empty,
                "对话与事件：",
                item.Transcript
            ]);
    }

    private static string FormatTaskStatus(AppTaskStatus status) => status switch
    {
        AppTaskStatus.Created => "已创建",
        AppTaskStatus.Running => "进行中",
        AppTaskStatus.Completed => "已完成",
        AppTaskStatus.Failed => "失败",
        AppTaskStatus.Cancelled => "已取消",
        _ => status.ToString()
    };

    private static string FormatTaskType(TaskType type) => type switch
    {
        TaskType.Chat => "普通问答",
        TaskType.SummarizePage => "网页总结",
        TaskType.AnalyzeError => "报错分析",
        TaskType.InspectProject => "项目诊断",
        TaskType.WriteFile => "写文件",
        TaskType.RunCommand => "执行命令",
        TaskType.UpdateMemory => "记忆更新",
        TaskType.ScreenUnderstand => "屏幕理解",
        TaskType.SolveProblem => "题目解答",
        TaskType.ComputerUse => "Computer Use",
        _ => type.ToString()
    };

    private static string FormatDuration(TimeSpan? duration)
    {
        if (!duration.HasValue)
            return "未完成";

        return duration.Value.TotalMinutes >= 1
            ? $"{duration.Value.TotalMinutes:F1} 分钟"
            : $"{Math.Max(1, duration.Value.TotalSeconds):F0} 秒";
    }

    private static string CreateAuditLogKey(AuditLogEntry entry)
    {
        if (!string.IsNullOrWhiteSpace(entry.Id))
            return entry.Id;

        return $"{entry.TaskId}|{entry.Operation}|{entry.Target}|{entry.Timestamp:O}";
    }

    private static string FormatTime(DateTime value)
    {
        var local = value.Kind == DateTimeKind.Local ? value : value.ToLocalTime();
        return local.ToString("MM-dd HH:mm");
    }

    private static string FormatPermissionLevel(PermissionLevel level) => level switch
    {
        PermissionLevel.L0_Chat => "L0 聊天",
        PermissionLevel.L1_WindowTitle => "L1 窗口标题",
        PermissionLevel.L2_ScreenBrowser => "L2 屏幕/浏览器",
        PermissionLevel.L3_FileRead => "L3 文件读取",
        PermissionLevel.L4_FileWrite => "L4 文件写入",
        PermissionLevel.L5_CommandExec => "L5 命令执行",
        PermissionLevel.L6_Forbidden => "L6 禁止",
        _ => level.ToString()
    };

    private static string FormatPermissionDecision(PermissionDecision decision) => decision switch
    {
        PermissionDecision.Allow => "允许",
        PermissionDecision.AllowOnce => "允许一次",
        PermissionDecision.AllowAlways => "永久允许",
        PermissionDecision.Deny => "拒绝",
        _ => decision.ToString()
    };

    private static string GetMemoryTypeText(MemoryType type) => type switch
    {
        MemoryType.User => "用户偏好",
        MemoryType.Project => "项目信息",
        MemoryType.Skill => "技能流程",
        MemoryType.History => "任务历史",
        _ => type.ToString()
    };

    private void RefreshRuntimeOverviewCards()
    {
        RuntimeOverviewItems.Clear();

        var providerName = SelectedProvider?.DisplayName ?? "未选择";
        var hasModelConfig = SelectedProvider is not null &&
                             !string.IsNullOrWhiteSpace(ApiEndpoint) &&
                             !string.IsNullOrWhiteSpace(ModelName);
        var hasRequiredKey = SelectedProvider?.Name == "Local" || !string.IsNullOrWhiteSpace(ApiKey);
        RuntimeOverviewItems.Add(new SettingsListItem(
            "模型 Provider",
            !hasModelConfig ? "未配置" : hasRequiredKey ? (IsModelConnectionSuccessful ? "已验证" : "待测试") : "缺少 Key",
            $"{providerName} / {CleanText(ModelName, "未填写模型", 64)}。实际可用性以测试连接结果为准。"));

        RuntimeOverviewItems.Add(new SettingsListItem(
            "Mimo Code",
            IsMimoCodeEnabled ? "已启用" : "未启用",
            IsMimoCodeEnabled
                ? $"{CleanText(MimoCodeExecutablePath, "mimo", 80)}；{GetMimoCodeModelModeText(MimoCodeModelConfigMode)}。"
                : "关闭时仍可使用内置 Agent；需要本机代码能力时再启用。"));

        RuntimeOverviewItems.Add(new SettingsListItem(
            "语音 UI",
            "状态壳就绪",
            $"{VoiceConfigurationSummary}。真实 STT/TTS 录制、转写和播放仍等待服务接入。"));

        RuntimeOverviewItems.Add(new SettingsListItem(
            "权限确认",
            _permissionManager is null ? "未注入" : "已接入",
            $"{SelectedAutoApproveLevel?.Title ?? "每次确认"}；最近审计 {_auditLogTotalCount} 条。"));

        RuntimeOverviewItems.Add(new SettingsListItem(
            "记忆中心",
            _memoryStore is null ? "未注入" : IsMemoryEnabled ? "已开启" : "已关闭",
            _memoryStatistics is null
                ? "等待 IMemoryStore 统计。"
                : $"全部 {_memoryStatistics.TotalCount} 条，待确认 {_memoryStatistics.UnconfirmedCount} 条。"));

        RuntimeOverviewItems.Add(new SettingsListItem(
            "任务历史",
            _taskHistoryStore is null ? "未注入" : _taskHistoryStatistics is null ? "等待统计" : $"{_taskHistoryStatistics.TotalTasks} 条",
            _taskHistoryStatistics is null
                ? "等待 ITaskHistoryStore 统计。"
                : $"完成 {_taskHistoryStatistics.CompletedTasks}，失败 {_taskHistoryStatistics.FailedTasks}，工具调用 {_taskHistoryStatistics.TotalToolCalls}。"));

        RuntimeOverviewItems.Add(new SettingsListItem(
            "快捷键",
            HotkeyItems.Count == 0 ? "未读取" : "已读取",
            $"聊天：{CleanText(ChatHotkeyText, "未配置", 40)}；屏幕圈选：{CleanText(ScreenSelectionHotkeyText, "未配置", 40)}。"));

        RuntimeOverviewItems.Add(new SettingsListItem(
            "日志/数据目录",
            DataDirectoryItems.Count == 0 ? "未读取" : "已定位",
            CleanText(DataStorageSummary, "等待刷新本机数据目录。", 160)));

        RuntimeOverviewItems.Add(new SettingsListItem(
            "角色外观",
            string.IsNullOrWhiteSpace(CharacterName) ? "未命名" : CharacterName,
            $"{CleanText(CharacterRole, "桌面助手", 60)}；状态图预览 {CharacterStatePreviewItems.Count} 项。"));

        RuntimeOverviewStatus =
            $"已汇总 {RuntimeOverviewItems.Count} 个运行模块。总览只读；具体配置请进入左侧对应页面处理。";
    }

    private void RefreshPermissionCards()
    {
        PermissionReadinessItems.Clear();
        PermissionReadinessItems.Add(new SettingsListItem(
            "独立确认弹窗",
            "已就绪",
            "文件写入、命令执行、权限请求和记忆保存可以共用当前确认弹窗体系。"));
        PermissionReadinessItems.Add(new SettingsListItem(
            "M29 回调接口",
            _permissionManager is null ? "未注入" : "已接入",
            "设置页会读取 IPermissionManager 和 IAuditLogStore 的最近权限审计数据。"));
        PermissionReadinessItems.Add(new SettingsListItem(
            "自动批准等级",
            SelectedAutoApproveLevel?.Title ?? "每次确认",
            $"本地策略已可保存，审计日志保留 {AuditLogRetentionDays} 天。"));
        PermissionReadinessItems.Add(new SettingsListItem(
            "审计记录",
            _auditLogTotalCount <= 0 ? "暂无" : $"{_auditLogTotalCount} 条",
            _recentAuditLogs.Count == 0 ? "当前没有可展示的权限决策记录。" : $"已展示最近 {_recentAuditLogs.Count} 条。"));

        PermissionRequestTypeItems.Clear();
        foreach (var group in _recentAuditLogs
                     .GroupBy(item => item.Level)
                     .OrderByDescending(group => group.Key)
                     .Take(4))
        {
            PermissionRequestTypeItems.Add(new SettingsListItem(
                FormatPermissionLevel(group.Key),
                $"{group.Count()} 条",
                $"最近权限审计中 {FormatPermissionLevel(group.Key)} 的请求数量。"));
        }

        if (PermissionRequestTypeItems.Count == 0)
        {
            PermissionRequestTypeItems.Add(new SettingsListItem(
                "文件写入确认",
                "已接入",
                "用于保存、覆盖、删除等会修改本地文件的操作。"));
            PermissionRequestTypeItems.Add(new SettingsListItem(
                "命令执行确认",
                "已接入",
                "用于 PowerShell、外部程序和高风险命令执行。"));
            PermissionRequestTypeItems.Add(new SettingsListItem(
                "工具权限确认",
                "等待记录",
                "工具执行管道产生审计后会在这里按风险级别统计。"));
        }

        PermissionAuditItems.Clear();
        PermissionAuditItems.Add(new SettingsListItem(
            "永久授权",
            $"{_permissionSettings.PermanentPermissions.Count} 项",
            _permissionSettings.PermanentPermissions.Count == 0
                ? "当前本地策略没有保存永久授权项。"
                : string.Join("、", _permissionSettings.PermanentPermissions.Take(3).Select(item => $"{item.Key} L{item.Value}"))));
        PermissionAuditItems.Add(new SettingsListItem(
            "黑名单命令",
            $"{_permissionSettings.BlockedCommands.Count} 项",
            _permissionSettings.BlockedCommands.Count == 0
                ? "当前没有配置本地阻止命令。"
                : string.Join("、", _permissionSettings.BlockedCommands.Take(3))));
        PermissionAuditItems.Add(new SettingsListItem(
            "审计日志",
            $"保留 {AuditLogRetentionDays} 天",
            _recentAuditLogs.Count == 0 ? "暂无最近审计记录。" : "最近权限审计记录如下。"));

        foreach (var entry in _recentAuditLogs)
        {
            PermissionAuditItems.Add(new SettingsListItem(
                FormatTime(entry.Timestamp),
                FormatPermissionDecision(entry.Decision),
                $"{entry.Operation} / {FormatPermissionLevel(entry.Level)} / {entry.Target}"));
        }
        RefreshRuntimeOverviewCards();
    }

    private void RefreshMemoryCards()
    {
        MemoryReadinessItems.Clear();
        MemoryReadinessItems.Add(new SettingsListItem(
            "记忆开关",
            IsMemoryEnabled ? "已开启" : "已关闭",
            "当前保存到 AppSettings.MemoryEnabled，可被工具链直接读取。"));
        MemoryReadinessItems.Add(new SettingsListItem(
            "M30 确认回调",
            "已接入",
            "UI 已实现 IMemoryConfirmationPrompt，支持保存、仅本次有效、不保存和编辑内容。"));
        MemoryReadinessItems.Add(new SettingsListItem(
            "待确认队列",
            HasPendingMemoryReviews ? $"{PendingMemoryReviews.Count} 条" : "空",
            "来自 IMemoryStore 中 IsConfirmed=false 的记忆条目。"));
        MemoryReadinessItems.Add(new SettingsListItem(
            "记忆存储统计",
            _memoryStore is null ? "未注入" : "已接入",
            _memoryStatistics is null
                ? "等待读取 MemoryStatistics。"
                : $"最近更新时间：{(_memoryStatistics.LastUpdated.HasValue ? FormatTime(_memoryStatistics.LastUpdated.Value) : "暂无")}。"));

        MemoryStatsItems.Clear();
        if (_memoryStatistics is null)
        {
            MemoryStatsItems.Add(new SettingsListItem("全部记忆", "未读取", "等待 IMemoryStore.GetStatisticsAsync。"));
            MemoryStatsItems.Add(new SettingsListItem("待确认", "未读取", "等待 IMemoryStore.GetStatisticsAsync。"));
        }
        else
        {
            MemoryStatsItems.Add(new SettingsListItem("全部记忆", $"{_memoryStatistics.TotalCount}", "MemoryStatistics.TotalCount。"));
            MemoryStatsItems.Add(new SettingsListItem("用户偏好", $"{_memoryStatistics.UserCount}", "MemoryType.User。"));
            MemoryStatsItems.Add(new SettingsListItem("项目信息", $"{_memoryStatistics.ProjectCount}", "MemoryType.Project。"));
            MemoryStatsItems.Add(new SettingsListItem("技能流程", $"{_memoryStatistics.SkillCount}", "MemoryType.Skill。"));
            MemoryStatsItems.Add(new SettingsListItem("任务历史", $"{_memoryStatistics.HistoryCount}", "MemoryType.History。"));
            MemoryStatsItems.Add(new SettingsListItem("待确认", $"{_memoryStatistics.UnconfirmedCount}", "未确认记忆需要用户确认后进入长期记忆。"));
        }

        MemoryActionItems.Clear();
        if (_recentMemoryEntries.Count == 0)
        {
            MemoryActionItems.Add(new SettingsListItem("最近记忆", "暂无", "保存记忆后会在这里显示最近条目。"));
        }
        else
        {
            foreach (var entry in _recentMemoryEntries)
            {
                MemoryActionItems.Add(new SettingsListItem(
                    $"{GetMemoryTypeText(entry.Type)} / {entry.Key}",
                    entry.IsConfirmed ? "已确认" : "待确认",
                    $"{FormatTime(entry.UpdatedAt)} · {CleanText(entry.Content, "无内容", 80)}"));
            }
        }
        RefreshRuntimeOverviewCards();
    }

    private void RefreshTaskHistoryCards()
    {
        TaskHistoryStatsItems.Clear();

        if (_taskHistoryStatistics is null)
        {
            TaskHistoryStatsItems.Add(new SettingsListItem("历史存储", _taskHistoryStore is null ? "未注入" : "已接入", "等待 ITaskHistoryStore.GetStatisticsAsync。"));
            TaskHistoryStatsItems.Add(new SettingsListItem("当前列表", $"{TaskHistoryBrowserItems.Count} 条", "搜索或筛选后会刷新当前列表。"));
            RefreshRuntimeOverviewCards();
            return;
        }

        TaskHistoryStatsItems.Add(new SettingsListItem("全部任务", $"{_taskHistoryStatistics.TotalTasks}", "ITaskHistoryStore 中记录的任务总数。"));
        TaskHistoryStatsItems.Add(new SettingsListItem("已完成", $"{_taskHistoryStatistics.CompletedTasks}", "成功结束的任务。"));
        TaskHistoryStatsItems.Add(new SettingsListItem("失败", $"{_taskHistoryStatistics.FailedTasks}", "执行失败或异常结束的任务。"));
        TaskHistoryStatsItems.Add(new SettingsListItem("进行中", $"{_taskHistoryStatistics.RunningTasks}", "仍处于 Created/Running 的任务。"));
        TaskHistoryStatsItems.Add(new SettingsListItem("工具调用", $"{_taskHistoryStatistics.TotalToolCalls}", "历史任务中记录的工具调用总数。"));
        TaskHistoryStatsItems.Add(new SettingsListItem("平均耗时", FormatDuration(TimeSpan.FromSeconds(_taskHistoryStatistics.AverageDurationSeconds)), "按已完成任务统计的平均耗时。"));

        foreach (var group in _taskHistoryStatistics.TaskTypeCounts
                     .OrderByDescending(item => item.Value)
                     .Take(4))
        {
            TaskHistoryStatsItems.Add(new SettingsListItem(
                FormatTaskType(group.Key),
                $"{group.Value} 条",
                "按任务类型统计的历史记录。"));
        }
        RefreshRuntimeOverviewCards();
    }

    private void RefreshCharacterAssetSuggestions()
    {
        CharacterAssetSuggestions.Clear();
        CharacterAvatarImageSuggestion = string.Empty;

        var folder = ResolveCharacterFolder(CharacterImageFolder);
        if (folder is null)
        {
            CharacterAssetSuggestionStatus = "图片目录不可用，无法生成匹配建议。";
            RefreshCharacterAssetSuggestionState();
            return;
        }

        var files = new DirectoryInfo(folder)
            .EnumerateFiles()
            .Where(file => SupportedCharacterImageExtensions.Contains(file.Extension))
            .OrderBy(file => file.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (files.Length == 0)
        {
            CharacterAssetSuggestionStatus = "图片目录里没有可用图片。";
            RefreshCharacterAssetSuggestionState();
            return;
        }

        CharacterAvatarImageSuggestion = CreateAvatarImageSuggestion(files);

        var alreadyAvailable = 0;
        var suggested = 0;
        var unmatched = 0;
        foreach (var item in CharacterStateImageItems)
        {
            var currentExists = ImageExists(folder, item.FileName);
            var match = currentExists
                ? (FileName: item.FileName, Score: 100)
                : FindBestAssetMatch(item.StateKey, item.DisplayName, files);

            var hasSuggestion = !string.IsNullOrWhiteSpace(match.FileName);
            var isAlreadyApplied = currentExists && hasSuggestion;
            if (isAlreadyApplied)
            {
                alreadyAvailable++;
            }
            else if (hasSuggestion)
            {
                suggested++;
            }
            else
            {
                unmatched++;
            }

            CharacterAssetSuggestions.Add(new CharacterAssetSuggestionItem(
                item.StateKey,
                item.DisplayName,
                item.FileName,
                match.FileName,
                CreateSuggestionStatusText(isAlreadyApplied, hasSuggestion, match.Score),
                CreateSuggestionReason(isAlreadyApplied, hasSuggestion, match.Score),
                isAlreadyApplied,
                CreateSuggestionStatusColor(isAlreadyApplied, hasSuggestion, match.Score)));
        }

        var avatarText = string.IsNullOrWhiteSpace(CharacterAvatarImageSuggestion)
            ? "未找到头像建议"
            : $"头像建议：{CharacterAvatarImageSuggestion}";
        CharacterAssetSuggestionStatus =
            $"扫描 {files.Length} 张图片：{alreadyAvailable} 个当前可用，{suggested} 个可应用建议，{unmatched} 个未匹配。{avatarText}。";
        RefreshCharacterAssetSuggestionState();
    }

    private static string CreateAvatarImageSuggestion(IReadOnlyList<FileInfo> files)
    {
        var candidate = files
            .Select(file => new
            {
                File = file,
                Score = ScoreCandidate("Avatar", "头像", file.Name, AvatarMatchKeywords)
            })
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.File.Name.Length)
            .ThenBy(x => x.File.Name, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();

        return candidate?.File.Name ?? string.Empty;
    }

    private static (string FileName, int Score) FindBestAssetMatch(
        string stateKey,
        string displayName,
        IReadOnlyList<FileInfo> files)
    {
        var keywords = CharacterStateMatchKeywords.TryGetValue(stateKey, out var configuredKeywords)
            ? configuredKeywords
            : [stateKey];

        var match = files
            .Select(file => new
            {
                File = file,
                Score = ScoreCandidate(stateKey, displayName, file.Name, keywords)
            })
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.File.Name.Length)
            .ThenBy(x => x.File.Name, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();

        return match is null ? (string.Empty, 0) : (match.File.Name, match.Score);
    }

    private static int ScoreCandidate(
        string stateKey,
        string displayName,
        string fileName,
        IReadOnlyList<string> keywords)
    {
        var name = Path.GetFileNameWithoutExtension(fileName);
        var score = 0;

        if (name.Equals(stateKey, StringComparison.OrdinalIgnoreCase))
        {
            score += 40;
        }
        else if (name.Contains(stateKey, StringComparison.OrdinalIgnoreCase))
        {
            score += 24;
        }

        foreach (var keyword in keywords
                     .Where(x => !string.IsNullOrWhiteSpace(x))
                     .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (name.Equals(keyword, StringComparison.OrdinalIgnoreCase))
            {
                score += 22;
            }
            else if (name.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            {
                score += 12;
            }
        }

        if (!string.IsNullOrWhiteSpace(displayName) &&
            name.Contains(displayName, StringComparison.OrdinalIgnoreCase))
        {
            score += 6;
        }

        return score;
    }

    private static string CreateSuggestionStatusText(bool isAlreadyApplied, bool hasSuggestion, int score)
    {
        if (isAlreadyApplied)
            return "当前可用";

        if (!hasSuggestion)
            return "未匹配";

        return score >= 20 ? "高匹配" : "可尝试";
    }

    private static string CreateSuggestionReason(bool isAlreadyApplied, bool hasSuggestion, int score)
    {
        if (isAlreadyApplied)
            return "当前配置已能解析到图片。";

        if (!hasSuggestion)
            return "目录中没有明显匹配的图片。";

        return score >= 20
            ? "文件名与状态关键词高度匹配。"
            : "文件名包含部分状态关键词。";
    }

    private static string CreateSuggestionStatusColor(bool isAlreadyApplied, bool hasSuggestion, int score)
    {
        if (isAlreadyApplied)
            return "#0F766E";

        if (!hasSuggestion)
            return "#667085";

        return score >= 20 ? "#175CD3" : "#B45309";
    }

    private void RefreshCharacterAssetSuggestionState()
    {
        OnPropertyChanged(nameof(HasCharacterAssetSuggestions));
        OnPropertyChanged(nameof(HasNoCharacterAssetSuggestions));
    }

    private void RefreshCharacterProfiles(string? selectedId = null)
    {
        selectedId ??= SelectedCharacterProfile?.Id;
        CharacterProfiles.Clear();

        foreach (var entry in _characterStore.ListProfiles())
        {
            CharacterProfiles.Add(CreateCharacterProfileListItem(entry));
        }

        SelectedCharacterProfile = !string.IsNullOrWhiteSpace(selectedId)
            ? CharacterProfiles.FirstOrDefault(x => x.Id == selectedId)
            : CharacterProfiles.FirstOrDefault();
        RefreshCharacterProfileState();
    }

    private void RefreshCharacterProfileState()
    {
        OnPropertyChanged(nameof(HasCharacterProfiles));
        OnPropertyChanged(nameof(HasNoCharacterProfiles));
        OnPropertyChanged(nameof(HasSelectedCharacterProfile));
        RefreshRuntimeOverviewCards();
    }

    private static CharacterProfileListItem CreateCharacterProfileListItem(MascotCharacterProfileEntry entry) =>
        new(entry, LoadCharacterProfileThumbnail(entry.AvatarImagePath));

    private static IImage? LoadCharacterProfileThumbnail(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            return null;

        try
        {
            using var stream = File.OpenRead(filePath);
            return new Bitmap(stream);
        }
        catch
        {
            return null;
        }
    }

    private string CreateUniqueDuplicateProfileName(string name)
    {
        var baseName = CleanText(name, "角色档案", 16);
        var existingNames = _characterStore
            .ListProfiles()
            .Select(x => x.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        for (var index = 1; index <= 99; index++)
        {
            var suffix = index == 1 ? "副本" : $"副本 {index}";
            var candidate = CleanText($"{baseName} {suffix}", "角色档案 副本", 24);
            if (!existingNames.Contains(candidate))
                return candidate;
        }

        return CleanText($"{baseName} {DateTime.Now:HHmmss}", "角色档案 副本", 24);
    }

    private void ApplyCharacterProfile(MascotCharacterProfile profile, bool save)
    {
        profile.EnsureImageDefaults();
        _isApplyingCharacterProfile = true;

        try
        {
            CharacterName = CleanText(profile.Name, "妍", 12);
            CharacterRole = CleanText(profile.Role, "寻研桌面助手", 24);
            CharacterAvatarText = CleanText(profile.AvatarText, "妍", 4);
            CharacterDescription = CleanText(profile.Description, "主动理解屏幕与任务上下文，清晰地给出下一步。", 120);
            CharacterPersonality = CleanText(profile.Personality, "沉稳可靠", 12);
            CharacterToneStyle = SelectExistingOption(CharacterToneStyleOptions, profile.ToneStyle, "友善");
            CharacterLanguageStyle = SelectExistingOption(CharacterLanguageStyleOptions, profile.LanguageStyle, "标准");
            CharacterReplyLength = SelectExistingOption(CharacterReplyLengthOptions, profile.ReplyLength, "平衡");
            CharacterUseEmoji = profile.UseEmoji;
            CharacterSystemPromptSuffix = CleanText(profile.SystemPromptSuffix, string.Empty, 500);
            ApplyCharacterTraitSelection(profile.PersonalityTraits);
            CharacterCatchphrase = CleanText(profile.Catchphrase, "我在桌面待命，随时可以接任务。", 40);
            PreviewCharacterStyle();
            CharacterAccentColor = NormalizeHexColor(profile.AccentColor, "#2563EB");
            CharacterBackgroundColor = NormalizeHexColor(profile.BackgroundColor, "#EEF6FF");
            CharacterImageFolder = CleanPathText(profile.ImageFolder, "assets/characters/default", 260);
            CharacterAvatarImage = CleanPathText(profile.AvatarImage, "avatar.png", 160);

            foreach (var item in CharacterStateImageItems)
            {
                if (profile.StateImages.TryGetValue(item.StateKey, out var fileName))
                {
                    item.FileName = CleanPathText(fileName, item.FileName, 160);
                }
            }

            RefreshCharacterBrushes();
            RefreshCharacterImagePreview();
        }
        finally
        {
            _isApplyingCharacterProfile = false;
        }

        if (save)
        {
            _characterStore.Save(BuildCurrentCharacterProfile());
        }
    }

    private MascotCharacterProfile BuildCurrentCharacterProfile() => new()
    {
        Name = CleanText(CharacterName, "妍", 12),
        Role = CleanText(CharacterRole, "寻研桌面助手", 24),
        AvatarText = CleanText(CharacterAvatarText, "妍", 4),
        Description = CleanText(CharacterDescription, "主动理解屏幕与任务上下文，清晰地给出下一步。", 120),
        Personality = CleanText(CharacterPersonality, "沉稳可靠", 12),
        ToneStyle = SelectExistingOption(CharacterToneStyleOptions, CharacterToneStyle, "友善"),
        LanguageStyle = SelectExistingOption(CharacterLanguageStyleOptions, CharacterLanguageStyle, "标准"),
        ReplyLength = SelectExistingOption(CharacterReplyLengthOptions, CharacterReplyLength, "平衡"),
        UseEmoji = CharacterUseEmoji,
        SystemPromptSuffix = CleanText(CharacterSystemPromptSuffix, string.Empty, 500),
        PersonalityTraits = CharacterTraitOptions
            .Where(item => item.IsSelected)
            .Select(item => item.Title)
            .ToList(),
        Catchphrase = CleanText(CharacterCatchphrase, "我在桌面待命，随时可以接任务。", 40),
        AccentColor = NormalizeHexColor(CharacterAccentColor, "#2563EB"),
        BackgroundColor = NormalizeHexColor(CharacterBackgroundColor, "#EEF6FF"),
        ImageFolder = CleanPathText(CharacterImageFolder, "assets/characters/default", 260),
        AvatarImage = CleanPathText(CharacterAvatarImage, "avatar.png", 160),
        StateImages = CharacterStateImageItems.ToDictionary(
            item => item.StateKey,
            item => CleanPathText(item.FileName, "avatar.png", 160))
    };

    private MascotCharacterManifest BuildCurrentCharacterManifest()
    {
        var stateDisplayNames = CharacterStateImageItems.ToDictionary(
            item => item.StateKey,
            item => item.DisplayName,
            StringComparer.OrdinalIgnoreCase);

        return MascotCharacterManifestFactory.Create(BuildCurrentCharacterProfile(), stateDisplayNames);
    }

    private void RefreshCharacterManifestPreviewCore(string? status = null)
    {
        var manifest = BuildCurrentCharacterManifest();
        CharacterManifestPreview = MascotCharacterManifestFactory.ToJson(manifest);
        RefreshCharacterManifestFormatItems(manifest);

        if (!string.IsNullOrWhiteSpace(status))
        {
            CharacterManifestStatus = status;
        }
    }

    private void RefreshCharacterManifestFormatItems(MascotCharacterManifest manifest)
    {
        CharacterManifestFormatItems.Clear();
        CharacterManifestFormatItems.Add(new SettingsListItem(
            "主格式",
            manifest.Schema,
            "以寻研角色资料、人格设定和状态图为主，不把 Petdex 当作项目主模型。"));
        CharacterManifestFormatItems.Add(new SettingsListItem(
            "默认角色",
            $"{manifest.Name} / {manifest.Slug}",
            "当前角色会作为可导出的完整角色包样例。"));
        CharacterManifestFormatItems.Add(new SettingsListItem(
            "状态资源",
            $"{manifest.States.Count} 个状态",
            "状态名沿用寻研 UI 状态，缺图时回退到 Idle 或默认头像。"));
        CharacterManifestFormatItems.Add(new SettingsListItem(
            "兼容策略",
            manifest.Animation.PetdexCompatibility.Enabled ? "Petdex 启用" : "Petdex 可选",
            "Petdex 精灵图、网格和状态行只作为导入转换信息。"));
    }

    private void SetStateImage(string stateKey, string fileName)
    {
        var item = CharacterStateImageItems.FirstOrDefault(x => x.StateKey == stateKey);
        if (item is not null)
        {
            item.FileName = fileName;
        }
    }

    private void ApplyPickedImageFile(string filePath, Action<string> applyFileName)
    {
        var directory = Path.GetDirectoryName(filePath);
        var fileName = Path.GetFileName(filePath);
        if (string.IsNullOrWhiteSpace(directory) || string.IsNullOrWhiteSpace(fileName))
            return;

        if (!IsSameFolder(CharacterImageFolder, directory))
        {
            CharacterImageFolder = directory;
        }

        applyFileName(fileName);
        RefreshCharacterImagePreview();
    }

    private static IReadOnlyList<string> NormalizeDroppedCharacterImageFiles(IEnumerable<string>? filePaths)
    {
        var result = new List<string>();
        foreach (var filePath in filePaths ?? [])
        {
            if (!IsSupportedCharacterImageFile(filePath))
                continue;

            try
            {
                result.Add(Path.GetFullPath(filePath));
            }
            catch
            {
                result.Add(filePath);
            }
        }

        return result
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private void RefreshCharacterBrushes()
    {
        CharacterAccentBrush = BrushFrom(CharacterAccentColor);
        CharacterBackgroundBrush = BrushFrom(CharacterBackgroundColor);
    }

    private void RefreshCharacterImagePreview()
    {
        var profile = BuildCurrentCharacterProfile();
        var state = MascotState.Idle;
        if (SelectedCharacterStateImage is not null &&
            Enum.TryParse<MascotState>(SelectedCharacterStateImage.StateKey, out var selectedState))
        {
            state = selectedState;
        }

        var result = _characterImageService.Resolve(profile, state);
        CharacterImageSource = result.Image;
        HasCharacterImage = result.HasImage;
        CharacterImageStatus = result.Message;
        RefreshCharacterAssetWarnings(profile);
        RefreshCharacterStatePreviews(profile);
        RefreshCharacterManifestPreviewCore();
    }

    private void ApplyCharacterTraitSelection(IEnumerable<string>? traits)
    {
        var selected = new HashSet<string>(traits ?? [], StringComparer.OrdinalIgnoreCase);
        if (selected.Count == 0)
        {
            selected.Add("可靠");
            selected.Add("主动");
        }

        foreach (var option in CharacterTraitOptions)
        {
            option.IsSelected = selected.Contains(option.Id) || selected.Contains(option.Title);
        }
    }

    private string GetSelectedCharacterTraitText()
    {
        var selected = CharacterTraitOptions
            .Where(item => item.IsSelected)
            .Select(item => item.Title)
            .ToArray();

        return selected.Length == 0 ? "稳定" : string.Join("、", selected);
    }

    private static string SelectExistingOption(IEnumerable<string> options, string? value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
            return fallback;

        return options.FirstOrDefault(option => string.Equals(option, value.Trim(), StringComparison.OrdinalIgnoreCase))
               ?? fallback;
    }

    private void RefreshCharacterStatePreviews(MascotCharacterProfile profile)
    {
        CharacterStatePreviewItems.Clear();

        var availableCount = 0;
        var fallbackCount = 0;
        var missingCount = 0;

        foreach (var item in CharacterStateImageItems)
        {
            if (!Enum.TryParse<MascotState>(item.StateKey, out var state))
            {
                var invalidResult = new CharacterImageResult();
                CharacterStatePreviewItems.Add(new CharacterStatePreviewItem(
                    item.StateKey,
                    item.DisplayName,
                    item.FileName,
                    invalidResult,
                    usesConfiguredImage: false));
                missingCount++;
                continue;
            }

            var result = _characterImageService.Resolve(profile, state);
            var usesConfiguredImage = UsesConfiguredStateImage(item.FileName, result.FilePath);
            CharacterStatePreviewItems.Add(new CharacterStatePreviewItem(
                item.StateKey,
                item.DisplayName,
                item.FileName,
                result,
                usesConfiguredImage));

            if (!result.HasImage)
            {
                missingCount++;
            }
            else if (usesConfiguredImage)
            {
                availableCount++;
            }
            else
            {
                fallbackCount++;
            }
        }

        CharacterStatePreviewStatus =
            $"状态图检查：{availableCount} 个可用，{fallbackCount} 个回退头像，{missingCount} 个缺失。";
        OnPropertyChanged(nameof(HasCharacterStatePreviews));
        RefreshRuntimeOverviewCards();
    }

    private static bool UsesConfiguredStateImage(string configuredFileName, string? resolvedFilePath)
    {
        if (string.IsNullOrWhiteSpace(configuredFileName) || string.IsNullOrWhiteSpace(resolvedFilePath))
            return false;

        try
        {
            var configured = configuredFileName.Replace('/', Path.DirectorySeparatorChar);
            if (Path.IsPathRooted(configured))
            {
                return string.Equals(
                    Path.GetFullPath(configured),
                    Path.GetFullPath(resolvedFilePath),
                    StringComparison.OrdinalIgnoreCase);
            }

            return string.Equals(
                Path.GetFileName(configured),
                Path.GetFileName(resolvedFilePath),
                StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private void RefreshCharacterAssetWarnings(MascotCharacterProfile profile)
    {
        var folder = ResolveCharacterFolder(profile.ImageFolder);
        if (folder is null)
        {
            CharacterAssetWarningText = "图片目录不可用，当前会回退到文字头像。";
            return;
        }

        var missingItems = new List<string>();
        if (!ImageExists(folder, profile.AvatarImage))
        {
            missingItems.Add("头像");
        }

        foreach (var item in CharacterStateImageItems)
        {
            if (!profile.StateImages.TryGetValue(item.StateKey, out var fileName) || !ImageExists(folder, fileName))
            {
                missingItems.Add(item.DisplayName);
            }
        }

        CharacterAssetWarningText = missingItems.Count == 0
            ? "当前目录下头像和状态图都可用。"
            : $"缺少或不支持 {missingItems.Count} 项：{string.Join("、", missingItems.Take(4))}{(missingItems.Count > 4 ? " 等" : string.Empty)}。";
    }

    private static bool ImageExists(string folder, string? imageFile)
    {
        if (string.IsNullOrWhiteSpace(imageFile))
            return false;

        var candidate = Path.IsPathRooted(imageFile)
            ? imageFile
            : Path.Combine(folder, imageFile.Replace('/', Path.DirectorySeparatorChar));

        return File.Exists(candidate) &&
               SupportedCharacterImageExtensions.Contains(Path.GetExtension(candidate));
    }

    private static string? ResolveCharacterFolder(string folder)
    {
        if (string.IsNullOrWhiteSpace(folder))
            return null;

        if (Path.IsPathRooted(folder))
        {
            return Directory.Exists(folder) ? folder : null;
        }

        foreach (var root in EnumerateCharacterRootCandidates())
        {
            var normalizedFolder = folder.Replace('/', Path.DirectorySeparatorChar);
            var direct = Path.GetFullPath(Path.Combine(root, normalizedFolder));
            if (Directory.Exists(direct))
                return direct;

            var underCharacters = Path.GetFullPath(Path.Combine(root, "assets", "characters", normalizedFolder));
            if (Directory.Exists(underCharacters))
                return underCharacters;
        }

        return null;
    }

    private static IEnumerable<string> EnumerateCharacterRootCandidates()
    {
        yield return Environment.CurrentDirectory;

        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            yield return directory.FullName;
            directory = directory.Parent;
        }
    }

    private static bool IsSameFolder(string left, string right)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
                return false;

            var leftFullPath = Path.GetFullPath(left.Replace('/', Path.DirectorySeparatorChar))
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var rightFullPath = Path.GetFullPath(right.Replace('/', Path.DirectorySeparatorChar))
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return string.Equals(leftFullPath, rightFullPath, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private void OnCharacterStateImageItemChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_isApplyingCharacterProfile || e.PropertyName != nameof(CharacterStateImageItem.FileName))
            return;

        RefreshCharacterImagePreview();
    }

    partial void OnSelectedCharacterStateImageChanged(CharacterStateImageItem? value)
    {
        if (!_isApplyingCharacterProfile)
        {
            RefreshCharacterImagePreview();
        }
    }

    partial void OnCharacterImageFolderChanged(string value)
    {
        if (!_isApplyingCharacterProfile)
        {
            RefreshCharacterImagePreview();
        }
    }

    partial void OnCharacterAvatarImageChanged(string value)
    {
        if (!_isApplyingCharacterProfile)
        {
            RefreshCharacterImagePreview();
        }
    }

    partial void OnCharacterAccentColorChanged(string value)
    {
        if (!_isApplyingCharacterProfile)
        {
            RefreshCharacterBrushes();
        }
    }

    partial void OnCharacterBackgroundColorChanged(string value)
    {
        if (!_isApplyingCharacterProfile)
        {
            RefreshCharacterBrushes();
        }
    }

    private static string CleanText(string? value, string fallback, int maxLength)
    {
        var text = string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        return text.Length <= maxLength ? text : text[..maxLength];
    }

    private static string CleanPathText(string? value, string fallback, int maxLength)
    {
        var text = CleanText(value, fallback, maxLength);
        return text.Replace('\\', '/');
    }

    private static string NormalizeHexColor(string? value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
            return fallback;

        var color = value.Trim();
        if (!color.StartsWith('#') || color.Length != 7)
            return fallback;

        try
        {
            Color.Parse(color);
            return color.ToUpperInvariant();
        }
        catch
        {
            return fallback;
        }
    }

    private static IBrush BrushFrom(string color)
    {
        try
        {
            return new SolidColorBrush(Color.Parse(color));
        }
        catch
        {
            return new SolidColorBrush(Color.Parse("#2563EB"));
        }
    }

    private void RefreshPendingMemoryReviewState()
    {
        if (SelectedPendingMemoryReview is not null &&
            PendingMemoryReviews.All(x => x.Id != SelectedPendingMemoryReview.Id))
        {
            SelectedPendingMemoryReview = null;
        }

        OnPropertyChanged(nameof(HasPendingMemoryReviews));
        OnPropertyChanged(nameof(HasNoPendingMemoryReviews));
    }

    private void RefreshMemoryBrowserState()
    {
        if (SelectedMemoryBrowserItem is not null &&
            MemoryBrowserItems.All(x => x.Id != SelectedMemoryBrowserItem.Id))
        {
            SelectedMemoryBrowserItem = null;
        }

        OnPropertyChanged(nameof(HasMemoryBrowserItems));
        OnPropertyChanged(nameof(HasNoMemoryBrowserItems));
    }

    private void RefreshTaskHistoryBrowserState()
    {
        if (SelectedTaskHistoryBrowserItem is not null &&
            TaskHistoryBrowserItems.All(x => x.Id != SelectedTaskHistoryBrowserItem.Id))
        {
            SelectedTaskHistoryBrowserItem = null;
        }

        OnPropertyChanged(nameof(HasTaskHistoryBrowserItems));
        OnPropertyChanged(nameof(HasNoTaskHistoryBrowserItems));
    }

    private void RefreshTaskHistoryDetailState()
    {
        OnPropertyChanged(nameof(HasTaskHistoryEvents));
        OnPropertyChanged(nameof(HasNoTaskHistoryEvents));
        OnPropertyChanged(nameof(HasTaskHistoryToolCalls));
        OnPropertyChanged(nameof(HasNoTaskHistoryToolCalls));
    }

    private void ResetMemoryClearConfirmation()
    {
        _pendingMemoryClearIds.Clear();
        _isMemoryClearConfirmationPending = false;
        MemoryClearButtonText = "清理";
    }

    private void ResetMemoryEditMode()
    {
        IsMemoryBrowserEditMode = false;
        MemoryEditDraftContent = string.Empty;
        MemoryEditDraftTags = string.Empty;
    }

    private void ResetTaskHistoryCleanupConfirmation()
    {
        _selectedTaskHistoryCleanupFilter = SelectedTaskHistoryStatusFilter;
        _isTaskHistoryCleanupConfirmationPending = false;
        TaskHistoryCleanupButtonText = "清理旧记录";
    }
}
