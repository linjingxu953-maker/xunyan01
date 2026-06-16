using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DesktopMascot.Core.Configuration;
using DesktopMascot.Core.Enums;
using DesktopMascot.Core.Interfaces;
using DesktopMascot.Core.Memory;
using DesktopMascot.Core.Models;
using DesktopMascot.Core.Security;
using DesktopMascot.Core.Storage;
using DesktopMascot.UI.Services;

namespace DesktopMascot.UI.ViewModels;

/// <summary>
/// 悬浮窗口 ViewModel — 分 partial 文件组织：
///   Properties.cs  (可观察属性), Commands.cs (RelayCommand),
///   Events.cs      (事件处理/Computer Use), Character.cs (角色/外观)
/// </summary>
public partial class FloatingWindowViewModel : ObservableObject, IDisposable
{
    public FloatingWindowViewModel(
        ITaskRouter taskRouter, ITaskEventBus eventBus, ITaskEventStream eventStream,
        IMascotCharacterStore characterStore, ICharacterImageService characterImageService,
        ITaskResultActionService taskResultActionService,
        IConfirmationHandler confirmationHandler, IMemoryConfirmationHandler memoryConfirmationHandler,
        IConfigurationManager configurationManager, ISettingsDiagnosticsService settingsDiagnosticsService,
        IOnboardingWindowService onboardingWindowService, ICharacterAssetImportService characterAssetImportService,
        IGlobalHotkeyService hotkeyService, IPermissionManager permissionManager,
        IAuditLogStore auditLogStore, IMemoryStore memoryStore,
        ITaskHistoryStore taskHistoryStore)
    {
        _taskRouter = taskRouter; _eventBus = eventBus; _eventStream = eventStream;
        _taskHistoryStore = taskHistoryStore;
        _characterStore = characterStore; _characterImageService = characterImageService;
        _taskResultActionService = taskResultActionService;
        _confirmationHandler = confirmationHandler; _memoryConfirmationHandler = memoryConfirmationHandler;

        InlineSettings = new SettingsWindowViewModel(configurationManager, settingsDiagnosticsService,
            onboardingWindowService, characterStore, characterImageService, characterAssetImportService,
            new CharacterAssetPickerService(() => _inlineSettingsOwner), hotkeyService,
            permissionManager, auditLogStore, memoryStore, taskHistoryStore);
        InlineSettings.PropertyChanged += OnInlineSettingsPropertyChanged;

        _characterStore.ProfileChanged += OnCharacterProfileChanged;
        ApplyCharacterProfile(_characterStore.Load(), save: false);
        ComputerUseActions.CollectionChanged += (_, _) => NotifyComputerUseActionStateChanged();
        MessageItems.CollectionChanged += (_, _) => NotifyMessageStateChanged();
        TaskHistory.CollectionChanged += (_, _) => NotifyTaskHistoryStateChanged();

        _eventBus.TaskEventPublished += OnTaskEventPublished;
        _eventStreamSubscription = _eventStream.SubscribeAll().Subscribe(new TaskEventObserver(OnTaskStreamEvent));
        _ = LoadTaskHistoryAsync();
    }

    // ── 核心任务执行 ──
    private void PrepareTaskSurface(AgentTask task, string typeText, string userMessage)
    {
        _lastUserMessage = userMessage; _pendingConfirmationTask = null; _pendingConfirmationInput = string.Empty;
        IsWaitingForUserConfirmation = false;
        ActiveTaskId = task.Id; ActiveTaskTitle = task.Title; ActiveTaskTypeText = typeText;
        TaskSummary = userMessage; ActiveStepText = "任务已创建"; CurrentProgress = 0; TaskProgressText = "0%";
        IsTaskActive = true; IsBusy = false; CanCancelTask = false; HasTaskDetails = true; HasTaskResult = false;
        TaskResultPreview = "任务完成后会在这里显示结果。"; TaskResultStatusText = "执行中"; TaskActionStatus = "任务已创建。";
        CanRetryTask = false; TaskTimeline.Clear(); TaskToolCalls.Clear(); HasToolCallRecords = false;
        ResetComputerUsePanel(IsComputerUseTask(task, typeText, userMessage));
        if (IsComputerUsePanelVisible)
            AddComputerUseActionRecord("任务接收", ResolveComputerUseTarget(task, userMessage), "已创建", userMessage, DateTime.UtcNow);
        IsChatVisible = true;
        OnPropertyChanged(nameof(CanRetryCurrentTask));
    }

