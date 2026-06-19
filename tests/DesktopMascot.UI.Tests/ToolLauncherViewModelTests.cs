using DesktopMascot.Core.Configuration;
using DesktopMascot.Core.Enums;
using DesktopMascot.Core.Interfaces;
using DesktopMascot.Core.Memory;
using DesktopMascot.Core.Models;
using DesktopMascot.Core.Security;
using DesktopMascot.Core.Storage;
using DesktopMascot.UI.Services;
using DesktopMascot.UI.ViewModels;

namespace DesktopMascot.UI.Tests;

public sealed class ToolLauncherViewModelTests
{
    [Fact]
    public void UseToolLauncherItem_ScreenUnderstandRequestsScreenSelection()
    {
        using var viewModel = ToolLauncherViewModelFixture.Create();
        var requested = false;
        viewModel.ScreenSelectionRequested += (_, _) => requested = true;
        var item = viewModel.ToolLauncherItems.First(x => x.Id == "screen_understand");

        viewModel.UseToolLauncherItem(item);

        Assert.True(requested);
        Assert.True(viewModel.IsChatVisible);
        Assert.False(viewModel.IsToolLauncherVisible);
        Assert.Equal(string.Empty, viewModel.InputText);
    }

    [Fact]
    public void UseToolLauncherItem_ComputerUseOpensControlPanelWithTemplate()
    {
        using var viewModel = ToolLauncherViewModelFixture.Create();
        var item = viewModel.ToolLauncherItems.First(x => x.Id == "computer_use");

        viewModel.UseToolLauncherItem(item);

        Assert.True(viewModel.IsComputerUsePanelVisible);
        Assert.True(viewModel.IsChatVisible);
        Assert.False(viewModel.IsToolLauncherVisible);
        Assert.Contains("computer_use", viewModel.InputText);
        Assert.Contains("描述桌面目标", viewModel.InputText);
    }

    [Fact]
    public void UseToolLauncherItem_DefaultToolKeepsPromptFillBehavior()
    {
        using var viewModel = ToolLauncherViewModelFixture.Create();
        var item = viewModel.ToolLauncherItems.First(x => x.Id == "security_scan");

        viewModel.UseToolLauncherItem(item);

        Assert.False(viewModel.IsComputerUsePanelVisible);
        Assert.False(viewModel.IsToolLauncherVisible);
        Assert.Contains("security_scan", viewModel.InputText);
        Assert.Contains("请说明目标", viewModel.InputText);
    }
    [Fact]
    public void UseToolLauncherItem_CommandToolOpensStructuredForm()
    {
        using var viewModel = ToolLauncherViewModelFixture.Create();
        var item = viewModel.ToolLauncherItems.First(x => x.Id == "run_command");

        viewModel.UseToolLauncherItem(item);

        Assert.True(viewModel.HasToolLauncherForm);
        Assert.True(viewModel.IsToolLauncherCommandForm);
        Assert.Equal(item, viewModel.SelectedToolLauncherFormItem);
        Assert.Equal(string.Empty, viewModel.InputText);
        Assert.Contains("命令", viewModel.ToolLauncherPrimaryLabel);
    }

    [Fact]
    public void ApplyToolLauncherForm_CommandToolBuildsStructuredPrompt()
    {
        using var viewModel = ToolLauncherViewModelFixture.Create();
        var item = viewModel.ToolLauncherItems.First(x => x.Id == "run_command");
        viewModel.UseToolLauncherItem(item);
        viewModel.ToolLauncherPrimaryInput = "dotnet test DesktopMascot.sln";
        viewModel.ToolLauncherSecondaryInput = "C:\\repo";
        viewModel.ToolLauncherObjectiveInput = "验证全量测试";
        viewModel.ToolLauncherOutputInput = "给出通过数量和失败摘要";

        viewModel.ApplyToolLauncherForm();

        Assert.False(viewModel.HasToolLauncherForm);
        Assert.False(viewModel.IsToolLauncherVisible);
        Assert.Contains("run_command", viewModel.InputText);
        Assert.Contains("dotnet test DesktopMascot.sln", viewModel.InputText);
        Assert.Contains("C:\\repo", viewModel.InputText);
        Assert.Contains("验证全量测试", viewModel.InputText);
        Assert.Contains("给出通过数量和失败摘要", viewModel.InputText);
    }

