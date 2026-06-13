using System.Collections.ObjectModel;
using System.ComponentModel;
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

    private readonly IConfigurationManager _configurationManager;
    private readonly ISettingsDiagnosticsService _diagnosticsService;
    private readonly IOnboardingWindowService _onboardingWindowService;
    private readonly IMascotCharacterStore _characterStore;
    private readonly ICharacterImageService _characterImageService;
    private readonly ICharacterAssetImportService _characterAssetImportService;
    private readonly ICharacterAssetPickerService _characterAssetPickerService;
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

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPendingMemoryReviewSelected))]
    [NotifyPropertyChangedFor(nameof(HasNoPendingMemoryReviewSelected))]
    private PendingMemoryReviewItem? _selectedPendingMemoryReview;

    [ObservableProperty] private bool _isBusy;

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
        ICharacterAssetPickerService characterAssetPickerService)
    {
        _configurationManager = configurationManager;
        _diagnosticsService = diagnosticsService;
        _onboardingWindowService = onboardingWindowService;
        _characterStore = characterStore;
        _characterImageService = characterImageService;
        _characterAssetImportService = characterAssetImportService;
        _characterAssetPickerService = characterAssetPickerService;

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
        PendingMemoryReviews = [];
        CharacterProfiles = [];
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

        SelectedProvider = Providers[0];
        SelectedAutoApproveLevel = PermissionAutoApproveLevels[0];
        SelectedCharacterStateImage = CharacterStateImageItems[0];
        ApplyProviderDefaultsIfEmpty();
        ApplyCharacterProfile(_characterStore.Load(), save: false);
        RefreshCharacterProfiles();
        RefreshMimoCodeCards();
        RefreshPermissionCards();
        RefreshMemoryCards();
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
    public ObservableCollection<PendingMemoryReviewItem> PendingMemoryReviews { get; }
    public ObservableCollection<CharacterProfileListItem> CharacterProfiles { get; }
    public ObservableCollection<CharacterStateImageItem> CharacterStateImageItems { get; }
    public bool IsNotModelSectionSelected => !IsModelSectionSelected;
    public bool HasPendingMemoryReviews => PendingMemoryReviews.Count > 0;
    public bool HasNoPendingMemoryReviews => !HasPendingMemoryReviews;
    public bool IsPendingMemoryReviewSelected => SelectedPendingMemoryReview is not null;
    public bool HasNoPendingMemoryReviewSelected => !IsPendingMemoryReviewSelected;
    public bool HasNoCharacterImage => !HasCharacterImage;
    public bool HasCharacterProfiles => CharacterProfiles.Count > 0;
    public bool HasNoCharacterProfiles => !HasCharacterProfiles;
    public bool HasSelectedCharacterProfile => SelectedCharacterProfile is not null;

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
            RefreshMimoCodeCards();
            RefreshPermissionCards();
            RefreshMemoryCards();
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
    private async Task PickCharacterImageFolder()
    {
        var folder = await _characterAssetPickerService.PickImageFolderAsync();
        if (string.IsNullOrWhiteSpace(folder))
            return;

        CharacterImageFolder = folder;
        RefreshCharacterImagePreview();
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