    private async Task ExecutePreparedTaskAsync(AgentTask task)
    {
        IsWaitingForUserConfirmation = false; IsTaskActive = true; IsBusy = true; CanCancelTask = true;
        TaskActionStatus = "任务正在执行。"; _pendingResponseMessage = $"{CharacterName}：正在处理...";
        Messages.Add(_pendingResponseMessage); MessageItems.Add(new MessageItem { Role = "assistant", Content = "正在处理..." });
        try
        {
            var result = await _taskRouter.DispatchAsync(task);
            RemovePendingResponse();
            HasTaskResult = true; CanRetryTask = !result.Success; CurrentProgress = 100; TaskProgressText = "100%";
            ActiveStepText = result.Success ? "已完成" : "执行失败";
            TaskResultStatusText = result.Success ? "已完成" : "执行失败";
            TaskResultPreview = ResolveResultText(result);
            TaskActionStatus = result.Success ? "任务完成，可以复制或保存结果。" : "任务失败，可以重试。";
            if (MessageItems.Count > 0 && MessageItems[^1].Content == "正在处理...")
                MessageItems.RemoveAt(MessageItems.Count - 1);
            if (result.Success) { Messages.Add($"{CharacterName}：{result.Content}"); MessageItems.Add(new MessageItem { Role = "assistant", Content = result.Content }); }
            else { Messages.Add($"{CharacterName}：{TaskResultPreview}"); MessageItems.Add(new MessageItem { Role = "assistant", Content = TaskResultPreview }); }
            await SaveCurrentConversationAsync(task, result);
        }
        finally { IsBusy = false; IsTaskActive = false; CanCancelTask = false; }
    }

    private async Task<bool> ShowPendingConfirmationAsync(AgentTask task, string userMessage)
    {
        _pendingConfirmationTask = task; _pendingConfirmationInput = userMessage;
        CurrentState = task.Type == TaskType.UpdateMemory ? MascotState.MemoryConfirm : MascotState.WaitingApproval;
        CurrentStateText = GetStateText(CurrentState); StatusMessage = GetDefaultStatusMessage(CurrentState);
        CurrentProgress = ResolveProgress(CurrentState, -1); TaskProgressText = $"{CurrentProgress}%";
        ActiveStepText = GetStateText(CurrentState); StateHint = GetStateHint(CurrentState); MascotMoodText = GetMascotMoodText(CurrentState);
        StateAccentBrush = GetAccentBrush(CurrentState); MascotBackgroundBrush = GetMascotBackgroundBrush(CurrentState); RefreshCharacterImage();
        PendingConfirmationTitle = GetConfirmationTitle(task); PendingConfirmationDescription = GetConfirmationDescription(task);
        PendingConfirmationRiskText = GetConfirmationRiskText(task); IsWaitingForUserConfirmation = true;
        TaskActionStatus = "等待你确认后继续执行。";
        AddTimelineItem(CurrentState, PendingConfirmationDescription, CurrentProgress, DateTime.UtcNow);
        AddToolCallRecord(GetConfirmationToolName(task), "等待确认", PendingConfirmationRiskText, DateTime.UtcNow);
        Messages.Add($"{CharacterName}：这个任务需要你确认后继续。");

        var confirmed = await RequestExternalConfirmationAsync(task, userMessage);
        _pendingConfirmationTask = null; _pendingConfirmationInput = string.Empty; IsWaitingForUserConfirmation = false;
        if (confirmed) { TaskActionStatus = "已确认，开始执行任务。"; AddToolCallRecord(GetConfirmationToolName(task), "已确认", PendingConfirmationRiskText, DateTime.UtcNow); Messages.Add($"{CharacterName}：已确认，开始执行。"); return true; }
        ApplyDeniedConfirmation(task); return false;
    }

    private async Task<bool> RequestExternalConfirmationAsync(AgentTask task, string userMessage)
    {
        if (task.Type == TaskType.UpdateMemory)
            return await _memoryConfirmationHandler.RequestConfirmationAsync(new MemoryConfirmRequest
            {
                ProposedMemory = new MemoryEntry { Type = MemoryType.User, Key = BuildMemoryKey(userMessage), Content = userMessage, Source = "桌面悬浮窗任务输入", TaskId = task.Id, Tags = { ["taskType"] = task.Type.ToString(), ["source"] = "ui" } },
                Reason = "用户请求保存为后续可复用的记忆。"
            });
        var response = await _confirmationHandler.RequestConfirmationAsync(new PermissionRequest
        {
            TaskId = task.Id, Level = task.RequiredPermission, Title = GetConfirmationTitle(task),
            Description = GetConfirmationDescription(task), Target = GetConfirmationTarget(task, userMessage),
            Risk = GetConfirmationRiskText(task),
            Metadata = { ["任务类型"] = task.Type.ToString(), ["任务标题"] = task.Title, ["用户输入"] = userMessage, ["权限等级"] = task.RequiredPermission.ToString() }
        });
        return response.Decision != PermissionDecision.Deny;
    }

