using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DesktopMascot.Core.Configuration;
using DesktopMascot.Core.Services;
using DesktopMascot.UI.Services;

namespace DesktopMascot.UI.ViewModels;

public sealed partial class OnboardingWindowViewModel : ObservableObject
{
    private readonly IConfigurationManager _configurationManager;
    private readonly ISettingsDiagnosticsService _diagnosticsService;
    private readonly ISettingsService? _settingsService;
    private bool _isApplyingProvider;
    private AppSettings _settings = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsModeStep))]
    [NotifyPropertyChangedFor(nameof(IsModelStep))]
    [NotifyPropertyChangedFor(nameof(IsMimoCodeStep))]
    [NotifyPropertyChangedFor(nameof(IsReviewStep))]
    [NotifyPropertyChangedFor(nameof(CanGoBack))]
    [NotifyPropertyChangedFor(nameof(ContinueButtonText))]
    private int _currentStep;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsBuiltInMode))]
    [NotifyPropertyChangedFor(nameof(IsMimoCodeMode))]
    [NotifyPropertyChangedFor(nameof(ModeSummaryText))]
    [NotifyPropertyChangedFor(nameof(NeedsMimoCodeStep))]
    private string _agentMode = "BuiltIn";

    [ObservableProperty] private ModelProviderOption? _selectedProvider;
    [ObservableProperty] private string _apiKey = string.Empty;
    [ObservableProperty] private string _apiEndpoint = string.Empty;
    [ObservableProperty] private string _modelName = string.Empty;
    [ObservableProperty] private string _modelTestStatus = "填写 Provider 后可以测试连接。";
    [ObservableProperty] private bool _isModelTestSuccessful;

    [ObservableProperty] private string _mimoCodeExecutablePath = "mimo";
    [ObservableProperty] private string _mimoCodeWorkspacePath = string.Empty;
    [ObservableProperty] private string _mimoCodeModelConfigMode = "AppProvider";
    [ObservableProperty] private string _mimoCodeTestStatus = "启用 MiMo Code 时会先检测本机 CLI。";
    [ObservableProperty] private bool _isMimoCodeTestSuccessful;

    [ObservableProperty] private string _setupStatus = "先选择运行模式。";
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanGoBack))]
    private bool _isBusy;

    public OnboardingWindowViewModel(
        IConfigurationManager configurationManager,
        ISettingsDiagnosticsService diagnosticsService,
        ISettingsService? settingsService = null)
    {
        _configurationManager = configurationManager;
        _diagnosticsService = diagnosticsService;
        _settingsService = settingsService;

        Providers = new ObservableCollection<ModelProviderOption>(ModelProviderCatalog.CreateDefaults());

        SelectedProvider = Providers[0];
        ApplyProviderDefaultsIfEmpty();
    }

    public ObservableCollection<ModelProviderOption> Providers { get; }
    public bool IsModeStep => CurrentStep == 0;
    public bool IsModelStep => CurrentStep == 1;
    public bool IsMimoCodeStep => CurrentStep == 2 && NeedsMimoCodeStep;
    public bool IsReviewStep => CurrentStep == ReviewStepIndex;
    public bool CanGoBack => CurrentStep > 0 && !IsBusy;
    public bool IsBuiltInMode => AgentMode == "BuiltIn";
    public bool IsMimoCodeMode => AgentMode == "MimoCode";
    public bool NeedsMimoCodeStep => IsMimoCodeMode;
    public int ReviewStepIndex => NeedsMimoCodeStep ? 3 : 2;
    public string ContinueButtonText => IsReviewStep ? "完成并进入" : "继续";
    public string ModeSummaryText => IsMimoCodeMode ? "MiMo Code" : "内置 Agent";
    public string MimoCodeModelModeText => NormalizeMimoCodeModelMode(MimoCodeModelConfigMode) == "MimoLocalConfig"
        ? "使用 MiMo Code 本机配置"
        : "使用寻研01 Provider/API Key";

    public event EventHandler? Completed;
    public event EventHandler? Skipped;

    public async Task LoadAsync(CancellationToken ct = default)
    {
        IsBusy = true;

        try
        {
            _settings = await _configurationManager.GetAppSettingsAsync(ct);
            await MigrateLegacyApiKeyAsync(ct);
            AgentMode = _settings.MimoCodeEnabled ? "MimoCode" : "BuiltIn";
            ApiKey = string.Empty;
            ApiEndpoint = _settings.ApiEndpoint;
            ModelName = _settings.ModelName;
            SelectedProvider = Providers.FirstOrDefault(x => x.Name == _settings.ProviderName) ??
                               InferProvider(_settings.ApiEndpoint) ??
                               Providers[0];
            ApiKey = await LoadApiKeyForSelectedProviderAsync(ct);
            MimoCodeExecutablePath = _settings.MimoCodeExecutablePath;
            MimoCodeWorkspacePath = _settings.MimoCodeWorkspaceDirectory;
            MimoCodeModelConfigMode = string.IsNullOrWhiteSpace(_settings.MimoCodeModelConfigMode)
                ? "AppProvider"
                : _settings.MimoCodeModelConfigMode;
            SetupStatus = "按顺序完成运行模式、模型和本机能力配置。";
            RefreshDerivedProperties();
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void SelectAgentMode(string? mode)
    {
        AgentMode = mode == "MimoCode" ? "MimoCode" : "BuiltIn";
        SetupStatus = IsMimoCodeMode
            ? "已选择 MiMo Code：代码任务会交给本机 CLI，仍使用用户自己的模型配置。"
            : "已选择内置 Agent：任务直接由寻研01内置 Agent 执行。";
        RefreshDerivedProperties();
    }

    [RelayCommand]
    private void UseProviderDefaults()
    {
        if (SelectedProvider is null)
            return;

        ApiEndpoint = SelectedProvider.DefaultEndpoint;
        ModelName = SelectedProvider.DefaultModel;
        ModelTestStatus = $"已填入 {SelectedProvider.DisplayName} 默认配置。";
    }

    [RelayCommand]
    private void UseAppProviderForMimoCode()
    {
        MimoCodeModelConfigMode = "AppProvider";
        OnPropertyChanged(nameof(MimoCodeModelModeText));
        MimoCodeTestStatus = "MiMo Code 会使用本向导保存的 Provider/API Key。";
    }

    [RelayCommand]
    private void UseMimoLocalConfig()
    {
        MimoCodeModelConfigMode = "MimoLocalConfig";
        OnPropertyChanged(nameof(MimoCodeModelModeText));
        MimoCodeTestStatus = "MiMo Code 会使用用户本机已有配置，本向导不会注入 API Key。";
    }

    [RelayCommand]
    private async Task TestModelConnection()
    {
        if (SelectedProvider is null)
        {
            ModelTestStatus = "请先选择 Provider。";
            return;
        }

        if (string.IsNullOrWhiteSpace(ApiEndpoint) || string.IsNullOrWhiteSpace(ModelName))
        {
            ModelTestStatus = "请先填写 Base URL 和模型名。";
            return;
        }

        IsBusy = true;
        ModelTestStatus = $"正在测试 {SelectedProvider.DisplayName}...";

        try
        {
            var result = await _diagnosticsService.TestModelConnectionAsync(
                new ModelConnectionTestRequest(
                    SelectedProvider.Name,
                    ApiEndpoint,
                    ModelName,
                    ApiKey));

            IsModelTestSuccessful = result.Success;
            ModelTestStatus = $"{result.Message} {result.Detail}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task TestMimoCode()
    {
        IsBusy = true;
        MimoCodeTestStatus = "正在检测 MiMo Code...";

        try
        {
            var result = await _diagnosticsService.TestMimoCodeAsync(
                new MimoCodeConnectionTestRequest(
                    IsMimoCodeMode,
                    MimoCodeExecutablePath,
                    MimoCodeWorkspacePath,
                    NormalizeMimoCodeModelMode(MimoCodeModelConfigMode),
                    SelectedProvider?.Name ?? _settings.ProviderName,
                    ApiEndpoint,
                    ModelName,
                    ApiKey));

            IsMimoCodeTestSuccessful = result.Success;
            MimoCodeTestStatus = $"{result.Message} {result.Detail}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void Back()
    {
        if (CurrentStep <= 0 || IsBusy)
            return;

        CurrentStep--;
        RefreshDerivedProperties();
    }

    [RelayCommand]
    private async Task Continue()
    {
        if (IsBusy)
            return;

        if (IsReviewStep)
        {
            await SaveAndCompleteAsync();
            return;
        }

        CurrentStep++;
        if (!NeedsMimoCodeStep && CurrentStep == 2)
        {
            CurrentStep = ReviewStepIndex;
        }

        RefreshDerivedProperties();
    }

    [RelayCommand]
    private async Task SkipSetup()
    {
        if (IsBusy)
            return;

        IsBusy = true;

        try
        {
            _settings.OnboardingCompleted = true;
            await _configurationManager.SaveAppSettingsAsync(_settings);
            Skipped?.Invoke(this, EventArgs.Empty);
        }
        finally
        {
            IsBusy = false;
        }
    }

    partial void OnSelectedProviderChanged(ModelProviderOption? value)
    {
        if (value is null || _isApplyingProvider)
            return;

        ApplyProviderDefaultsIfEmpty();
        _ = LoadApiKeyForSelectedProviderAndAssignAsync();
    }

    private async Task LoadApiKeyForSelectedProviderAndAssignAsync(CancellationToken ct = default)
    {
        ApiKey = await LoadApiKeyForSelectedProviderAsync(ct);
    }

    private async Task<string> LoadApiKeyForSelectedProviderAsync(CancellationToken ct = default)
    {
        if (_settingsService is null || SelectedProvider is null)
            return string.Empty;

        return await _settingsService.GetApiKeyAsync(SelectedProvider.Name, ct) ?? string.Empty;
    }

    private async Task MigrateLegacyApiKeyAsync(CancellationToken ct = default)
    {
        if (_settingsService is null || string.IsNullOrWhiteSpace(_settings.ApiKey))
            return;

        await _settingsService.SetApiKeyAsync(_settings.ProviderName, _settings.ApiKey.Trim(), ct);
        _settings.ApiKey = string.Empty;
        await _configurationManager.SaveAppSettingsAsync(_settings, ct);
    }

    partial void OnAgentModeChanged(string value)
    {
        if (!NeedsMimoCodeStep && CurrentStep > ReviewStepIndex)
        {
            CurrentStep = ReviewStepIndex;
        }

        RefreshDerivedProperties();
    }

    private async Task SaveAndCompleteAsync()
    {
        if (SelectedProvider is null)
            return;

        IsBusy = true;
        SetupStatus = "正在保存配置...";

        try
        {
            _settings.ProviderName = SelectedProvider.Name;
            if (!string.IsNullOrWhiteSpace(ApiKey) && _settingsService != null)
            {
                await _settingsService.SetApiKeyAsync(SelectedProvider.Name, ApiKey.Trim());
            }
            _settings.ApiKey = string.Empty;
            _settings.ApiEndpoint = ApiEndpoint.Trim();
            _settings.ModelName = ModelName.Trim();
            _settings.MimoCodeEnabled = IsMimoCodeMode;
            _settings.MimoCodeExecutablePath = string.IsNullOrWhiteSpace(MimoCodeExecutablePath)
                ? "mimo"
                : MimoCodeExecutablePath.Trim();
            _settings.MimoCodeWorkspaceDirectory = MimoCodeWorkspacePath.Trim();
            _settings.MimoCodeModelConfigMode = NormalizeMimoCodeModelMode(MimoCodeModelConfigMode);
            _settings.OnboardingCompleted = true;

            await _configurationManager.SaveAppSettingsAsync(_settings);
            SetupStatus = "配置已保存。";
            Completed?.Invoke(this, EventArgs.Empty);
        }
        finally
        {
            IsBusy = false;
        }
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

    private void RefreshDerivedProperties()
    {
        OnPropertyChanged(nameof(IsModeStep));
        OnPropertyChanged(nameof(IsModelStep));
        OnPropertyChanged(nameof(IsMimoCodeStep));
        OnPropertyChanged(nameof(IsReviewStep));
        OnPropertyChanged(nameof(CanGoBack));
        OnPropertyChanged(nameof(IsBuiltInMode));
        OnPropertyChanged(nameof(IsMimoCodeMode));
        OnPropertyChanged(nameof(NeedsMimoCodeStep));
        OnPropertyChanged(nameof(ReviewStepIndex));
        OnPropertyChanged(nameof(ContinueButtonText));
        OnPropertyChanged(nameof(ModeSummaryText));
        OnPropertyChanged(nameof(MimoCodeModelModeText));
    }

    private static string NormalizeMimoCodeModelMode(string value)
    {
        return value == "MimoLocalConfig" ? "MimoLocalConfig" : "AppProvider";
    }
}