    [Fact]
    public void UseToolLauncherItem_PathToolOpensPathForm()
    {
        using var viewModel = ToolLauncherViewModelFixture.Create();
        var item = viewModel.ToolLauncherItems.First(x => x.Id == "read_file");

        viewModel.UseToolLauncherItem(item);

        Assert.True(viewModel.HasToolLauncherForm);
        Assert.True(viewModel.IsToolLauncherPathForm);
        Assert.Contains("路径", viewModel.ToolLauncherPrimaryLabel);
    }

    [Fact]
    public void UseToolLauncherItem_ContentToolOpensContentForm()
    {
        using var viewModel = ToolLauncherViewModelFixture.Create();
        var item = viewModel.ToolLauncherItems.First(x => x.Id == "translate");

        viewModel.UseToolLauncherItem(item);

        Assert.True(viewModel.HasToolLauncherForm);
        Assert.True(viewModel.IsToolLauncherContentForm);
        Assert.Contains("内容", viewModel.ToolLauncherPrimaryLabel);
    }

    [Fact]
    public async Task PickToolLauncherFilePathAsync_PathFormSetsPrimaryInput()
    {
        using var viewModel = ToolLauncherViewModelFixture.Create();
        viewModel.SetToolLauncherPathPickers(
            _ => Task.FromResult<string?>("C:\\data\\report.pdf"),
            _ => Task.FromResult<string?>("C:\\data"));
        var item = viewModel.ToolLauncherItems.First(x => x.Id == "read_file");
        viewModel.UseToolLauncherItem(item);

        await viewModel.PickToolLauncherFilePathAsync();

        Assert.Equal("C:\\data\\report.pdf", viewModel.ToolLauncherPrimaryInput);
    }

    [Fact]
    public async Task PickToolLauncherFolderPathAsync_PathFormSetsPrimaryInput()
    {
        using var viewModel = ToolLauncherViewModelFixture.Create();
        viewModel.SetToolLauncherPathPickers(
            _ => Task.FromResult<string?>("C:\\data\\report.pdf"),
            _ => Task.FromResult<string?>("C:\\data\\project"));
        var item = viewModel.ToolLauncherItems.First(x => x.Id == "list_directory");
        viewModel.UseToolLauncherItem(item);

        await viewModel.PickToolLauncherFolderPathAsync();

        Assert.Equal("C:\\data\\project", viewModel.ToolLauncherPrimaryInput);
    }

    [Fact]
    public void CharacterSwitchItems_IncludeAssetDirectoryCandidates()
    {
        using var assetRoot = CharacterAssetTestRoot.Create(("yue guang", "avatar.png"));
        using var viewModel = ToolLauncherViewModelFixture.Create();
        viewModel.SetCharacterAssetRootCandidates([assetRoot.RootPath]);

        var item = Assert.Single(viewModel.CharacterSwitchItems, x => x.Name == "月光");
        Assert.Equal("assets/characters/yue guang", item.ImageFolder);
    }

    [Fact]
    public void SwitchCharacterProfile_AppliesAndSavesSelectedProfile()
    {
        var yueGuang = new MascotCharacterProfile
        {
            Name = "月光",
            Role = "寻研夜间助手",
            AvatarText = "月",
            ImageFolder = "assets/characters/yue guang",
            AvatarImage = "avatar.png",
            AccentColor = "#7C3AED",
            BackgroundColor = "#F5F3FF"
        };
        var store = new StubMascotCharacterStore(profiles: [("yue-guang", yueGuang)]);
        using var viewModel = ToolLauncherViewModelFixture.Create(store);

        var item = viewModel.CharacterSwitchItems.First(x => x.Name == "月光");
        viewModel.SwitchCharacterProfile(item);

        Assert.Equal("月光", viewModel.CharacterName);
        Assert.Equal("寻研夜间助手", viewModel.CharacterRole);
        Assert.Equal("assets/characters/yue guang", viewModel.CharacterImageFolder);
        Assert.Equal("月光", store.SavedProfile?.Name);
        Assert.Contains("月光", viewModel.CharacterSaveStatus);
    }