    private void ApplyDeniedConfirmation(AgentTask task)
    {
        IsTaskActive = false; CanCancelTask = false; IsBusy = false; CurrentState = MascotState.Error;
        CurrentStateText = GetStateText(CurrentState); CurrentProgress = 100; TaskProgressText = "100%"; ActiveStepText = "已拒绝";
        StateHint = GetStateHint(CurrentState); StateAccentBrush = GetAccentBrush(CurrentState); MascotBackgroundBrush = GetMascotBackgroundBrush(CurrentState);
        StatusMessage = "用户拒绝了操作"; HasTaskResult = true; TaskResultPreview = "用户拒绝了操作。";
        TaskResultStatusText = "已拒绝"; TaskActionStatus = "操作已拒绝，未执行任务。"; CanRetryTask = true;
        AddTimelineItem(MascotState.Error, "用户拒绝了操作", CurrentProgress, DateTime.UtcNow);
        AddToolCallRecord(GetConfirmationToolName(task), "已拒绝", PendingConfirmationRiskText, DateTime.UtcNow);
        Messages.Add($"{CharacterName}：已取消该操作。");
    }

    private static bool RequiresUserConfirmation(AgentTask task) =>
        task.Type is TaskType.WriteFile or TaskType.RunCommand or TaskType.UpdateMemory || task.RequiredPermission >= PermissionLevel.L4_FileWrite;

    // ── 设置 ──
    public void OpenSettingsPanel(string? section = null)
    {
        IsMascotIconVisible = true; IsChatDialogVisible = true; IsChatVisible = true; IsChatPageVisible = false; IsSettingsPageVisible = true;
        InlineSettings.SelectSectionById(section); ApplyInlineSettingsSection(section); _ = LoadInlineSettingsAsync(section);
    }

    private async Task LoadInlineSettingsAsync(string? section)
    {
        try
        {
            if (!_hasLoadedInlineSettings) { _hasLoadedInlineSettings = true; await InlineSettings.LoadAsync(); }
            InlineSettings.SelectSectionById(section); SyncInlineSettingsStatus();
        }
        catch { InlineSettingsStatus = "设置加载失败，请稍后重试。"; }
    }

    private void ApplyInlineSettingsSection(string? section)
    {
        var key = string.IsNullOrWhiteSpace(section) ? "overview" : section;
        var (title, desc, fallback) = key switch
        {
            "model" => ("模型设置", "配置 Provider、API Key、Base URL 和默认模型。", "模型配置会保存到本机配置目录。"),
            "mimoCode" => ("Mimo Code", "接入本机 Mimo Code，模型调用仍使用用户自己的 API 配置。", "Mimo Code 接入配置会保存到本机配置目录。"),
            "permission" => ("权限", "查看文件写入、命令执行和高风险工具的确认策略。", "权限确认仍走当前独立确认弹窗体系。"),
            "memory" => ("记忆", "管理待确认记忆、已保存记忆和自动学习策略。", "记忆确认仍走当前 M30 回调入口。"),
            "history" => ("任务历史", "查看任务记录、结果、事件和工具调用。", "任务历史会从 ITaskHistoryStore 读取。"),
            "hotkey" => ("快捷键", "配置唤起输入和屏幕圈选快捷键。", "快捷键保存会继续做冲突检测和失败回滚。"),
            "data" => ("日志/数据", "查看本机配置、日志、缓存和数据目录。", "可打开目录、刷新占用并清理本地缓存。"),
            "appearance" => ("角色外观", "管理人物图片、状态图映射、角色名、颜色和预设。", "角色图片仍优先导入到本机稳定资源目录。"),
            _ => ("设置", "模型、权限、记忆、快捷键、数据目录和角色外观都在这里管理。", "选择左侧设置项查看当前配置入口。")
        };
        InlineSettingsTitle = title; InlineSettingsDescription = desc; InlineSettingsStatus = GetInlineSettingsStatus(key, fallback);
    }

