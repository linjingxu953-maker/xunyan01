using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Media;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DesktopMascot.Core.Enums;
using DesktopMascot.Core.Interfaces;
using DesktopMascot.Core.Memory;
using DesktopMascot.Core.Models;
using DesktopMascot.Core.Security;
using DesktopMascot.UI.Services;

namespace DesktopMascot.UI.ViewModels;

/// <summary>
/// 悬浮窗口 ViewModel
/// </summary>
public partial class FloatingWindowViewModel : ObservableObject, IDisposable
{
    private readonly ITaskRouter _taskRouter;
    private readonly ITaskEventBus _eventBus;
    private readonly ITaskEventStream _eventStream;
    private readonly IMascotCharacterStore _characterStore;
    private readonly ICharacterImageService _characterImageService;
    private readonly ITaskResultActionService _taskResultActionService;
    private readonly IConfirmationHandler _confirmationHandler;
    private readonly IMemoryConfirmationHandler _memoryConfirmationHandler;
    private readonly IDisposable _eventStreamSubscription;
    private readonly HashSet<string> _appliedEventIds = new(StringComparer.Ordinal);
    private readonly Queue<string> _appliedEventIdOrder = new();
    private string? _pendingResponseMessage;
    private AgentTask? _pendingConfirmationTask;
    private string _pendingConfirmationInput = string.Empty;
    private string _lastUserMessage = string.Empty;
    private Dictionary<string, string> _characterStateImages = new();
    private bool _isApplyingCharacterProfile;