    [Fact]
    public void CharacterSwitchItems_ShowStateImageReadinessForPartialAssetDirectory()
    {
        using var assetRoot = CharacterAssetTestRoot.Create(
            ("feng lin yu ren", "avatar.png"),
            ("feng lin yu ren", "idle.png"),
            ("feng lin yu ren", "listening.png"));
        using var viewModel = ToolLauncherViewModelFixture.Create();
        viewModel.SetCharacterAssetRootCandidates([assetRoot.RootPath]);

        var item = Assert.Single(viewModel.CharacterSwitchItems, x => x.Name == "枫林渔人");

        Assert.Equal(2, item.AvailableStateImageCount);
        Assert.Equal(11, item.TotalStateImageCount);
        Assert.True(item.HasMissingStateImages);
        Assert.Contains("2/11", item.StateImageStatusText);
        Assert.Contains("完成", item.MissingStateImageText);
    }
}

file static class ToolLauncherViewModelFixture
{
    public static FloatingWindowViewModel Create(IMascotCharacterStore? characterStore = null)
    {
        return new FloatingWindowViewModel(
            new StubTaskRouter(),
            new StubTaskEventBus(),
            new StubTaskEventStream(),
            characterStore ?? new StubMascotCharacterStore(),
            new StubCharacterImageService(),
            new StubTaskResultActionService(),
            new StubConfirmationHandler(),
            new StubMemoryConfirmationHandler(),
            new StubConfigurationManager(),
            new StubSettingsDiagnosticsService(),
            new StubOnboardingWindowService(),
            new StubCharacterAssetImportService(),
            new StubGlobalHotkeyService(),
            new StubPermissionManager(),
            new StubAuditLogStore(),
            new StubMemoryStore(),
            new StubTaskHistoryStore());
    }
}

file sealed class StubTaskRouter : ITaskRouter
{
    public Task<TaskResult> DispatchAsync(AgentTask task, CancellationToken ct = default) =>
        Task.FromResult(new TaskResult { TaskId = task.Id, Success = true, Content = "ok" });

    public bool CancelTask(string taskId) => false;
    public void CancelAllTasks() { }
}

file sealed class StubTaskEventBus : ITaskEventBus
{
    public event EventHandler<TaskEvent>? TaskEventPublished;
    public void Publish(TaskEvent evt) => TaskEventPublished?.Invoke(this, evt);
}

file sealed class StubTaskEventStream : ITaskEventStream
{
    private static readonly IObservable<TaskEvent> EmptyObservable = new EmptyTaskEventObservable();

    public void Publish(TaskEvent evt) { }
    public IObservable<TaskEvent> Subscribe(string taskId) => EmptyObservable;
    public IObservable<TaskEvent> SubscribeAll() => EmptyObservable;
    public IReadOnlyList<TaskEvent> GetRecentEvents(string taskId, int count = 50) => [];
}

file sealed class EmptyTaskEventObservable : IObservable<TaskEvent>
{
    public IDisposable Subscribe(IObserver<TaskEvent> observer) => new EmptyDisposable();
}

file sealed class EmptyDisposable : IDisposable
{
    public void Dispose() { }
}