    private void OnInlineSettingsPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!ShouldSyncInlineSettingsStatus(e.PropertyName)) return;
        if (Dispatcher.UIThread.CheckAccess()) { SyncInlineSettingsStatus(); return; }
        Dispatcher.UIThread.Post(SyncInlineSettingsStatus);
    }

    private static bool ShouldSyncInlineSettingsStatus(string? propertyName) =>
        propertyName is nameof(SettingsWindowViewModel.SelectedSectionId) or nameof(SettingsWindowViewModel.IsBusy)
            or nameof(SettingsWindowViewModel.ModelSettingsStatus) or nameof(SettingsWindowViewModel.MimoCodeStatus)
            or nameof(SettingsWindowViewModel.PermissionSettingsStatus) or nameof(SettingsWindowViewModel.MemorySettingsStatus)
            or nameof(SettingsWindowViewModel.TaskHistorySettingsStatus)
            or nameof(SettingsWindowViewModel.HotkeySettingsStatus) or nameof(SettingsWindowViewModel.DataSettingsStatus)
            or nameof(SettingsWindowViewModel.DataStorageSummary) or nameof(SettingsWindowViewModel.CharacterSaveStatus)
            or nameof(SettingsWindowViewModel.CharacterAssetSuggestionStatus) or nameof(SettingsWindowViewModel.CharacterStatePreviewStatus)
            or nameof(SettingsWindowViewModel.CharacterImageStatus);

    private void SyncInlineSettingsStatus() => InlineSettingsStatus = GetInlineSettingsStatus(InlineSettings.SelectedSectionId, InlineSettingsStatus);

    private string GetInlineSettingsStatus(string? section, string fallback)
    {
        if (InlineSettings.IsBusy) return "正在处理设置操作，请稍候。";
        return section switch
        {
            "model" => InlineSettings.ModelSettingsStatus, "mimoCode" => InlineSettings.MimoCodeStatus,
            "permission" => InlineSettings.PermissionSettingsStatus, "memory" => InlineSettings.MemorySettingsStatus,
            "history" => InlineSettings.TaskHistorySettingsStatus,
            "hotkey" => InlineSettings.HotkeySettingsStatus, "data" => $"{InlineSettings.DataSettingsStatus} {InlineSettings.DataStorageSummary}",
            "appearance" => string.Join(" ", new[] { InlineSettings.CharacterSaveStatus, InlineSettings.CharacterImageStatus, InlineSettings.CharacterAssetSuggestionStatus, InlineSettings.CharacterStatePreviewStatus }.Where(t => !string.IsNullOrWhiteSpace(t))),
            _ => fallback
        };
    }

    // ── 公开方法 ──
    public void PlayMessageAudio(string? content) { IsVoiceReplyPlaying = true; VoiceReplyStatus = $"准备朗读：{CleanText(content, "当前消息", 60)}"; }
    public void RequestScreenSelection() { if (!CanStartScreenSelection) return; StatusMessage = "拖动鼠标圈选要理解的屏幕区域。"; ScreenSelectionRequested?.Invoke(this, EventArgs.Empty); }

    public async Task AnalyzeSelectedScreenRegionAsync(ScreenSelectionResult result)
    {
        if (!result.HasRegion) return;
        var userMessage = $"请分析我圈选的屏幕区域 {result.Summary}";
        Messages.Add($"你：{userMessage}");
        var task = new AgentTask { Title = "屏幕区域理解", Input = userMessage, Type = TaskType.ScreenUnderstand, RequiredPermission = PermissionLevel.L2_ScreenBrowser, Parameters = { ["Region"] = new { region = new { x = result.X, y = result.Y, width = result.Width, height = result.Height } }, ["UserHint"] = "用户圈选的屏幕区域" } };
        PrepareTaskSurface(task, "屏幕理解", userMessage);
        if (RequiresUserConfirmation(task)) { if (!await ShowPendingConfirmationAsync(task, userMessage)) return; }
        await ExecutePreparedTaskAsync(task);
    }

    public void CancelScreenSelection() { StatusMessage = "已取消屏幕圈选。"; IsChatVisible = true; }
    public void OpenChatPanel() => ExpandDialog();
    public void CollapseChatPanel() => CollapseDialog();
    public void OpenTaskHistoryItem(TaskHistoryItem? item) { if (item is null) return; BackToChat(); MessageItems.Clear(); Messages.Clear(); foreach (var m in item.Messages) { MessageItems.Add(m); Messages.Add($"{m.RoleText}：{m.Content}"); } StateHint = "历史"; TaskActionStatus = $"已打开历史任务：{item.Title}"; }
    public async Task CopyTaskHistoryItemAsync(TaskHistoryItem? item) { if (item is null) return; var copied = await _taskResultActionService.CopyToClipboardAsync(BuildTaskHistoryDocument(item)); TaskActionStatus = copied ? "历史任务已复制到剪贴板。" : "复制失败：当前没有可用剪贴板。"; }
    public async Task SaveTaskHistoryItemAsync(TaskHistoryItem? item) { if (item is null) return; var path = await _taskResultActionService.SaveResultAsync(item.Title, BuildTaskHistoryDocument(item)); TaskActionStatus = $"历史任务已保存：{path}"; }
    public async Task DeleteTaskHistoryItemAsync(TaskHistoryItem? item)
    {
        if (item is null) return;

        try
        {
            var deleted = await _taskHistoryStore.DeleteTaskAsync(item.Id);
            TaskHistory.Remove(item);
            TaskActionStatus = deleted ? $"已删除历史任务：{item.Title}" : $"已从侧栏移除历史任务：{item.Title}";
        }
        catch
        {
            TaskHistory.Remove(item);
            TaskActionStatus = $"历史任务已从侧栏移除，存储删除稍后重试：{item.Title}";
        }
    }

    private async Task LoadTaskHistoryAsync()
    {
        try
        {
            var records = await _taskHistoryStore.GetRecentTasksAsync(30);
            var items = records
                .OrderByDescending(record => record.CreatedAt)
                .Select(CreateTaskHistoryItem)
                .Where(item => item.Messages.Count > 0)
                .ToList();

            Dispatcher.UIThread.Post(() =>
            {
                TaskHistory.Clear();
                foreach (var item in items)
                {
                    TaskHistory.Add(item);
                }
            });
        }
        catch
        {
            Dispatcher.UIThread.Post(() => TaskActionStatus = "任务历史加载失败，当前只显示本次会话历史。");
        }
    }

    private async Task SaveCurrentConversationAsync(AgentTask? task = null, TaskResult? result = null)
    {
        if (MessageItems.Count == 0)
            return;

        var firstUserMessage = MessageItems.FirstOrDefault(message => message.Role == "user")?.Content ?? TaskSummary;
        var lastAssistantMessage = MessageItems.LastOrDefault(message => message.Role != "user")?.Content ?? TaskResultPreview;
        var metadata = task is null ? BuildTaskMetadata(firstUserMessage) : (task.Type, task.RequiredPermission, task.Title, ActiveTaskTypeText);
        var completedAt = DateTime.UtcNow;
        var record = new TaskHistoryRecord
        {
            Id = _currentConversationId,
            Title = CleanText(firstUserMessage, metadata.Title, 42),
            Input = firstUserMessage,
            Type = metadata.Type,
            Status = result is null || result.Success ? AppTaskStatus.Completed : AppTaskStatus.Failed,
            Result = lastAssistantMessage,
            Error = result is { Success: false } ? result.Error : null,
            CreatedAt = _currentConversationCreatedAt,
            CompletedAt = completedAt,
            Events = MessageItems.Select((message, index) => new TaskEventRecord
            {
                TaskId = _currentConversationId,
                State = message.Role,
                Message = message.Content,
                Progress = 100,
                Timestamp = _currentConversationCreatedAt.AddMilliseconds(index)
            }).ToList()
        };

        try
        {
            await _taskHistoryStore.SaveTaskAsync(record);
            UpsertTaskHistoryItem(CreateTaskHistoryItem(record));
        }
        catch
        {
            UpsertTaskHistoryItem(new TaskHistoryItem
            {
                Id = record.Id,
                Title = record.Title,
                TimeText = FormatHistoryTime(record.CreatedAt),
                Messages = MessageItems.Select(CloneMessage).ToList()
            });
            TaskActionStatus = "任务历史已保留在侧栏，持久化保存稍后重试。";
        }
    }

    private void StartNewConversation()
    {
        _currentConversationId = Guid.NewGuid().ToString("N");
        _currentConversationCreatedAt = DateTime.UtcNow;
        _lastUserMessage = string.Empty;
    }

    private void UpsertTaskHistoryItem(TaskHistoryItem item)
    {
        var existing = TaskHistory.FirstOrDefault(history => history.Id == item.Id);
        if (existing is not null)
        {
            var index = TaskHistory.IndexOf(existing);
            TaskHistory[index] = item;
        }
        else
        {
            TaskHistory.Insert(0, item);
        }
    }

    private static TaskHistoryItem CreateTaskHistoryItem(TaskHistoryRecord record)
    {
        var messages = record.Events
            .Where(item => item.State is "user" or "assistant")
            .OrderBy(item => item.Timestamp)
            .Select(item => new MessageItem { Role = item.State, Content = item.Message })
            .ToList();

        if (messages.Count == 0 && !string.IsNullOrWhiteSpace(record.Input))
        {
            messages.Add(new MessageItem { Role = "user", Content = record.Input });

            if (!string.IsNullOrWhiteSpace(record.Result))
            {
                messages.Add(new MessageItem { Role = "assistant", Content = record.Result });
            }
            else if (!string.IsNullOrWhiteSpace(record.Error))
            {
                messages.Add(new MessageItem { Role = "assistant", Content = record.Error });
            }
        }

        return new TaskHistoryItem
        {
            Id = record.Id,
            Title = CleanText(record.Title, record.Input, 42),
            TimeText = FormatHistoryTime(record.CreatedAt),
            Messages = messages
        };
    }

    private static MessageItem CloneMessage(MessageItem message) => new() { Role = message.Role, Content = message.Content };

    private static string FormatHistoryTime(DateTime value)
    {
        var local = value.Kind == DateTimeKind.Local ? value : value.ToLocalTime();
        return local.Date == DateTime.Now.Date ? local.ToString("HH:mm") : local.ToString("MM-dd HH:mm");
    }

    // ── 状态映射 ──
    private bool CanCancelCurrentTask() => CanCancelTask;

    private static string GetStateText(MascotState state) => state switch { MascotState.Idle => "空闲", MascotState.Listening => "聆听中", MascotState.Understanding => "理解中", MascotState.ReadingContext => "读取中", MascotState.Planning => "规划中", MascotState.WaitingApproval => "等待确认", MascotState.Working => "工作中", MascotState.MemoryConfirm => "记忆确认", MascotState.Reporting => "汇报中", MascotState.Completed => "完成", MascotState.Error => "出错了", _ => "未知" };
    private static string GetDefaultStatusMessage(MascotState state) => state switch { MascotState.Idle => "等待新任务", MascotState.Listening => "正在接收任务", MascotState.Understanding => "正在理解意图", MascotState.ReadingContext => "正在读取上下文", MascotState.Planning => "正在规划步骤", MascotState.WaitingApproval => "等待用户确认", MascotState.Working => "正在执行任务", MascotState.MemoryConfirm => "等待记忆确认", MascotState.Reporting => "正在整理结果", MascotState.Completed => "任务已完成", MascotState.Error => "任务出现异常", _ => "状态更新" };
    private static string GetStateHint(MascotState state) => state switch { MascotState.Idle => "待命", MascotState.Listening => "接收", MascotState.Understanding => "理解", MascotState.ReadingContext => "读取", MascotState.Planning => "规划", MascotState.WaitingApproval => "确认", MascotState.Working => "执行", MascotState.MemoryConfirm => "记忆", MascotState.Reporting => "汇报", MascotState.Completed => "完成", MascotState.Error => "异常", _ => "状态" };
    private static string GetMascotMoodText(MascotState state) => state switch { MascotState.Idle => "Ready", MascotState.Listening => "Listening", MascotState.Understanding => "Thinking", MascotState.ReadingContext => "Reading", MascotState.Planning => "Planning", MascotState.WaitingApproval => "Confirm", MascotState.Working => "Working", MascotState.MemoryConfirm => "Memory", MascotState.Reporting => "Report", MascotState.Completed => "Done", MascotState.Error => "Error", _ => "AI" };
    private static int ResolveProgress(MascotState state, int eventProgress) => eventProgress >= 0 ? Math.Clamp(eventProgress, 0, 100) : state switch { MascotState.Idle => 0, MascotState.Listening => 10, MascotState.Understanding => 25, MascotState.ReadingContext => 40, MascotState.Planning => 55, MascotState.WaitingApproval => 60, MascotState.Working => 70, MascotState.Reporting => 85, MascotState.MemoryConfirm => 88, MascotState.Completed => 100, MascotState.Error => 100, _ => 0 };
    private static string GetEventStepText(TaskEvent e) => e.EventType switch { TaskEventType.TaskStarted => "任务已开始", TaskEventType.TaskCompleted => "已完成", TaskEventType.TaskFailed => "执行失败", TaskEventType.TaskCancelled => "已取消", TaskEventType.ToolCallStarted => $"执行工具：{GetMetadataString(e, "toolName", "未知工具")}", TaskEventType.ToolCallCompleted => $"工具完成：{GetMetadataString(e, "toolName", "未知工具")}", TaskEventType.ToolCallFailed => $"工具失败：{GetMetadataString(e, "toolName", "未知工具")}", TaskEventType.PermissionRequested => "等待权限确认", TaskEventType.PermissionGranted => "权限已授权", TaskEventType.PermissionDenied => "权限已拒绝", TaskEventType.MemorySaveRequested => "等待记忆确认", TaskEventType.MemorySaveCompleted => "记忆已保存", TaskEventType.MemorySaveRejected => "记忆已拒绝", _ => GetStateText(e.State) };
    private static string ResolveEventMessage(TaskEvent e) => !string.IsNullOrWhiteSpace(e.Message) ? e.Message : e.EventType switch { TaskEventType.TaskStarted => "任务已开始", TaskEventType.TaskCompleted => "任务已完成", TaskEventType.TaskFailed => "任务执行失败", TaskEventType.TaskCancelled => "任务已取消", TaskEventType.ToolCallStarted => $"正在执行工具：{GetMetadataString(e, "toolName", "未知工具")}", TaskEventType.ToolCallCompleted => $"工具执行完成：{GetMetadataString(e, "toolName", "未知工具")}", TaskEventType.ToolCallFailed => $"工具执行失败：{GetMetadataString(e, "toolName", "未知工具")}", TaskEventType.PermissionRequested => "等待权限确认", TaskEventType.PermissionGranted => "权限已授权", TaskEventType.PermissionDenied => "权限已拒绝", TaskEventType.MemorySaveRequested => "等待记忆确认", TaskEventType.MemorySaveCompleted => "记忆已保存", TaskEventType.MemorySaveRejected => "记忆保存已拒绝", _ => GetDefaultStatusMessage(e.State) };
    private static int ResolveEventProgress(TaskEvent e) => e.EventType switch { TaskEventType.TaskFailed or TaskEventType.TaskCancelled => 100, _ => ResolveProgress(e.State, e.Progress) };
    private static string ResolvePermissionDescription(TaskEvent e, string fallback) { var scope = GetMetadataString(e, "scope", string.Empty); var reason = GetMetadataString(e, "reason", fallback); return string.IsNullOrWhiteSpace(scope) ? reason : $"{reason}：{scope}"; }
    private static string GetMetadataString(TaskEvent e, string key, string fallback) { if (e.Metadata is null || !e.Metadata.TryGetValue(key, out var v) || v is null) return fallback; var t = v.ToString(); return string.IsNullOrWhiteSpace(t) ? fallback : t; }

    // ── 确认帮助 ──
    private static string GetConfirmationTitle(AgentTask task) => task.Type switch { TaskType.WriteFile => "确认文件写入", TaskType.RunCommand => "确认命令执行", TaskType.UpdateMemory => "确认保存记忆", _ => "确认高风险操作" };
    private static string GetConfirmationDescription(AgentTask task) => task.Type switch { TaskType.WriteFile => "该任务可能创建或修改本地文件。", TaskType.RunCommand => "该任务将运行本地命令，请确认命令意图可信。", TaskType.UpdateMemory => "该任务会保存为后续可复用的记忆。", _ => "该任务需要更高权限，请确认后继续。" };
    private static string GetConfirmationRiskText(AgentTask task) => task.Type switch { TaskType.WriteFile => "L4 文件写入", TaskType.RunCommand => "L5 命令执行", TaskType.UpdateMemory => "记忆保存", _ => $"{task.RequiredPermission}" };
    private static string GetConfirmationTarget(AgentTask task, string userMessage) => task.Type switch { TaskType.WriteFile => ExtractReadableTarget(userMessage, "待写入文件"), TaskType.RunCommand => ExtractReadableTarget(userMessage, "待执行命令"), TaskType.UpdateMemory => BuildMemoryKey(userMessage), _ => task.Title };
    private static string GetConfirmationToolName(AgentTask task) => task.Type switch { TaskType.WriteFile => "文件写入确认", TaskType.RunCommand => "命令执行确认", TaskType.UpdateMemory => "记忆保存确认", _ => "权限确认" };
    private static string BuildMemoryKey(string input) => CleanText(input, "用户记忆", 24).ReplaceLineEndings(" ");
    private static string ExtractReadableTarget(string input, string fallback) => CleanText(input, fallback, 80).ReplaceLineEndings(" ");
    private static string ResolveComputerUseTarget(AgentTask task, string userMessage) => task.Type == TaskType.ScreenUnderstand || task.Parameters.ContainsKey("Region") ? "屏幕圈选区域" : ExtractReadableTarget(userMessage, "当前桌面");
    private static string ResolveResultText(TaskResult result) => result.Success ? (string.IsNullOrWhiteSpace(result.Content) ? "任务已完成。" : result.Content) : (!string.IsNullOrWhiteSpace(result.Error) ? result.Error : (string.IsNullOrWhiteSpace(result.Content) ? "处理失败。" : result.Content));
    private static (TaskType Type, PermissionLevel Permission, string Title, string TypeText) BuildTaskMetadata(string input) { var lower = input.ToLowerInvariant(); if (input.Contains("总结") || lower.Contains("summarize")) return (TaskType.SummarizePage, PermissionLevel.L2_ScreenBrowser, "总结当前内容", "网页/屏幕总结"); if (input.Contains("报错") || input.Contains("错误") || lower.Contains("error") || lower.Contains("exception")) return (TaskType.AnalyzeError, PermissionLevel.L1_WindowTitle, "分析当前报错", "报错分析"); if (ContainsComputerUseSignal(input)) return (TaskType.ComputerUse, PermissionLevel.L2_ScreenBrowser, "桌面自动操作", "Computer Use"); if (input.Contains("项目") || input.Contains("目录") || lower.Contains("project") || lower.Contains("repo")) return (TaskType.InspectProject, PermissionLevel.L3_FileRead, "诊断项目目录", "项目诊断"); if (input.Contains("写入") || input.Contains("生成文件") || input.Contains("保存")) return (TaskType.WriteFile, PermissionLevel.L4_FileWrite, "生成文件", "文件写入"); if (input.Contains("执行命令") || input.Contains("运行命令") || lower.Contains("terminal") || lower.Contains("command")) return (TaskType.RunCommand, PermissionLevel.L5_CommandExec, "执行命令", "命令执行"); if (input.Contains("记住") || input.Contains("记忆")) return (TaskType.UpdateMemory, PermissionLevel.L0_Chat, "记忆更新", "记忆确认"); return (TaskType.Chat, PermissionLevel.L0_Chat, "用户对话", "普通问答"); }

    // ── 文档生成 ──
    private string BuildTaskResultDocument() => $"# {ActiveTaskTitle}\n\n- 类型：{ActiveTaskTypeText}\n- 状态：{TaskResultStatusText}\n- 进度：{TaskProgressText}\n\n## 输入\n\n{TaskSummary}\n\n## 结果\n\n{TaskResultPreview}\n";
    private static string BuildTaskHistoryDocument(TaskHistoryItem item) { var sb = new StringBuilder(); foreach (var m in item.Messages) { if (sb.Length > 0) sb.AppendLine().AppendLine(); sb.AppendLine($"## {m.RoleText}").AppendLine().AppendLine(m.Content); } return $"# {item.Title}\n\n- 时间：{item.TimeText}\n- 消息：{item.MessageCountText}\n\n{sb}"; }

    private void RemovePendingResponse() { if (!string.IsNullOrWhiteSpace(_pendingResponseMessage) && Messages.Count > 0 && Messages[^1] == _pendingResponseMessage) Messages.RemoveAt(Messages.Count - 1); _pendingResponseMessage = null; }

    public void Dispose()
    {
        _eventBus.TaskEventPublished -= OnTaskEventPublished;
        _eventStreamSubscription.Dispose();
    }

    private sealed class TaskEventObserver(Action<TaskEvent> onNext) : IObserver<TaskEvent>
    {
        public void OnCompleted() { }
        public void OnError(Exception error) { }
        public void OnNext(TaskEvent value) => onNext(value);
    }
}

