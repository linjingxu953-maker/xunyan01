using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DesktopMascot.Core.Configuration;
using DesktopMascot.Core.Enums;
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
    private bool _isApplyingProvider;
    private bool _isApplyingCharacterProfile;
    private AppSettings _settings = new();
    private PermissionSettings _permissionSettings = new();

    [ObservableProperty] private string _selectedSectionId = "model";
    [ObservableProperty] private ModelProviderOption? _selectedProvider;
    [ObservableProperty] private string _apiKey = string.Empty;
    [ObservableProperty] private string _apiEndpoint = string.Empty;
    [ObservableProperty] private string _modelName = string.Empty;
    [ObservableProperty] private string _modelSettingsStatus = "模型配置会保存到本机配置目录。";
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
    [ObservableProperty] private string _hotkeySettingsStatus = "快捷键状态会在应用启动后显示。";
    [ObservableProperty] private string _chatHotkeyText = string.Empty;
    [ObservableProperty] private string _screenSelectionHotkeyText = string.Empty;
    [ObservableProperty] private string _dataSettingsStatus = "本机数据目录用于配置、日志、记忆、任务历史和角色资源。";
    [ObservableProperty] private string _dataStorageSummary = "正在等待刷新。";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPendingMemoryReviewSelected))]
    [NotifyPropertyChangedFor(nameof(HasNoPendingMemoryReviewSelected))]
    private PendingMemoryReviewItem? _selectedPendingMemoryReview;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotBusy))]
    private bool _isBusy;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotModelSectionSelected))]
    private bool _isModelSectionSelected = true;

    [ObservableProperty] private bool _isMimoCodeSectionSelected;
    [ObservableProperty] private bool _isPermissionSectionSelected;
    [ObservableProperty] private bool _isMemorySectionSelected;
    [ObservableProperty] private bool _isHotkeySectionSelected;
    [ObservableProperty] private bool _isDataSectionSelected;
    [ObservableProperty] private bool _isAppearanceSectionSelected;
    [ObservableProperty] private string _characterName = "小桌灵";
    [ObservableProperty] private string _characterRole = "桌面工作助手";
    [ObservableProperty] private string _characterAvatarText = "灵";
    [ObservableProperty] private string _characterPersonality = "沉稳可靠";
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
        IGlobalHotkeyService hotkeyService)
    {
        _configurationManager = configurationManager;
        _diagnosticsService = diagnosticsService;
        _onboardingWindowService = onboardingWindowService;
        _characterStore = characterStore;
        _characterImageService = characterImageService;
        _characterAssetImportService = characterAssetImportService;
        _characterAssetPickerService = characterAssetPickerService;
        _hotkeyService = hotkeyService;

        Sections =
        [
            new SettingsSectionItem("model", "模型", "Provider、API Key、模型名"),
            new SettingsSectionItem("mimoCode", "Mimo Code", "本机代码能力接入"),
            new SettingsSectionItem("permission", "权限", "确认策略与审计"),
            new SettingsSectionItem("memory", "记忆", "记忆保存与检索"),
            new SettingsSectionItem("hotkey", "快捷键", "唤起与桌面操作"),
            new SettingsSectionItem("data", "日志/数据", "本地目录与日志"),
            new SettingsSectionItem("appearance", "角色外观", "小人形象与主题")
        ];

        Providers = new ObservableCollection<ModelProviderOption>(ModelProviderCatalog.CreateDefaults());

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
        PermissionRequestTypeItems = [];
        PermissionAuditItems = [];
        MimoCodeReadinessItems = [];
        MemoryReadinessItems = [];
        MemoryStatsItems = [];
        MemoryActionItems = [];
        HotkeyItems = [];
        DataDirectoryItems = [];
        PendingMemoryReviews = [];
        CharacterProfiles = [];
        CharacterAssetSuggestions = [];
        CharacterStatePreviewItems = [];
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
        RefreshHotkeyCards();
        RefreshDataDirectoryItems();
    }

    public ObservableCollection<SettingsSectionItem> Sections { get; }
    public ObservableCollection<ModelProviderOption> Providers { get; }
    public ObservableCollection<PermissionLevelOption> PermissionAutoApproveLevels { get; }
    public ObservableCollection<SettingsListItem> MimoCodeReadinessItems { get; }
    public ObservableCollection<SettingsListItem> PermissionReadinessItems { get; }
    public ObservableCollection<SettingsListItem> PermissionRequestTypeItems { get; }
    public ObservableCollection<SettingsListItem> PermissionAuditItems { get; }
    public ObservableCollection<SettingsListItem> MemoryReadinessItems { get; }
    public ObservableCollection<SettingsListItem> MemoryStatsItems { get; }
    public ObservableCollection<SettingsListItem> MemoryActionItems { get; }
    public ObservableCollection<SettingsListItem> HotkeyItems { get; }
    public ObservableCollection<DataDirectoryItem> DataDirectoryItems { get; }
    public ObservableCollection<PendingMemoryReviewItem> PendingMemoryReviews { get; }
    public ObservableCollection<CharacterProfileListItem> CharacterProfiles { get; }
    public ObservableCollection<CharacterAssetSuggestionItem> CharacterAssetSuggestions { get; }
    public ObservableCollection<CharacterStateImageItem> CharacterStateImageItems { get; }
    public ObservableCollection<CharacterStatePreviewItem> CharacterStatePreviewItems { get; }
    public bool IsNotModelSectionSelected => !IsModelSectionSelected;
    public bool HasPendingMemoryReviews => PendingMemoryReviews.Count > 0;
    public bool HasNoPendingMemoryReviews => !HasPendingMemoryReviews;
    public bool IsPendingMemoryReviewSelected => SelectedPendingMemoryReview is not null;
    public bool HasNoPendingMemoryReviewSelected => !IsPendingMemoryReviewSelected;
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
            SelectedProvider = Providers.FirstOrDefault(x => x.Name == _settings.ProviderName) ??
                               InferProvider(_settings.ApiEndpoint) ??
                               Providers[0];
            IsMimoCodeEnabled = _settings.MimoCodeEnabled;
            MimoCodeExecutablePath = _settings.MimoCodeExecutablePath;
            MimoCodeWorkspacePath = _settings.MimoCodeWorkspaceDirectory;
            MimoCodeModelConfigMode = string.IsNullOrWhiteSpace(_settings.MimoCodeModelConfigMode)
                ? "AppProvider"
                : _settings.MimoCodeModelConfigMode;
            IsMemoryEnabled = _settings.MemoryEnabled;

            _permissionSettings = await _configurationManager.GetPermissionSettingsAsync(ct);
            SelectedAutoApproveLevel =
                PermissionAutoApproveLevels.FirstOrDefault(x => x.Level == _permissionSettings.AutoApproveLevel) ??
                PermissionAutoApproveLevels[0];
            AuditLogRetentionDays = _permissionSettings.AuditLogRetentionDays.ToString();

            ModelSettingsStatus = "已加载本机模型配置。";
            MimoCodeStatus = "已加载 Mimo Code 接入配置。模型调用不会使用内置 Key。";
            PermissionSettingsStatus = "已加载本机权限策略。M29 接入后会显示真实请求和审计数据。";
            MemorySettingsStatus = "已加载本机记忆开关。记忆确认弹窗已接入 IMemoryConfirmationPrompt。";
            ApplyCharacterProfile(_characterStore.Load(), save: false);
            CharacterSaveStatus = "已加载本机角色外观配置。";
            RefreshCharacterProfiles();
            LoadHotkeyTextFromService();
            RefreshMimoCodeCards();
            RefreshPermissionCards();
            RefreshMemoryCards();
            RefreshHotkeyCards();
            RefreshDataDirectoryItems();
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
        IsModelSectionSelected = sectionId == "model";
        IsMimoCodeSectionSelected = sectionId == "mimoCode";
        IsPermissionSectionSelected = sectionId == "permission";
        IsMemorySectionSelected = sectionId == "memory";
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
            await _configurationManager.SaveAppSettingsAsync(_settings);

            ModelSettingsStatus = string.IsNullOrWhiteSpace(_settings.ApiKey)
                ? "已保存模型配置，但 API Key 为空。"
                : $"已保存 {_settings.ProviderName} / {_settings.ModelName}。";
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
            return;
        }

        if (string.IsNullOrWhiteSpace(ApiEndpoint) || string.IsNullOrWhiteSpace(ModelName))
        {
            ModelSettingsStatus = "请先填写 Base URL 和模型名。";
            return;
        }

        if (string.IsNullOrWhiteSpace(ApiKey) && SelectedProvider.Name != "Local")
        {
            ModelSettingsStatus = "当前 API Key 为空。远程 Provider 需要用户自己的 API Key。";
            return;
        }

        IsBusy = true;
        ModelSettingsStatus = $"正在测试 {SelectedProvider.DisplayName}...";

        try
        {
            var result = await _diagnosticsService.TestModelConnectionAsync(
                new ModelConnectionTestRequest(
                    SelectedProvider.Name,
                    ApiEndpoint,
                    ModelName,
                    ApiKey));

            ModelSettingsStatus = result.Success
                ? $"{result.Message} {result.Detail}"
                : $"{result.Message} {result.Detail}";
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
        MimoCodeStatus = "已选择使用桌面小人设置中心的 Provider/API Key。";
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
            RefreshPermissionCards();
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void RefreshPermissionStatus()
    {
        PermissionSettingsStatus = "权限 UI 壳已就绪。等待 M29 将 IPermissionManager 的请求、审计和授权状态接入。";
        RefreshPermissionCards();
    }

    [RelayCommand]
    private void OpenPermissionAudit()
    {
        PermissionSettingsStatus = "审计日志入口已预留。M29 接入后这里会读取 IPermissionManager.GetAuditLogsAsync。";
    }

    [RelayCommand]
    private void ManagePermanentPermissions()
    {
        PermissionSettingsStatus = "永久授权管理入口已预留。M29 接入后这里会支持查看和撤销授权。";
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
            RefreshMemoryCards();
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void RefreshMemoryStatus()
    {
        MemorySettingsStatus = "记忆 UI 已接入 IMemoryConfirmationPrompt。统计、浏览和队列数据后续可从 IMemoryStore/事件流填充。";
        RefreshMemoryCards();
        RefreshPendingMemoryReviewState();
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
    private void OpenMemoryBrowser()
    {
        MemorySettingsStatus = "记忆浏览入口已预留。后续这里会支持按类型检索和确认状态筛选。";
    }

    [RelayCommand]
    private void ExportMemory()
    {
        MemorySettingsStatus = "记忆导出入口已预留。后续这里会调用 IMemoryStore.ExportAsync。";
    }

    [RelayCommand]
    private void ImportMemory()
    {
        MemorySettingsStatus = "记忆导入入口已预留。后续这里会调用 IMemoryStore.ImportAsync。";
    }

    [RelayCommand]
    private void ClearMemory()
    {
        MemorySettingsStatus = "记忆清理入口已预留。真实删除需要 M30 后增加二次确认。";
    }

    [RelayCommand]
    private void ConfirmPendingMemoryReview()
    {
        if (SelectedPendingMemoryReview is null)
        {
            MemorySettingsStatus = "请先选择一条待确认记忆。";
            return;
        }

        MemorySettingsStatus =
            $"保存入口已预留：{SelectedPendingMemoryReview.Key}。接入队列数据后会返回 MemoryDecision.Save。";
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
            $"拒绝入口已预留：{SelectedPendingMemoryReview.Key}。接入队列数据后会返回 MemoryDecision.Reject。";
    }

    [RelayCommand]
    private void SaveEditedPendingMemoryReview()
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

        MemorySettingsStatus =
            $"编辑后保存入口已预留：{SelectedPendingMemoryReview.Key}。接入队列数据后会返回 MemoryDecision.Save 和 EditedContent。";
    }

    [RelayCommand]
    private void SaveCharacter()
    {
        var profile = BuildCurrentCharacterProfile();
        ApplyCharacterProfile(profile, save: true);
        RefreshCharacterProfiles();
        CharacterSaveStatus = $"已保存 {CharacterName} 的角色外观配置。";
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

        ApplyProviderDefaultsIfEmpty();
    }

    partial void OnSelectedPendingMemoryReviewChanged(PendingMemoryReviewItem? value)
    {
        PendingMemoryDraftContent = value?.Content ?? string.Empty;
    }

    partial void OnSelectedCharacterProfileChanged(CharacterProfileListItem? value)
    {
        CharacterProfileNameDraft = value?.Name ?? CharacterName;
    }

    private void ApplyProviderDefaultsIfEmpty()
    {
        if (SelectedProvider is null)
            return;

        _isApplyingProvider = true;

        try
        {
            if (string.IsNullOrWhiteSpace(ApiEndpoint))
            {
                ApiEndpoint = SelectedProvider.DefaultEndpoint;
            }

            if (string.IsNullOrWhiteSpace(ModelName))
            {
                ModelName = SelectedProvider.DefaultModel;
            }
        }
        finally
        {
            _isApplyingProvider = false;
        }
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
            "开启后桌面小人可以把代码任务交给本机 Mimo Code connector。"));
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
            "走桌面小人确认体系",
            "文件写入、命令执行和记忆保存仍通过当前权限/记忆弹窗确认。"));
        MimoCodeReadinessItems.Add(new SettingsListItem(
            "后续对接",
            "等待 connector",
            "后续只需要把启动、任务输入、事件流和结果返回挂到这些配置上。"));
    }

    private static string NormalizeMimoCodeModelMode(string value)
    {
        return value == "MimoLocalConfig" ? "MimoLocalConfig" : "AppProvider";
    }

    private static string GetMimoCodeModelModeText(string value)
    {
        return NormalizeMimoCodeModelMode(value) == "MimoLocalConfig"
            ? "使用 Mimo Code 本机配置"
            : "使用桌面小人 Provider/API Key";
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

    private void RefreshPermissionCards()
    {
        PermissionReadinessItems.Clear();
        PermissionReadinessItems.Add(new SettingsListItem(
            "独立确认弹窗",
            "已就绪",
            "文件写入、命令执行、权限请求和记忆保存可以共用当前确认弹窗体系。"));
        PermissionReadinessItems.Add(new SettingsListItem(
            "M29 回调接口",
            "接入中",
            "MiMo 接入权限确认接口后，这里会显示真实权限请求状态。"));
        PermissionReadinessItems.Add(new SettingsListItem(
            "自动批准等级",
            SelectedAutoApproveLevel?.Title ?? "每次确认",
            "本地策略已可保存，后续由工具执行管道读取并执行。"));

        PermissionRequestTypeItems.Clear();
        PermissionRequestTypeItems.Add(new SettingsListItem(
            "文件写入确认",
            "弹窗入口已预留",
            "用于保存、覆盖、删除等会修改本地文件的操作。"));
        PermissionRequestTypeItems.Add(new SettingsListItem(
            "命令执行确认",
            "弹窗入口已预留",
            "用于 PowerShell、外部程序和高风险命令执行。"));
        PermissionRequestTypeItems.Add(new SettingsListItem(
            "工具权限确认",
            "等待 M25/M29",
            "用于工具执行管道的权限检查、用户决策和结果回写。"));

        PermissionAuditItems.Clear();
        PermissionAuditItems.Add(new SettingsListItem(
            "永久授权",
            $"{_permissionSettings.PermanentPermissions.Count} 项",
            "后续支持查看、撤销和按操作类型筛选。"));
        PermissionAuditItems.Add(new SettingsListItem(
            "黑名单命令",
            $"{_permissionSettings.BlockedCommands.Count} 项",
            "后续支持从设置页维护阻止规则。"));
        PermissionAuditItems.Add(new SettingsListItem(
            "审计日志",
            $"保留 {AuditLogRetentionDays} 天",
            "M29 接入后会展示最近请求、决策、目标和时间。"));
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
            "后续可绑定 MemoryConfirmationRequest 列表或状态事件流。"));
        MemoryReadinessItems.Add(new SettingsListItem(
            "记忆存储统计",
            "等待 IMemoryStore",
            "后续会读取 TotalCount、UnconfirmedCount 和分类数量。"));

        MemoryStatsItems.Clear();
        MemoryStatsItems.Add(new SettingsListItem("全部记忆", "待接入", "对应 MemoryStatistics.TotalCount。"));
        MemoryStatsItems.Add(new SettingsListItem("用户偏好", "待接入", "对应 MemoryType.User。"));
        MemoryStatsItems.Add(new SettingsListItem("项目信息", "待接入", "对应 MemoryType.Project。"));
        MemoryStatsItems.Add(new SettingsListItem("技能流程", "待接入", "对应 MemoryType.Skill。"));
        MemoryStatsItems.Add(new SettingsListItem("待确认", "待接入", "对应 MemoryStatistics.UnconfirmedCount。"));

        MemoryActionItems.Clear();
        MemoryActionItems.Add(new SettingsListItem("浏览记忆", "预留", "按类型、标签和关键词查看记忆。"));
        MemoryActionItems.Add(new SettingsListItem("导入/导出", "预留", "通过 IMemoryStore 导入导出结构化记忆。"));
        MemoryActionItems.Add(new SettingsListItem("清理记忆", "预留", "删除前需要二次确认和审计记录。"));
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
            CharacterName = CleanText(profile.Name, "小桌灵", 12);
            CharacterRole = CleanText(profile.Role, "桌面工作助手", 24);
            CharacterAvatarText = CleanText(profile.AvatarText, "灵", 4);
            CharacterPersonality = CleanText(profile.Personality, "沉稳可靠", 12);
            CharacterCatchphrase = CleanText(profile.Catchphrase, "我在桌面待命，随时可以接任务。", 40);
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
        Name = CleanText(CharacterName, "小桌灵", 12),
        Role = CleanText(CharacterRole, "桌面工作助手", 24),
        AvatarText = CleanText(CharacterAvatarText, "灵", 4),
        Personality = CleanText(CharacterPersonality, "沉稳可靠", 12),
        Catchphrase = CleanText(CharacterCatchphrase, "我在桌面待命，随时可以接任务。", 40),
        AccentColor = NormalizeHexColor(CharacterAccentColor, "#2563EB"),
        BackgroundColor = NormalizeHexColor(CharacterBackgroundColor, "#EEF6FF"),
        ImageFolder = CleanPathText(CharacterImageFolder, "assets/characters/default", 260),
        AvatarImage = CleanPathText(CharacterAvatarImage, "avatar.png", 160),
        StateImages = CharacterStateImageItems.ToDictionary(
            item => item.StateKey,
            item => CleanPathText(item.FileName, "avatar.png", 160))
    };

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
}