file sealed class StubMascotCharacterStore : IMascotCharacterStore
{
    private readonly Dictionary<string, MascotCharacterProfile> _profiles;
    private MascotCharacterProfile _activeProfile;

    public StubMascotCharacterStore(
        MascotCharacterProfile? activeProfile = null,
        IReadOnlyList<(string Id, MascotCharacterProfile Profile)>? profiles = null)
    {
        _activeProfile = activeProfile ?? new MascotCharacterProfile();
        _profiles = profiles?.ToDictionary(x => x.Id, x => x.Profile, StringComparer.OrdinalIgnoreCase)
            ?? new Dictionary<string, MascotCharacterProfile>(StringComparer.OrdinalIgnoreCase);
    }

    public event EventHandler? ProfileChanged;
    public MascotCharacterProfile? SavedProfile { get; private set; }
    public MascotCharacterProfile Load() => _activeProfile.Clone();

    public void Save(MascotCharacterProfile profile)
    {
        _activeProfile = profile.Clone();
        SavedProfile = profile.Clone();
        ProfileChanged?.Invoke(this, EventArgs.Empty);
    }

    public IReadOnlyList<MascotCharacterProfileEntry> ListProfiles() =>
        _profiles.Select(item => new MascotCharacterProfileEntry
        {
            Id = item.Key,
            Name = item.Value.Name,
            Role = item.Value.Role,
            ImageFolder = item.Value.ImageFolder,
            AccentColor = item.Value.AccentColor,
            IsActive = string.Equals(item.Value.Name, _activeProfile.Name, StringComparison.OrdinalIgnoreCase),
            UpdatedAt = DateTime.UtcNow
        }).ToArray();

    public MascotCharacterProfile? LoadProfile(string id) =>
        _profiles.TryGetValue(id, out var profile) ? profile.Clone() : null;

    public MascotCharacterProfileEntry SaveProfile(MascotCharacterProfile profile) => new();
    public MascotCharacterProfileEntry SaveProfileAs(MascotCharacterProfile profile, string name) => new();
    public bool DeleteProfile(string id) => false;
}

file sealed class CharacterAssetTestRoot : IDisposable
{
    private CharacterAssetTestRoot(string rootPath) => RootPath = rootPath;

    public string RootPath { get; }

    public static CharacterAssetTestRoot Create(params (string FolderName, string ImageName)[] assets)
    {
        var root = Path.Combine(Path.GetTempPath(), "DesktopMascotCharacterAssets", Guid.NewGuid().ToString("N"));
        foreach (var (folderName, imageName) in assets)
        {
            var directory = Path.Combine(root, "assets", "characters", folderName);
            Directory.CreateDirectory(directory);
            File.WriteAllBytes(Path.Combine(directory, imageName), [0]);
        }

        return new CharacterAssetTestRoot(root);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(RootPath))
            {
                Directory.Delete(RootPath, recursive: true);
            }
        }
        catch
        {
        }
    }
}

file sealed class StubCharacterImageService : ICharacterImageService
{
    public CharacterImageResult Resolve(MascotCharacterProfile profile, MascotState state) => new();
}

file sealed class StubTaskResultActionService : ITaskResultActionService
{
    public Task<bool> CopyToClipboardAsync(string text) => Task.FromResult(true);
    public Task<string> SaveResultAsync(string title, string content) => Task.FromResult(string.Empty);
}

file sealed class StubConfirmationHandler : IConfirmationHandler
{
    public Task<PermissionResponse> RequestConfirmationAsync(PermissionRequest request, CancellationToken ct = default) =>
        Task.FromResult(new PermissionResponse { RequestId = request.Id, Decision = PermissionDecision.AllowOnce });
}

file sealed class StubMemoryConfirmationHandler : IMemoryConfirmationHandler
{
    public Task<bool> RequestConfirmationAsync(MemoryConfirmRequest request, CancellationToken ct = default) =>
        Task.FromResult(true);
}