// ── 嵌套模型类（纯数据/视图展示，不属于 ViewModel 业务逻辑） ──

public class MessageItem
{
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public bool IsAssistant => Role != "user";
    public string RoleText => Role == "user" ? "你" : "妍";
    public HorizontalAlignment BubbleAlignment => Role == "user" ? HorizontalAlignment.Right : HorizontalAlignment.Left;
    public CornerRadius BubbleCornerRadius => Role == "user" ? new CornerRadius(14, 14, 4, 14) : new CornerRadius(14, 14, 14, 4);
    public IBrush RoleColor => Role == "user" ? new SolidColorBrush(Color.Parse("#93C5FD")) : new SolidColorBrush(Color.Parse("#CBD5E1"));
    public IBrush BubbleBackground => Role == "user" ? new SolidColorBrush(Color.Parse("#2563EB")) : new SolidColorBrush(Color.Parse("#F8FAFC"));
    public IBrush ContentBrush => Role == "user" ? new SolidColorBrush(Color.Parse("#FFFFFF")) : new SolidColorBrush(Color.Parse("#111827"));
}

public class TaskHistoryItem
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string TimeText { get; set; } = string.Empty;
    public List<MessageItem> Messages { get; set; } = new();
    public string MessageCountText => Messages.Count == 0 ? "无消息" : $"{Messages.Count} 条消息";
    public string PreviewText => Trim(Messages.LastOrDefault()?.Content ?? "暂无内容", 96);
    private static string Trim(string value, int maxLength) { var t = string.IsNullOrWhiteSpace(value) ? "暂无内容" : value.Trim(); return t.Length <= maxLength ? t : $"{t[..maxLength]}..."; }
}