    [ObservableProperty] private MascotState _currentState = MascotState.Idle;
    [ObservableProperty] private string _currentStateText = "空闲";
    [ObservableProperty] private string _statusMessage = "点击小人开始";
    [ObservableProperty] private string _stateHint = "待命";
    [ObservableProperty] private string _mascotMoodText = "Ready";
    [ObservableProperty] private int _currentProgress = 0;
    [ObservableProperty] private bool _isTaskActive = false;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanUseTaskResultActions))]
    [NotifyPropertyChangedFor(nameof(CanRetryCurrentTask))]
    private bool _isBusy = false;
    [ObservableProperty] private bool _hasTaskDetails = false;
    [ObservableProperty] private bool _canCancelTask = false;
    [ObservableProperty] private string _activeTaskId = string.Empty;
    [ObservableProperty] private string _activeTaskTitle = "当前任务";
    [ObservableProperty] private string _activeTaskTypeText = "普通问答";
    [ObservableProperty] private string _taskSummary = "暂无任务";
    [ObservableProperty] private string _activeStepText = "等待任务";
    [ObservableProperty] private string _taskProgressText = "0%";
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNoTaskResult))]
    [NotifyPropertyChangedFor(nameof(CanUseTaskResultActions))]
    private bool _hasTaskResult = false;
    [ObservableProperty] private string _taskResultPreview = "任务完成后会在这里显示结果。";
    [ObservableProperty] private string _taskResultStatusText = "暂无结果";
    [ObservableProperty] private string _taskActionStatus = "等待任务执行。";
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanRetryCurrentTask))]
    private bool _canRetryTask = false;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNoToolCallRecords))]
    private bool _hasToolCallRecords = false;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanRetryCurrentTask))]
    private bool _isWaitingForUserConfirmation = false;
    [ObservableProperty] private string _pendingConfirmationTitle = "等待确认";
    [ObservableProperty] private string _pendingConfirmationDescription = "请确认后继续执行。";
    [ObservableProperty] private string _pendingConfirmationRiskText = "低风险";
    [ObservableProperty] private IBrush _stateAccentBrush = BrushFrom("#2563EB");
    [ObservableProperty] private IBrush _mascotBackgroundBrush = BrushFrom("#EEF6FF");
    [ObservableProperty] private bool _isChatVisible = false;
    [ObservableProperty] private bool _isCharacterPanelVisible = false;
    [ObservableProperty] private string _inputText = string.Empty;
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
    [ObservableProperty] private IImage? _characterImageSource;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNoCharacterImage))]
    private bool _hasCharacterImage;
    [ObservableProperty] private string _characterSaveStatus = "角色配置会自动保存在本机。";
    
    /// <summary>
    /// 主区域是否响应点击（聊天面板打开时禁用，避免干扰拖动）
    /// </summary>
    [ObservableProperty] private bool _isMainAreaHitTestVisible = true;
    
    /// <summary>
    /// 聊天面板是否响应点击
    /// </summary>
    [ObservableProperty] private bool _isChatPanelHitTestVisible = true;

    public ObservableCollection<string> Messages { get; } = new();
    public ObservableCollection<TaskTimelineItem> TaskTimeline { get; } = new();
    public ObservableCollection<TaskToolCallItem> TaskToolCalls { get; } = new();

    public bool CanSendMessage => !IsBusy && !IsWaitingForUserConfirmation && !string.IsNullOrWhiteSpace(InputText);
    public bool CanStartScreenSelection => !IsBusy && !IsWaitingForUserConfirmation;
    public bool HasNoCharacterImage => !HasCharacterImage;
    public bool HasNoTaskResult => !HasTaskResult;
    public bool HasNoToolCallRecords => !HasToolCallRecords;
    public bool CanUseTaskResultActions => HasTaskResult && !IsBusy;
    public bool CanRetryCurrentTask => CanRetryTask && !IsBusy && !IsWaitingForUserConfirmation && !string.IsNullOrWhiteSpace(_lastUserMessage);
    public bool CanResolvePendingConfirmation => IsWaitingForUserConfirmation && _pendingConfirmationTask is not null && !IsBusy;

    public event EventHandler? HideRequested;
    public event EventHandler? ExitRequested;
    public event EventHandler? SettingsRequested;
    public event EventHandler? AppearanceSettingsRequested;
    public event EventHandler? ScreenSelectionRequested;

    public FloatingWindowViewModel(
        ITaskRouter taskRouter,
        ITaskEventBus eventBus,
        ITaskEventStream eventStream,
        IMascotCharacterStore characterStore,
        ICharacterImageService characterImageService,
        ITaskResultActionService taskResultActionService,
        IConfirmationHandler confirmationHandler,
        IMemoryConfirmationHandler memoryConfirmationHandler)
    {
        _taskRouter = taskRouter;
        _eventBus = eventBus;
        _eventStream = eventStream;
        _characterStore = characterStore;
        _characterImageService = characterImageService;
        _taskResultActionService = taskResultActionService;
        _confirmationHandler = confirmationHandler;
        _memoryConfirmationHandler = memoryConfirmationHandler;

        _characterStore.ProfileChanged += OnCharacterProfileChanged;
        ApplyCharacterProfile(_characterStore.Load(), save: false);

        // 监听任务事件
        _eventBus.TaskEventPublished += OnTaskEventPublished;
        _eventStreamSubscription = _eventStream.SubscribeAll().Subscribe(new TaskEventObserver(OnTaskStreamEvent));
    }

    private void OnTaskEventPublished(object? sender, TaskEvent e)
    {
        QueueTaskEvent(e);
    }

    private void OnTaskStreamEvent(TaskEvent e)
    {
        QueueTaskEvent(e);
    }

    private void QueueTaskEvent(TaskEvent e)
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            ApplyTaskEvent(e);
            return;
        }

        Dispatcher.UIThread.Post(() => ApplyTaskEvent(e));
    }

    private void ApplyTaskEvent(TaskEvent e)
    {
        if (!ShouldDisplayTaskEvent(e) || !TryMarkTaskEventApplied(e))
            return;

        var message = ResolveEventMessage(e);
        var progress = ResolveEventProgress(e);

        CurrentState = e.State;
        CurrentStateText = GetStateText(e.State);
        StatusMessage = message;
        CurrentProgress = progress;
        TaskProgressText = $"{CurrentProgress}%";
        ActiveStepText = GetEventStepText(e);
        StateHint = GetStateHint(e.State);
        MascotMoodText = GetMascotMoodText(e.State);
        StateAccentBrush = GetAccentBrush(e.State);
        MascotBackgroundBrush = GetMascotBackgroundBrush(e.State);
        IsWaitingForUserConfirmation = e.State is MascotState.WaitingApproval or MascotState.MemoryConfirm;
        ApplyConfirmationStateFromEvent(e, message);
        ApplyTaskResultStateFromEvent(e, message);
        RefreshCharacterImage();
        AddToolCallRecord(e);

        if (e.State != MascotState.Idle)
        {
            HasTaskDetails = true;
            AddTimelineItem(e.State, message, CurrentProgress, e.CreatedAt);
            TrimTimeline();
        }
    }

    partial void OnIsChatVisibleChanged(bool value)
    {
        // 聊天面板打开时，主区域不再响应点击，避免干扰
        IsMainAreaHitTestVisible = !value;
    }

    partial void OnInputTextChanged(string value)
    {
        SendMessageCommand.NotifyCanExecuteChanged();
    }

    partial void OnIsBusyChanged(bool value)
    {
        SendMessageCommand.NotifyCanExecuteChanged();
        StartScreenSelectionCommand.NotifyCanExecuteChanged();
        CopyTaskResultCommand.NotifyCanExecuteChanged();
        SaveTaskResultCommand.NotifyCanExecuteChanged();
        RetryTaskCommand.NotifyCanExecuteChanged();
        ApprovePendingTaskCommand.NotifyCanExecuteChanged();
        DenyPendingTaskCommand.NotifyCanExecuteChanged();
    }

    partial void OnCanCancelTaskChanged(bool value)
    {
        CancelCurrentTaskCommand.NotifyCanExecuteChanged();
    }

    partial void OnHasTaskResultChanged(bool value)
    {
        CopyTaskResultCommand.NotifyCanExecuteChanged();
        SaveTaskResultCommand.NotifyCanExecuteChanged();
    }

    partial void OnCanRetryTaskChanged(bool value)
    {
        RetryTaskCommand.NotifyCanExecuteChanged();
    }

    partial void OnIsWaitingForUserConfirmationChanged(bool value)
    {
        SendMessageCommand.NotifyCanExecuteChanged();
        StartScreenSelectionCommand.NotifyCanExecuteChanged();
        RetryTaskCommand.NotifyCanExecuteChanged();
        ApprovePendingTaskCommand.NotifyCanExecuteChanged();
        DenyPendingTaskCommand.NotifyCanExecuteChanged();
    }

    partial void OnCharacterImageFolderChanged(string value)
    {
        if (!_isApplyingCharacterProfile)
        {
            RefreshCharacterImage();
        }
    }

    partial void OnCharacterAvatarImageChanged(string value)
    {
        if (!_isApplyingCharacterProfile)
        {
            RefreshCharacterImage();
        }
    }

    [RelayCommand]
    private void ToggleChat()
    {
        IsChatVisible = !IsChatVisible;
    }

    [RelayCommand]
    private void CloseChat()
    {
        IsChatVisible = false;
        IsCharacterPanelVisible = false;
    }

    [RelayCommand]
    private void ToggleCharacterPanel()
    {
        IsCharacterPanelVisible = !IsCharacterPanelVisible;
        IsChatVisible = true;
    }

    [RelayCommand]
    private void OpenAppearanceSettings()
    {
        IsCharacterPanelVisible = false;
        AppearanceSettingsRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnCharacterProfileChanged(object? sender, EventArgs e)
    {
        if (_isApplyingCharacterProfile)
            return;

        ApplyCharacterProfile(_characterStore.Load(), save: false);
        CharacterSaveStatus = "角色外观已从设置中心更新。";
    }

    [RelayCommand(CanExecute = nameof(CanSendMessage))]
    private async Task SendMessage()
    {
        if (string.IsNullOrWhiteSpace(InputText))
            return;

        var userMessage = InputText;
        InputText = string.Empty;
        Messages.Add($"你：{userMessage}");

        var metadata = BuildTaskMetadata(userMessage);
        var task = new AgentTask
        {
            Title = metadata.Title,
            Input = userMessage,
            Type = metadata.Type,
            RequiredPermission = metadata.Permission
        };

        PrepareTaskSurface(task, metadata.TypeText, userMessage);

        if (RequiresUserConfirmation(task))
        {
            var confirmed = await ShowPendingConfirmationAsync(task, userMessage);
            if (!confirmed)
                return;
        }

        await ExecutePreparedTaskAsync(task);
    }

    [RelayCommand(CanExecute = nameof(CanCancelCurrentTask))]
    private void CancelCurrentTask()
    {
        if (string.IsNullOrWhiteSpace(ActiveTaskId))
            return;

        if (_taskRouter.CancelTask(ActiveTaskId))
        {
            CanCancelTask = false;
            StatusMessage = "正在中断任务...";
            Messages.Add($"{CharacterName}：已请求中断当前任务。");
        }
    }

    [RelayCommand(CanExecute = nameof(CanResolvePendingConfirmation))]
    private async Task ApprovePendingTask()
    {
        if (_pendingConfirmationTask is null)
            return;

        var task = _pendingConfirmationTask;
        _pendingConfirmationTask = null;
        _pendingConfirmationInput = string.Empty;
        IsWaitingForUserConfirmation = false;
        TaskActionStatus = "已确认，开始执行任务。";
        Messages.Add($"{CharacterName}：已确认，开始执行。");

        await ExecutePreparedTaskAsync(task);
    }

    [RelayCommand(CanExecute = nameof(CanResolvePendingConfirmation))]
    private void DenyPendingTask()
    {
        if (_pendingConfirmationTask is null)
            return;

        _pendingConfirmationTask = null;
        _pendingConfirmationInput = string.Empty;
        IsWaitingForUserConfirmation = false;
        IsTaskActive = false;
        CanCancelTask = false;
        IsBusy = false;
        CurrentState = MascotState.Error;
        CurrentStateText = GetStateText(CurrentState);
        CurrentProgress = 100;
        TaskProgressText = "100%";
        ActiveStepText = "已拒绝";
        StateHint = GetStateHint(CurrentState);
        StateAccentBrush = GetAccentBrush(CurrentState);
        MascotBackgroundBrush = GetMascotBackgroundBrush(CurrentState);
        StatusMessage = "用户拒绝了操作";
        HasTaskResult = true;
        TaskResultPreview = "用户拒绝了操作。";
        TaskResultStatusText = "已拒绝";
        TaskActionStatus = "操作已拒绝，未执行任务。";
        CanRetryTask = true;

        AddTimelineItem(MascotState.Error, "用户拒绝了操作", CurrentProgress, DateTime.UtcNow);
        AddToolCallRecord("权限确认", "已拒绝", PendingConfirmationRiskText, DateTime.UtcNow);
        Messages.Add($"{CharacterName}：已取消该操作。");
    }

    [RelayCommand(CanExecute = nameof(CanUseTaskResultActions))]
    private async Task CopyTaskResult()
    {
        var copied = await _taskResultActionService.CopyToClipboardAsync(TaskResultPreview);
        TaskActionStatus = copied ? "结果已复制到剪贴板。" : "复制失败：当前没有可用剪贴板。";
    }

    [RelayCommand(CanExecute = nameof(CanUseTaskResultActions))]
    private async Task SaveTaskResult()
    {
        var content = BuildTaskResultDocument();
        var path = await _taskResultActionService.SaveResultAsync(ActiveTaskTitle, content);
        TaskActionStatus = $"结果已保存：{path}";
    }

    [RelayCommand(CanExecute = nameof(CanRetryCurrentTask))]
    private async Task RetryTask()
    {
        if (string.IsNullOrWhiteSpace(_lastUserMessage))
            return;

        var metadata = BuildTaskMetadata(_lastUserMessage);
        var task = new AgentTask
        {
            Title = metadata.Title,
            Input = _lastUserMessage,
            Type = metadata.Type,
            RequiredPermission = metadata.Permission
        };

        PrepareTaskSurface(task, metadata.TypeText, _lastUserMessage);

        if (RequiresUserConfirmation(task))
        {
            var confirmed = await ShowPendingConfirmationAsync(task, _lastUserMessage);
            if (!confirmed)
                return;
        }

        await ExecutePreparedTaskAsync(task);
    }

    [RelayCommand]
    private void SaveCharacter()
    {
        var profile = BuildCurrentCharacterProfile();
        ApplyCharacterProfile(profile, save: true);
        CharacterSaveStatus = $"已保存 {CharacterName} 的角色设定。";
    }

    [RelayCommand]
    private void ResetCharacter()
    {
        ApplyCharacterProfile(new MascotCharacterProfile(), save: true);
        CharacterSaveStatus = "已恢复默认角色。";
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

        ApplyCharacterProfile(profile, save: true);
        CharacterSaveStatus = $"已切换到 {CharacterName}。";
    }

    [RelayCommand]
    private void ChooseAccentColor(string? color)
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
    private void HideWindow()
    {
        HideRequested?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void Exit()
    {
        ExitRequested?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void OpenSettings()
    {
        SettingsRequested?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand(CanExecute = nameof(CanStartScreenSelection))]
    private void StartScreenSelection()
    {
        RequestScreenSelection();
    }

    public void RequestScreenSelection()
    {
        if (!CanStartScreenSelection)
            return;

        StatusMessage = "拖动鼠标圈选要理解的屏幕区域。";
        ScreenSelectionRequested?.Invoke(this, EventArgs.Empty);
    }

    public async Task AnalyzeSelectedScreenRegionAsync(ScreenSelectionResult result)
    {
        if (!result.HasRegion)
            return;

        var userMessage = $"请分析我圈选的屏幕区域 {result.Summary}";
        Messages.Add($"你：{userMessage}");

        var task = new AgentTask
        {
            Title = "屏幕区域理解",
            Input = userMessage,
            Type = TaskType.ScreenUnderstand,
            RequiredPermission = PermissionLevel.L2_ScreenBrowser,
            Parameters =
            {
                ["Region"] = new
                {
                    region = new
                    {
                        x = result.X,
                        y = result.Y,
                        width = result.Width,
                        height = result.Height
                    }
                },
                ["UserHint"] = "用户圈选的屏幕区域"
            }
        };

        PrepareTaskSurface(task, "屏幕理解", userMessage);

        if (RequiresUserConfirmation(task))
        {
            var confirmed = await ShowPendingConfirmationAsync(task, userMessage);
            if (!confirmed)
                return;
        }

        await ExecutePreparedTaskAsync(task);
    }

    public void CancelScreenSelection()
    {
        StatusMessage = "已取消屏幕圈选。";
        IsChatVisible = true;
    }

    public void OpenChatPanel()
    {
        IsChatVisible = true;
    }

    private bool CanCancelCurrentTask() => CanCancelTask;

    private void PrepareTaskSurface(AgentTask task, string typeText, string userMessage)
    {
        _lastUserMessage = userMessage;
        _pendingConfirmationTask = null;
        _pendingConfirmationInput = string.Empty;
        IsWaitingForUserConfirmation = false;

        ActiveTaskId = task.Id;
        ActiveTaskTitle = task.Title;
        ActiveTaskTypeText = typeText;
        TaskSummary = userMessage;
        ActiveStepText = "任务已创建";
        CurrentProgress = 0;
        TaskProgressText = "0%";
        IsTaskActive = true;
        IsBusy = false;
        CanCancelTask = false;
        HasTaskDetails = true;
        HasTaskResult = false;
        TaskResultPreview = "任务完成后会在这里显示结果。";
        TaskResultStatusText = "执行中";
        TaskActionStatus = "任务已创建。";
        CanRetryTask = false;
        TaskTimeline.Clear();
        TaskToolCalls.Clear();
        HasToolCallRecords = false;
        IsChatVisible = true;

        OnPropertyChanged(nameof(CanRetryCurrentTask));
        RetryTaskCommand.NotifyCanExecuteChanged();
        ApprovePendingTaskCommand.NotifyCanExecuteChanged();
        DenyPendingTaskCommand.NotifyCanExecuteChanged();
    }

    private async Task ExecutePreparedTaskAsync(AgentTask task)
    {
        IsWaitingForUserConfirmation = false;
        IsTaskActive = true;
        IsBusy = true;
        CanCancelTask = true;
        TaskActionStatus = "任务正在执行。";
        _pendingResponseMessage = $"{CharacterName}：正在处理...";
        Messages.Add(_pendingResponseMessage);

        try
        {
            var result = await _taskRouter.DispatchAsync(task);

            RemovePendingResponse();
            HasTaskResult = true;
            CanRetryTask = !result.Success;
            CurrentProgress = 100;
            TaskProgressText = "100%";
            ActiveStepText = result.Success ? "已完成" : "执行失败";
            TaskResultStatusText = result.Success ? "已完成" : "执行失败";
            TaskResultPreview = ResolveResultText(result);
            TaskActionStatus = result.Success ? "任务完成，可以复制或保存结果。" : "任务失败，可以重试。";

            if (result.Success)
            {
                Messages.Add($"{CharacterName}：{result.Content}");
            }
            else
            {
                Messages.Add($"{CharacterName}：{TaskResultPreview}");
            }
        }
        finally
        {
            IsBusy = false;
            IsTaskActive = false;
            CanCancelTask = false;
            CopyTaskResultCommand.NotifyCanExecuteChanged();
            SaveTaskResultCommand.NotifyCanExecuteChanged();
            RetryTaskCommand.NotifyCanExecuteChanged();
        }
    }

    private async Task<bool> ShowPendingConfirmationAsync(AgentTask task, string userMessage)
    {
        _pendingConfirmationTask = task;
        _pendingConfirmationInput = userMessage;

        CurrentState = task.Type == TaskType.UpdateMemory ? MascotState.MemoryConfirm : MascotState.WaitingApproval;
        CurrentStateText = GetStateText(CurrentState);
        StatusMessage = GetDefaultStatusMessage(CurrentState);
        CurrentProgress = ResolveProgress(CurrentState, -1);
        TaskProgressText = $"{CurrentProgress}%";
        ActiveStepText = GetStateText(CurrentState);
        StateHint = GetStateHint(CurrentState);
        MascotMoodText = GetMascotMoodText(CurrentState);
        StateAccentBrush = GetAccentBrush(CurrentState);
        MascotBackgroundBrush = GetMascotBackgroundBrush(CurrentState);
        RefreshCharacterImage();

        PendingConfirmationTitle = GetConfirmationTitle(task);
        PendingConfirmationDescription = GetConfirmationDescription(task);
        PendingConfirmationRiskText = GetConfirmationRiskText(task);
        IsWaitingForUserConfirmation = true;
        TaskActionStatus = "等待你确认后继续执行。";

        AddTimelineItem(CurrentState, PendingConfirmationDescription, CurrentProgress, DateTime.UtcNow);
        AddToolCallRecord(GetConfirmationToolName(task), "等待确认", PendingConfirmationRiskText, DateTime.UtcNow);
        Messages.Add($"{CharacterName}：这个任务需要你确认后继续。");

        ApprovePendingTaskCommand.NotifyCanExecuteChanged();
        DenyPendingTaskCommand.NotifyCanExecuteChanged();

        var confirmed = await RequestExternalConfirmationAsync(task, userMessage);
        _pendingConfirmationTask = null;
        _pendingConfirmationInput = string.Empty;
        IsWaitingForUserConfirmation = false;

        if (confirmed)
        {
            TaskActionStatus = "已确认，开始执行任务。";
            AddToolCallRecord(GetConfirmationToolName(task), "已确认", PendingConfirmationRiskText, DateTime.UtcNow);
            Messages.Add($"{CharacterName}：已确认，开始执行。");
            return true;
        }

        ApplyDeniedConfirmation(task);
        return false;
    }

    private async Task<bool> RequestExternalConfirmationAsync(AgentTask task, string userMessage)
    {
        if (task.Type == TaskType.UpdateMemory)
        {
            return await _memoryConfirmationHandler.RequestConfirmationAsync(new MemoryConfirmRequest
            {
                ProposedMemory = new MemoryEntry
                {
                    Type = MemoryType.User,
                    Key = BuildMemoryKey(userMessage),
                    Content = userMessage,
                    Source = "桌面悬浮窗任务输入",
                    TaskId = task.Id,
                    Tags =
                    {
                        ["taskType"] = task.Type.ToString(),
                        ["source"] = "ui"
                    }
                },
                Reason = "用户请求保存为后续可复用的记忆。"
            });
        }

        var response = await _confirmationHandler.RequestConfirmationAsync(new PermissionRequest
        {
            TaskId = task.Id,
            Level = task.RequiredPermission,
            Title = GetConfirmationTitle(task),
            Description = GetConfirmationDescription(task),
            Target = GetConfirmationTarget(task, userMessage),
            Risk = GetConfirmationRiskText(task),
            Metadata =
            {
                ["任务类型"] = task.Type.ToString(),
                ["任务标题"] = task.Title,
                ["用户输入"] = userMessage,
                ["权限等级"] = task.RequiredPermission.ToString()
            }
        });

        return response.Decision != PermissionDecision.Deny;
    }

    private void ApplyDeniedConfirmation(AgentTask task)
    {
        IsTaskActive = false;
        CanCancelTask = false;
        IsBusy = false;
        CurrentState = MascotState.Error;
        CurrentStateText = GetStateText(CurrentState);
        CurrentProgress = 100;
        TaskProgressText = "100%";
        ActiveStepText = "已拒绝";
        StateHint = GetStateHint(CurrentState);
        StateAccentBrush = GetAccentBrush(CurrentState);
        MascotBackgroundBrush = GetMascotBackgroundBrush(CurrentState);
        StatusMessage = "用户拒绝了操作";
        HasTaskResult = true;
        TaskResultPreview = "用户拒绝了操作。";
        TaskResultStatusText = "已拒绝";
        TaskActionStatus = "操作已拒绝，未执行任务。";
        CanRetryTask = true;

        AddTimelineItem(MascotState.Error, "用户拒绝了操作", CurrentProgress, DateTime.UtcNow);
        AddToolCallRecord(GetConfirmationToolName(task), "已拒绝", PendingConfirmationRiskText, DateTime.UtcNow);
        Messages.Add($"{CharacterName}：已取消该操作。");
    }

    private static bool RequiresUserConfirmation(AgentTask task)
    {
        return task.Type is TaskType.WriteFile or TaskType.RunCommand or TaskType.UpdateMemory ||
               task.RequiredPermission >= PermissionLevel.L4_FileWrite;
    }

    private bool ShouldDisplayTaskEvent(TaskEvent taskEvent)
    {
        if (string.IsNullOrWhiteSpace(taskEvent.TaskId))
            return true;

        if (string.IsNullOrWhiteSpace(ActiveTaskId))
        {
            PrepareEventDrivenTaskSurface(taskEvent);
            return true;
        }

        if (string.Equals(taskEvent.TaskId, ActiveTaskId, StringComparison.Ordinal))
            return true;

        if (taskEvent.EventType == TaskEventType.TaskStarted && !IsTaskActive && !IsBusy)
        {
            PrepareEventDrivenTaskSurface(taskEvent);
            return true;
        }

        return false;
    }

    private void PrepareEventDrivenTaskSurface(TaskEvent taskEvent)
    {
        ActiveTaskId = taskEvent.TaskId;
        ActiveTaskTitle = CleanText(taskEvent.Message, "事件流任务", 36);
        ActiveTaskTypeText = "事件流任务";
        TaskSummary = string.IsNullOrWhiteSpace(taskEvent.Message)
            ? "来自任务事件流。"
            : taskEvent.Message;
        HasTaskDetails = true;
        HasTaskResult = false;
        CanRetryTask = false;
        TaskResultPreview = "任务完成后会在这里显示结果。";
        TaskResultStatusText = "执行中";
        TaskActionStatus = "任务事件已接入 UI。";
        TaskTimeline.Clear();
        TaskToolCalls.Clear();
        HasToolCallRecords = false;
    }

    private bool TryMarkTaskEventApplied(TaskEvent taskEvent)
    {
        if (string.IsNullOrWhiteSpace(taskEvent.Id))
            return true;

        if (!_appliedEventIds.Add(taskEvent.Id))
            return false;

        _appliedEventIdOrder.Enqueue(taskEvent.Id);

        while (_appliedEventIdOrder.Count > 300)
        {
            _appliedEventIds.Remove(_appliedEventIdOrder.Dequeue());
        }

        return true;
    }

    private void ApplyConfirmationStateFromEvent(TaskEvent taskEvent, string message)
    {
        switch (taskEvent.EventType)
        {
            case TaskEventType.PermissionRequested:
                PendingConfirmationTitle = "等待权限确认";
                PendingConfirmationDescription = ResolvePermissionDescription(taskEvent, message);
                PendingConfirmationRiskText = GetMetadataString(taskEvent, "permissionType", "权限确认");
                IsWaitingForUserConfirmation = true;
                TaskActionStatus = "等待权限确认弹窗处理。";
                break;

            case TaskEventType.MemorySaveRequested:
                PendingConfirmationTitle = "等待记忆确认";
                PendingConfirmationDescription = GetMetadataString(taskEvent, "content", message);
                PendingConfirmationRiskText = GetMetadataString(taskEvent, "memoryType", "记忆保存");
                IsWaitingForUserConfirmation = true;
                TaskActionStatus = "等待记忆确认弹窗处理。";
                break;

            case TaskEventType.PermissionGranted:
            case TaskEventType.MemorySaveCompleted:
                IsWaitingForUserConfirmation = false;
                TaskActionStatus = "确认已通过，任务继续执行。";
                break;

            case TaskEventType.PermissionDenied:
            case TaskEventType.MemorySaveRejected:
                IsWaitingForUserConfirmation = false;
                TaskActionStatus = "确认已拒绝，任务未继续执行。";
                break;
        }
    }

    private void ApplyTaskResultStateFromEvent(TaskEvent taskEvent, string message)
    {
        switch (taskEvent.EventType)
        {
            case TaskEventType.TaskCompleted:
                HasTaskResult = true;
                TaskResultStatusText = "已完成";
                TaskResultPreview = message;
                TaskActionStatus = "任务完成，可以复制或保存结果。";
                CanRetryTask = false;
                if (!IsBusy)
                {
                    IsTaskActive = false;
                    CanCancelTask = false;
                }
                break;

            case TaskEventType.TaskFailed:
                HasTaskResult = true;
                TaskResultStatusText = "执行失败";
                TaskResultPreview = message;
                TaskActionStatus = "任务失败，可以重试。";
                CanRetryTask = true;
                if (!IsBusy)
                {
                    IsTaskActive = false;
                    CanCancelTask = false;
                }
                break;

            case TaskEventType.TaskCancelled:
                HasTaskResult = true;
                TaskResultStatusText = "已取消";
                TaskResultPreview = message;
                TaskActionStatus = "任务已取消。";
                CanRetryTask = true;
                if (!IsBusy)
                {
                    IsTaskActive = false;
                    CanCancelTask = false;
                }
                break;

            case TaskEventType.ToolCallStarted:
                TaskActionStatus = $"正在执行工具：{GetMetadataString(taskEvent, "toolName", "未知工具")}";
                break;

            case TaskEventType.ToolCallCompleted:
                TaskActionStatus = $"工具执行完成：{GetMetadataString(taskEvent, "toolName", "未知工具")}";
                break;

            case TaskEventType.ToolCallFailed:
                TaskActionStatus = $"工具执行失败：{GetMetadataString(taskEvent, "toolName", "未知工具")}";
                break;
        }
    }

    private static string ResolvePermissionDescription(TaskEvent taskEvent, string fallback)
    {
        var scope = GetMetadataString(taskEvent, "scope", string.Empty);
        var reason = GetMetadataString(taskEvent, "reason", fallback);

        if (string.IsNullOrWhiteSpace(scope))
            return reason;

        return $"{reason}：{scope}";
    }

    private static string ResolveEventMessage(TaskEvent taskEvent)
    {
        if (!string.IsNullOrWhiteSpace(taskEvent.Message))
            return taskEvent.Message;

        return taskEvent.EventType switch
        {
            TaskEventType.TaskStarted => "任务已开始",
            TaskEventType.TaskCompleted => "任务已完成",
            TaskEventType.TaskFailed => "任务执行失败",
            TaskEventType.TaskCancelled => "任务已取消",
            TaskEventType.ToolCallStarted => $"正在执行工具：{GetMetadataString(taskEvent, "toolName", "未知工具")}",
            TaskEventType.ToolCallCompleted => $"工具执行完成：{GetMetadataString(taskEvent, "toolName", "未知工具")}",
            TaskEventType.ToolCallFailed => $"工具执行失败：{GetMetadataString(taskEvent, "toolName", "未知工具")}",
            TaskEventType.PermissionRequested => "等待权限确认",
            TaskEventType.PermissionGranted => "权限已授权",
            TaskEventType.PermissionDenied => "权限已拒绝",
            TaskEventType.MemorySaveRequested => "等待记忆确认",
            TaskEventType.MemorySaveCompleted => "记忆已保存",
            TaskEventType.MemorySaveRejected => "记忆保存已拒绝",
            TaskEventType.ProgressUpdated => "任务进度更新",
            TaskEventType.ContextReadingStarted => "开始读取上下文",
            TaskEventType.ContextReadingCompleted => "上下文读取完成",
            TaskEventType.LlmCallStarted => "开始调用模型",
            TaskEventType.LlmCallCompleted => "模型调用完成",
            TaskEventType.LlmStreamChunk => "模型正在回复",
            _ => GetDefaultStatusMessage(taskEvent.State)
        };
    }

    private static int ResolveEventProgress(TaskEvent taskEvent)
    {
        return taskEvent.EventType switch
        {
            TaskEventType.TaskFailed or TaskEventType.TaskCancelled => 100,
            TaskEventType.ToolCallStarted or TaskEventType.ToolCallCompleted or TaskEventType.ToolCallFailed => ResolveProgress(taskEvent.State, -1),
            TaskEventType.PermissionRequested or TaskEventType.MemorySaveRequested => ResolveProgress(taskEvent.State, -1),
            _ => ResolveProgress(taskEvent.State, taskEvent.Progress)
        };
    }

    private static string GetEventStepText(TaskEvent taskEvent)
    {
        return taskEvent.EventType switch
        {
            TaskEventType.TaskStarted => "任务已开始",
            TaskEventType.TaskCompleted => "已完成",
            TaskEventType.TaskFailed => "执行失败",
            TaskEventType.TaskCancelled => "已取消",
            TaskEventType.StepChanged or TaskEventType.StepCompleted => ResolveEventMessage(taskEvent),
            TaskEventType.ToolCallStarted => $"执行工具：{GetMetadataString(taskEvent, "toolName", "未知工具")}",
            TaskEventType.ToolCallCompleted => $"工具完成：{GetMetadataString(taskEvent, "toolName", "未知工具")}",
            TaskEventType.ToolCallFailed => $"工具失败：{GetMetadataString(taskEvent, "toolName", "未知工具")}",
            TaskEventType.PermissionRequested => "等待权限确认",
            TaskEventType.PermissionGranted => "权限已授权",
            TaskEventType.PermissionDenied => "权限已拒绝",
            TaskEventType.MemorySaveRequested => "等待记忆确认",
            TaskEventType.MemorySaveCompleted => "记忆已保存",
            TaskEventType.MemorySaveRejected => "记忆已拒绝",
            TaskEventType.ProgressUpdated => ResolveEventMessage(taskEvent),
            TaskEventType.ContextReadingStarted => "读取上下文",
            TaskEventType.ContextReadingCompleted => "上下文已读取",
            TaskEventType.LlmCallStarted => "调用模型",
            TaskEventType.LlmCallCompleted => "模型已返回",
            TaskEventType.LlmStreamChunk => "接收模型回复",
            _ => GetStateText(taskEvent.State)
        };
    }

    private static string GetMetadataString(TaskEvent taskEvent, string key, string fallback)
    {
        if (taskEvent.Metadata is null || !taskEvent.Metadata.TryGetValue(key, out var value) || value is null)
            return fallback;

        var text = value.ToString();
        return string.IsNullOrWhiteSpace(text) ? fallback : text;
    }

    private void AddTimelineItem(MascotState state, string message, int progress, DateTime createdAt)
    {
        foreach (var item in TaskTimeline)
        {
            item.IsCurrent = false;
        }

        TaskTimeline.Add(new TaskTimelineItem(state, message, progress, createdAt, isCurrent: true));
    }

    private void AddToolCallRecord(TaskEvent taskEvent)
    {
        var detail = string.IsNullOrWhiteSpace(taskEvent.Message)
            ? GetDefaultStatusMessage(taskEvent.State)
            : taskEvent.Message;

        switch (taskEvent.EventType)
        {
            case TaskEventType.ToolCallStarted:
                AddToolCallRecord(
                    GetMetadataString(taskEvent, "toolName", "未知工具"),
                    "运行中",
                    GetMetadataString(taskEvent, "input", detail),
                    taskEvent.CreatedAt);
                return;

            case TaskEventType.ToolCallCompleted:
                AddToolCallRecord(
                    GetMetadataString(taskEvent, "toolName", "未知工具"),
                    "已完成",
                    GetMetadataString(taskEvent, "output", detail),
                    taskEvent.CreatedAt);
                return;

            case TaskEventType.ToolCallFailed:
                AddToolCallRecord(
                    GetMetadataString(taskEvent, "toolName", "未知工具"),
                    "失败",
                    GetMetadataString(taskEvent, "output", detail),
                    taskEvent.CreatedAt);
                return;

            case TaskEventType.PermissionRequested:
                AddToolCallRecord("权限确认", "等待确认", ResolvePermissionDescription(taskEvent, detail), taskEvent.CreatedAt);
                return;

            case TaskEventType.PermissionGranted:
                AddToolCallRecord("权限确认", "已授权", detail, taskEvent.CreatedAt);
                return;

            case TaskEventType.PermissionDenied:
                AddToolCallRecord("权限确认", "已拒绝", detail, taskEvent.CreatedAt);
                return;

            case TaskEventType.MemorySaveRequested:
                AddToolCallRecord("记忆确认", "等待确认", GetMetadataString(taskEvent, "content", detail), taskEvent.CreatedAt);
                return;

            case TaskEventType.MemorySaveCompleted:
                AddToolCallRecord("记忆保存", "已完成", detail, taskEvent.CreatedAt);
                return;

            case TaskEventType.MemorySaveRejected:
                AddToolCallRecord("记忆保存", "已拒绝", detail, taskEvent.CreatedAt);
                return;
        }

        switch (taskEvent.State)
        {
            case MascotState.Listening:
                AddToolCallRecord("任务入口", "已接收", detail, taskEvent.CreatedAt);
                break;
            case MascotState.Understanding:
                AddToolCallRecord("意图解析", "处理中", detail, taskEvent.CreatedAt);
                break;
            case MascotState.ReadingContext:
                AddToolCallRecord("上下文读取", "处理中", detail, taskEvent.CreatedAt);
                break;
            case MascotState.Planning:
                AddToolCallRecord("执行规划", "处理中", detail, taskEvent.CreatedAt);
                break;
            case MascotState.WaitingApproval:
                AddToolCallRecord("权限确认", "等待确认", detail, taskEvent.CreatedAt);
                break;
            case MascotState.Working:
                AddToolCallRecord("任务执行", "运行中", detail, taskEvent.CreatedAt);
                break;
            case MascotState.MemoryConfirm:
                AddToolCallRecord("记忆确认", "等待确认", detail, taskEvent.CreatedAt);
                break;
            case MascotState.Reporting:
                AddToolCallRecord("结果整理", "处理中", detail, taskEvent.CreatedAt);
                break;
            case MascotState.Completed:
                AddToolCallRecord("任务执行", "已完成", detail, taskEvent.CreatedAt);
                break;
            case MascotState.Error:
                AddToolCallRecord("错误处理", "异常", detail, taskEvent.CreatedAt);
                break;
        }
    }

    private void AddToolCallRecord(string toolName, string statusText, string detail, DateTime createdAt)
    {
        TaskToolCalls.Add(new TaskToolCallItem(toolName, statusText, detail, createdAt));

        while (TaskToolCalls.Count > 10)
        {
            TaskToolCalls.RemoveAt(0);
        }

        HasToolCallRecords = TaskToolCalls.Count > 0;
    }

    private string BuildTaskResultDocument()
    {
        return $"""
               # {ActiveTaskTitle}

               - 类型：{ActiveTaskTypeText}
               - 状态：{TaskResultStatusText}
               - 进度：{TaskProgressText}

               ## 输入

               {TaskSummary}

               ## 结果

               {TaskResultPreview}
               """;
    }

    private static string ResolveResultText(TaskResult result)
    {
        if (result.Success)
            return string.IsNullOrWhiteSpace(result.Content) ? "任务已完成。" : result.Content;

        if (!string.IsNullOrWhiteSpace(result.Error))
            return result.Error;

        return string.IsNullOrWhiteSpace(result.Content) ? "处理失败。" : result.Content;
    }

    private static string GetConfirmationTitle(AgentTask task) => task.Type switch
    {
        TaskType.WriteFile => "确认文件写入",
        TaskType.RunCommand => "确认命令执行",
        TaskType.UpdateMemory => "确认保存记忆",
        _ => "确认高风险操作"
    };

    private static string GetConfirmationDescription(AgentTask task) => task.Type switch
    {
        TaskType.WriteFile => "该任务可能创建或修改本地文件。",
        TaskType.RunCommand => "该任务将运行本地命令，请确认命令意图可信。",
        TaskType.UpdateMemory => "该任务会保存为后续可复用的记忆。",
        _ => "该任务需要更高权限，请确认后继续。"
    };

    private static string GetConfirmationRiskText(AgentTask task) => task.Type switch
    {
        TaskType.WriteFile => "L4 文件写入",
        TaskType.RunCommand => "L5 命令执行",
        TaskType.UpdateMemory => "记忆保存",
        _ => $"{task.RequiredPermission}"
    };

    private static string GetConfirmationTarget(AgentTask task, string userMessage) => task.Type switch
    {
        TaskType.WriteFile => ExtractReadableTarget(userMessage, "待写入文件"),
        TaskType.RunCommand => ExtractReadableTarget(userMessage, "待执行命令"),
        TaskType.UpdateMemory => BuildMemoryKey(userMessage),
        _ => task.Title
    };

    private static string GetConfirmationToolName(AgentTask task) => task.Type switch
    {
        TaskType.WriteFile => "文件写入确认",
        TaskType.RunCommand => "命令执行确认",
        TaskType.UpdateMemory => "记忆保存确认",
        _ => "权限确认"
    };

    private static string BuildMemoryKey(string input)
    {
        var text = CleanText(input, "用户记忆", 24);
        return text.ReplaceLineEndings(" ");
    }

    private static string ExtractReadableTarget(string input, string fallback)
    {
        var text = CleanText(input, fallback, 80);
        return text.ReplaceLineEndings(" ");
    }

    private void RemovePendingResponse()
    {
        if (!string.IsNullOrWhiteSpace(_pendingResponseMessage) &&
            Messages.Count > 0 &&
            Messages[^1] == _pendingResponseMessage)
        {
            Messages.RemoveAt(Messages.Count - 1);
        }

        _pendingResponseMessage = null;
    }

    private void TrimTimeline()
    {
        while (TaskTimeline.Count > 12)
        {
            TaskTimeline.RemoveAt(0);
        }
    }

    private static (TaskType Type, PermissionLevel Permission, string Title, string TypeText) BuildTaskMetadata(string input)
    {
        var lower = input.ToLowerInvariant();

        if (input.Contains("总结") || lower.Contains("summarize"))
            return (TaskType.SummarizePage, PermissionLevel.L2_ScreenBrowser, "总结当前内容", "网页/屏幕总结");

        if (input.Contains("报错") || input.Contains("错误") || lower.Contains("error") || lower.Contains("exception"))
            return (TaskType.AnalyzeError, PermissionLevel.L1_WindowTitle, "分析当前报错", "报错分析");

        if (input.Contains("项目") || input.Contains("目录") || lower.Contains("project") || lower.Contains("repo"))
            return (TaskType.InspectProject, PermissionLevel.L3_FileRead, "诊断项目目录", "项目诊断");

        if (input.Contains("写入") || input.Contains("生成文件") || input.Contains("保存"))
            return (TaskType.WriteFile, PermissionLevel.L4_FileWrite, "生成文件", "文件写入");

        if (input.Contains("执行命令") || input.Contains("运行命令") || lower.Contains("terminal") || lower.Contains("command"))
            return (TaskType.RunCommand, PermissionLevel.L5_CommandExec, "执行命令", "命令执行");

        if (input.Contains("记住") || input.Contains("记忆"))
            return (TaskType.UpdateMemory, PermissionLevel.L0_Chat, "记忆更新", "记忆确认");

        return (TaskType.Chat, PermissionLevel.L0_Chat, "用户对话", "普通问答");
    }

    private static int ResolveProgress(MascotState state, int eventProgress)
    {
        if (eventProgress >= 0)
            return Math.Clamp(eventProgress, 0, 100);

        return state switch
        {
            MascotState.Idle => 0,
            MascotState.Listening => 10,
            MascotState.Understanding => 25,
            MascotState.ReadingContext => 40,
            MascotState.Planning => 55,
            MascotState.WaitingApproval => 60,
            MascotState.Working => 70,
            MascotState.Reporting => 85,
            MascotState.MemoryConfirm => 88,
            MascotState.Completed => 100,
            MascotState.Error => 100,
            _ => 0
        };
    }

    private static string GetStateText(MascotState state) => state switch
    {
        MascotState.Idle => "空闲",
        MascotState.Listening => "聆听中",
        MascotState.Understanding => "理解中",
        MascotState.ReadingContext => "读取中",
        MascotState.Planning => "规划中",
        MascotState.WaitingApproval => "等待确认",
        MascotState.Working => "工作中",
        MascotState.MemoryConfirm => "记忆确认",
        MascotState.Reporting => "汇报中",
        MascotState.Completed => "完成",
        MascotState.Error => "出错了",
        _ => "未知"
    };

    private static string GetDefaultStatusMessage(MascotState state) => state switch
    {
        MascotState.Idle => "等待新任务",
        MascotState.Listening => "正在接收任务",
        MascotState.Understanding => "正在理解意图",
        MascotState.ReadingContext => "正在读取上下文",
        MascotState.Planning => "正在规划步骤",
        MascotState.WaitingApproval => "等待用户确认",
        MascotState.Working => "正在执行任务",
        MascotState.MemoryConfirm => "等待记忆确认",
        MascotState.Reporting => "正在整理结果",
        MascotState.Completed => "任务已完成",
        MascotState.Error => "任务出现异常",
        _ => "状态更新"
    };

    private static string GetStateHint(MascotState state) => state switch
    {
        MascotState.Idle => "待命",
        MascotState.Listening => "接收",
        MascotState.Understanding => "理解",
        MascotState.ReadingContext => "读取",
        MascotState.Planning => "规划",
        MascotState.WaitingApproval => "确认",
        MascotState.Working => "执行",
        MascotState.MemoryConfirm => "记忆",
        MascotState.Reporting => "汇报",
        MascotState.Completed => "完成",
        MascotState.Error => "异常",
        _ => "状态"
    };

    private static string GetMascotMoodText(MascotState state) => state switch
    {
        MascotState.Idle => "Ready",
        MascotState.Listening => "Listening",
        MascotState.Understanding => "Thinking",
        MascotState.ReadingContext => "Reading",
        MascotState.Planning => "Planning",
        MascotState.WaitingApproval => "Confirm",
        MascotState.Working => "Working",
        MascotState.MemoryConfirm => "Memory",
        MascotState.Reporting => "Report",
        MascotState.Completed => "Done",
        MascotState.Error => "Error",
        _ => "AI"
    };

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
            CharacterImageFolder = CleanPathText(profile.ImageFolder, "assets/characters/default", 160);
            CharacterAvatarImage = CleanPathText(profile.AvatarImage, "avatar.png", 80);
            _characterStateImages = new Dictionary<string, string>(profile.StateImages);
            RefreshCharacterBrushes();
            RefreshCharacterImage();
        }
        finally
        {
            _isApplyingCharacterProfile = false;
        }

        if (CurrentState == MascotState.Idle)
        {
            StatusMessage = CharacterCatchphrase;
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
        ImageFolder = CleanPathText(CharacterImageFolder, "assets/characters/default", 160),
        AvatarImage = CleanPathText(CharacterAvatarImage, "avatar.png", 80),
        StateImages = new Dictionary<string, string>(_characterStateImages)
    };

    private void RefreshCharacterBrushes()
    {
        StateAccentBrush = GetAccentBrush(CurrentState);
        MascotBackgroundBrush = GetMascotBackgroundBrush(CurrentState);
    }

    private void RefreshCharacterImage()
    {
        var result = _characterImageService.Resolve(BuildCurrentCharacterProfile(), CurrentState);
        CharacterImageSource = result.Image;
        HasCharacterImage = result.HasImage;
        CharacterImageStatus = result.Message;
    }

    private IBrush GetAccentBrush(MascotState state) => state switch
    {
        MascotState.WaitingApproval => BrushFrom("#D97706"),
        MascotState.Completed => BrushFrom("#16A34A"),
        MascotState.Error => BrushFrom("#DC2626"),
        _ => BrushFrom(CharacterAccentColor)
    };

    private IBrush GetMascotBackgroundBrush(MascotState state) => state switch
    {
        MascotState.WaitingApproval => BrushFrom("#FFF7ED"),
        MascotState.Completed => BrushFrom("#F0FDF4"),
        MascotState.Error => BrushFrom("#FEF2F2"),
        _ => BrushFrom(CharacterBackgroundColor)
    };

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

    private static IBrush BrushFrom(string hex) => new SolidColorBrush(Color.Parse(hex));

    public void Dispose()
    {
        _eventBus.TaskEventPublished -= OnTaskEventPublished;
        _eventStreamSubscription.Dispose();
    }

    private sealed class TaskEventObserver : IObserver<TaskEvent>
    {
        private readonly Action<TaskEvent> _onNext;

        public TaskEventObserver(Action<TaskEvent> onNext)
        {
            _onNext = onNext;
        }

        public void OnCompleted()
        {
        }

        public void OnError(Exception error)
        {
        }

        public void OnNext(TaskEvent value)
        {
            _onNext(value);
        }
    }
}