file sealed class StubConfigurationManager : IConfigurationManager
{
    public Task<AppSettings> GetAppSettingsAsync(CancellationToken ct = default) => Task.FromResult(new AppSettings());
    public Task SaveAppSettingsAsync(AppSettings settings, CancellationToken ct = default) => Task.CompletedTask;
    public Task<UserPreferences> GetUserPreferencesAsync(CancellationToken ct = default) => Task.FromResult(new UserPreferences());
    public Task SaveUserPreferencesAsync(UserPreferences preferences, CancellationToken ct = default) => Task.CompletedTask;
    public Task<ProjectSettings?> GetProjectSettingsAsync(string projectId, CancellationToken ct = default) => Task.FromResult<ProjectSettings?>(null);
    public Task SaveProjectSettingsAsync(ProjectSettings settings, CancellationToken ct = default) => Task.CompletedTask;
    public Task<PermissionSettings> GetPermissionSettingsAsync(CancellationToken ct = default) => Task.FromResult(new PermissionSettings());
    public Task SavePermissionSettingsAsync(PermissionSettings settings, CancellationToken ct = default) => Task.CompletedTask;
    public Task ResetToDefaultsAsync(CancellationToken ct = default) => Task.CompletedTask;
}

file sealed class StubSettingsDiagnosticsService : ISettingsDiagnosticsService
{
    public Task<SettingsDiagnosticsResult> TestModelConnectionAsync(ModelConnectionTestRequest request, CancellationToken ct = default) =>
        Task.FromResult(new SettingsDiagnosticsResult(true, "ok", string.Empty));

    public Task<SettingsDiagnosticsResult> TestMimoCodeAsync(MimoCodeConnectionTestRequest request, CancellationToken ct = default) =>
        Task.FromResult(new SettingsDiagnosticsResult(true, "ok", string.Empty));
}

file sealed class StubOnboardingWindowService : IOnboardingWindowService
{
    public Task ShowOnboardingWindowAsync(CancellationToken ct = default) => Task.CompletedTask;
}

file sealed class StubCharacterAssetImportService : ICharacterAssetImportService
{
    public CharacterAssetImportResult ImportToAppData(MascotCharacterProfile profile) =>
        new() { Success = true, Profile = profile };
}

file sealed class StubGlobalHotkeyService : IGlobalHotkeyService
{
    public event EventHandler? HotkeyPressed { add { } remove { } }
    public event EventHandler? ScreenSelectionHotkeyPressed { add { } remove { } }
    public event EventHandler? HotkeysChanged { add { } remove { } }
    public string DisplayText => HotkeyGesture.DefaultChat().DisplayText;
    public string ScreenSelectionDisplayText => HotkeyGesture.DefaultScreenSelection().DisplayText;
    public HotkeyGesture ChatHotkey => HotkeyGesture.DefaultChat();
    public HotkeyGesture ScreenSelectionHotkey => HotkeyGesture.DefaultScreenSelection();
    public bool IsDefaultHotkeyRegistered => true;
    public bool IsScreenSelectionHotkeyRegistered => true;
    public bool RegisterDefaultHotkey() => true;
    public HotkeyUpdateResult UpdateHotkeys(HotkeyGesture chatHotkey, HotkeyGesture screenSelectionHotkey) =>
        HotkeyUpdateResult.Succeeded("ok", true, true);
    public HotkeyUpdateResult ResetHotkeys() => HotkeyUpdateResult.Succeeded("ok", true, true);
    public void Dispose() { }
}

file sealed class StubPermissionManager : IPermissionManager
{
    public Task<PermissionResponse> RequestPermissionAsync(PermissionRequest request, CancellationToken ct = default) =>
        Task.FromResult(new PermissionResponse { RequestId = request.Id, Decision = PermissionDecision.AllowOnce });

    public Task<bool> HasPermissionAsync(string operation, PermissionLevel level, CancellationToken ct = default) =>
        Task.FromResult(true);

    public Task GrantPermanentPermissionAsync(string operation, PermissionLevel level, CancellationToken ct = default) =>
        Task.CompletedTask;

    public Task RevokePermissionAsync(string operation, CancellationToken ct = default) => Task.CompletedTask;
    public Task<List<AuditLogEntry>> GetAuditLogsAsync(int limit = 100, CancellationToken ct = default) => Task.FromResult(new List<AuditLogEntry>());
    public Task LogAuditAsync(AuditLogEntry entry, CancellationToken ct = default) => Task.CompletedTask;
}

