using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using DesktopMascot.Core.Configuration;
using DesktopMascot.Core.Memory;
using DesktopMascot.Core.Security;
using DesktopMascot.Core.Storage;
using DesktopMascot.UI.ViewModels;
using DesktopMascot.UI.Views;

namespace DesktopMascot.UI.Services;

public sealed class SettingsWindowService : ISettingsWindowService
{
    private readonly IConfigurationManager _configurationManager;
    private readonly ISettingsDiagnosticsService _diagnosticsService;
    private readonly IOnboardingWindowService _onboardingWindowService;
    private readonly IMascotCharacterStore _characterStore;
    private readonly ICharacterImageService _characterImageService;
    private readonly ICharacterAssetImportService _characterAssetImportService;
    private readonly IGlobalHotkeyService _hotkeyService;
    private readonly IPermissionManager _permissionManager;
    private readonly IAuditLogStore _auditLogStore;
    private readonly IMemoryStore _memoryStore;
    private readonly ITaskHistoryStore _taskHistoryStore;
    private readonly ITaskResultActionService _taskResultActionService;
    private SettingsWindow? _window;

    public SettingsWindowService(
        IConfigurationManager configurationManager,
        ISettingsDiagnosticsService diagnosticsService,
        IOnboardingWindowService onboardingWindowService,
        IMascotCharacterStore characterStore,
        ICharacterImageService characterImageService,
        ICharacterAssetImportService characterAssetImportService,
        IGlobalHotkeyService hotkeyService,
        IPermissionManager permissionManager,
        IAuditLogStore auditLogStore,
        IMemoryStore memoryStore,
        ITaskHistoryStore taskHistoryStore,
        ITaskResultActionService taskResultActionService)
    {
        _configurationManager = configurationManager;
        _diagnosticsService = diagnosticsService;
        _onboardingWindowService = onboardingWindowService;
        _characterStore = characterStore;
        _characterImageService = characterImageService;
        _characterAssetImportService = characterAssetImportService;
        _hotkeyService = hotkeyService;
        _permissionManager = permissionManager;
        _auditLogStore = auditLogStore;
        _memoryStore = memoryStore;
        _taskHistoryStore = taskHistoryStore;
        _taskResultActionService = taskResultActionService;
    }

    public void ShowSettingsWindow(string? sectionId = null)
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(() => ShowSettingsWindow(sectionId));
            return;
        }

        if (_window is { IsVisible: true })
        {
            SelectSection(sectionId);
            _window.Activate();
            return;
        }

        var window = new SettingsWindow();
        var assetPickerService = new CharacterAssetPickerService(() => window);
        var viewModel = new SettingsWindowViewModel(
            _configurationManager,
            _diagnosticsService,
            _onboardingWindowService,
            _characterStore,
            _characterImageService,
            _characterAssetImportService,
            assetPickerService,
            _hotkeyService,
            _permissionManager,
            _auditLogStore,
            _memoryStore,
            _taskHistoryStore,
            _taskResultActionService);
        _window = window;
        _window.DataContext = viewModel;
        SelectSection(viewModel, sectionId);
        _window.Closed += (_, _) => _window = null;

        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime { MainWindow: { } owner })
        {
            _window.Show(owner);
        }
        else
        {
            _window.Show();
        }

        _window.Activate();
    }

    private void SelectSection(string? sectionId)
    {
        if (_window?.DataContext is SettingsWindowViewModel viewModel)
        {
            SelectSection(viewModel, sectionId);
        }
    }

    private static void SelectSection(SettingsWindowViewModel viewModel, string? sectionId)
    {
        if (!string.IsNullOrWhiteSpace(sectionId))
        {
            viewModel.SelectSectionById(sectionId);
        }
    }
}