file sealed class StubAuditLogStore : IAuditLogStore
{
    public Task SaveAsync(AuditLogEntry entry, CancellationToken ct = default) => Task.CompletedTask;
    public Task<List<AuditLogEntry>> GetLogsAsync(int limit = 100, CancellationToken ct = default) => Task.FromResult(new List<AuditLogEntry>());
    public Task<List<AuditLogEntry>> GetLogsByTaskAsync(string taskId, CancellationToken ct = default) => Task.FromResult(new List<AuditLogEntry>());
    public Task<int> GetTotalCountAsync(CancellationToken ct = default) => Task.FromResult(0);
}

file sealed class StubMemoryStore : IMemoryStore
{
    public Task<MemoryEntry> SaveAsync(MemoryEntry entry, CancellationToken ct = default) => Task.FromResult(entry);
    public Task<MemoryEntry?> GetByIdAsync(string id, CancellationToken ct = default) => Task.FromResult<MemoryEntry?>(null);
    public Task<MemoryEntry?> GetByKeyAsync(string key, MemoryType type, CancellationToken ct = default) => Task.FromResult<MemoryEntry?>(null);
    public Task<bool> DeleteAsync(string id, CancellationToken ct = default) => Task.FromResult(false);
    public Task<MemorySearchResult> SearchAsync(string query, MemoryType? type = null, int limit = 50, CancellationToken ct = default) => Task.FromResult(new MemorySearchResult());
    public Task<List<MemoryEntry>> GetByTypeAsync(MemoryType type, int limit = 100, CancellationToken ct = default) => Task.FromResult(new List<MemoryEntry>());
    public Task<bool> ConfirmAsync(string id, CancellationToken ct = default) => Task.FromResult(true);
    public Task<MemoryStatistics> GetStatisticsAsync(CancellationToken ct = default) => Task.FromResult(new MemoryStatistics());
    public Task<string> ExportAsync(MemoryType? type = null, CancellationToken ct = default) => Task.FromResult("[]");
    public Task<int> ImportAsync(string data, CancellationToken ct = default) => Task.FromResult(0);
}

file sealed class StubTaskHistoryStore : ITaskHistoryStore
{
    public Task<TaskHistoryRecord> SaveTaskAsync(TaskHistoryRecord record, CancellationToken ct = default) => Task.FromResult(record);
    public Task<TaskHistoryRecord?> UpdateTaskAsync(TaskHistoryRecord record, CancellationToken ct = default) => Task.FromResult<TaskHistoryRecord?>(record);
    public Task<TaskHistoryRecord?> GetTaskAsync(string taskId, CancellationToken ct = default) => Task.FromResult<TaskHistoryRecord?>(null);
    public Task<bool> DeleteTaskAsync(string taskId, CancellationToken ct = default) => Task.FromResult(false);
    public Task<TaskHistorySearchResult> SearchTasksAsync(string query, int limit = 50, CancellationToken ct = default) => Task.FromResult(new TaskHistorySearchResult());
    public Task<List<TaskHistoryRecord>> GetRecentTasksAsync(int limit = 20, CancellationToken ct = default) => Task.FromResult(new List<TaskHistoryRecord>());
    public Task<ToolCallRecord> SaveToolCallAsync(ToolCallRecord record, CancellationToken ct = default) => Task.FromResult(record);
    public Task<List<ToolCallRecord>> GetToolCallsAsync(string taskId, CancellationToken ct = default) => Task.FromResult(new List<ToolCallRecord>());
    public Task<TaskHistoryStatistics> GetStatisticsAsync(CancellationToken ct = default) => Task.FromResult(new TaskHistoryStatistics());
    public Task<string> ExportAsync(DateTime? from = null, DateTime? to = null, CancellationToken ct = default) => Task.FromResult("[]");
    public Task<int> CleanupAsync(int keepDays = 30, CancellationToken ct = default) => Task.FromResult(0);
}
