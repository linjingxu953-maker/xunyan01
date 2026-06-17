using Avalonia.Media.Imaging;
using Avalonia.Threading;
using DesktopMascot.Core.Enums;
using DesktopMascot.Core.Interfaces;
using DesktopMascot.Core.Models;

namespace DesktopMascot.UI.ViewModels;

/// <summary>FloatingWindowViewModel — 事件处理、工具调用记录、时间线、Computer Use 事件</summary>
public partial class FloatingWindowViewModel
{
    private void OnTaskEventPublished(object? sender, TaskEvent e) => QueueTaskEvent(e);
    private void OnTaskStreamEvent(TaskEvent e) => QueueTaskEvent(e);

    private void QueueTaskEvent(TaskEvent e)
    {
        if (Dispatcher.UIThread.CheckAccess()) { ApplyTaskEvent(e); return; }
        Dispatcher.UIThread.Post(() => ApplyTaskEvent(e));
    }

    private void ApplyTaskEvent(TaskEvent e)
    {
        if (!ShouldDisplayTaskEvent(e) || !TryMarkTaskEventApplied(e)) return;
        var message = ResolveEventMessage(e);
        var progress = ResolveEventProgress(e);
        CurrentState = e.State; CurrentStateText = GetStateText(e.State); StatusMessage = message; CurrentProgress = progress;
        TaskProgressText = $"{progress}%"; ActiveStepText = GetEventStepText(e); StateHint = GetStateHint(e.State);
        MascotMoodText = GetMascotMoodText(e.State); StateAccentBrush = GetAccentBrush(e.State); MascotBackgroundBrush = GetMascotBackgroundBrush(e.State);
        IsWaitingForUserConfirmation = e.State is MascotState.WaitingApproval or MascotState.MemoryConfirm;
        ApplyConfirmationStateFromEvent(e, message);
        ApplyTaskResultStateFromEvent(e, message);
        ApplyComputerUseStateFromEvent(e, message);
        RefreshCharacterImage();
        AddToolCallRecord(e);
        if (e.State != MascotState.Idle) { HasTaskDetails = true; AddTimelineItem(e.State, message, progress, e.CreatedAt); TrimTimeline(); }
    }

    // ── 部分属性变更回调 ──
    partial void OnIsChatVisibleChanged(bool value) => IsMainAreaHitTestVisible = !value;
    partial void OnInputTextChanged(string value) => SendMessageCommand.NotifyCanExecuteChanged();
    partial void OnCurrentStateChanged(MascotState value) { StateAccentBrush = GetAccentBrush(value); MascotBackgroundBrush = GetMascotBackgroundBrush(value); RefreshCharacterImage(); }

    partial void OnIsBusyChanged(bool value)
    {
        OnPropertyChanged(nameof(CanSendMessage)); OnPropertyChanged(nameof(CanStartScreenSelection));
        SendMessageCommand.NotifyCanExecuteChanged(); StartScreenSelectionCommand.NotifyCanExecuteChanged();
        CopyTaskResultCommand.NotifyCanExecuteChanged(); SaveTaskResultCommand.NotifyCanExecuteChanged();
        RetryTaskCommand.NotifyCanExecuteChanged(); ApprovePendingTaskCommand.NotifyCanExecuteChanged(); DenyPendingTaskCommand.NotifyCanExecuteChanged();
    }

    partial void OnCanCancelTaskChanged(bool value) => CancelCurrentTaskCommand.NotifyCanExecuteChanged();
    partial void OnHasTaskResultChanged(bool value) { CopyTaskResultCommand.NotifyCanExecuteChanged(); SaveTaskResultCommand.NotifyCanExecuteChanged(); }
    partial void OnCanRetryTaskChanged(bool value) => RetryTaskCommand.NotifyCanExecuteChanged();

    partial void OnIsWaitingForUserConfirmationChanged(bool value)
    {
        OnPropertyChanged(nameof(CanSendMessage)); OnPropertyChanged(nameof(CanStartScreenSelection)); OnPropertyChanged(nameof(CanResolvePendingConfirmation));
        SendMessageCommand.NotifyCanExecuteChanged(); StartScreenSelectionCommand.NotifyCanExecuteChanged(); RetryTaskCommand.NotifyCanExecuteChanged();
        ApprovePendingTaskCommand.NotifyCanExecuteChanged(); DenyPendingTaskCommand.NotifyCanExecuteChanged();
    }

    partial void OnCharacterImageFolderChanged(string value) { if (!_isApplyingCharacterProfile) RefreshCharacterImage(); }
    partial void OnCharacterAvatarImageChanged(string value) { if (!_isApplyingCharacterProfile) RefreshCharacterImage(); }

    // ── 事件过滤 ──
    private bool ShouldDisplayTaskEvent(TaskEvent taskEvent)
    {
        if (string.IsNullOrWhiteSpace(taskEvent.TaskId)) return true;
        if (string.IsNullOrWhiteSpace(ActiveTaskId)) { PrepareEventDrivenTaskSurface(taskEvent); return true; }
        if (string.Equals(taskEvent.TaskId, ActiveTaskId, StringComparison.Ordinal)) return true;
        if (taskEvent.EventType == TaskEventType.TaskStarted && !IsTaskActive && !IsBusy) { PrepareEventDrivenTaskSurface(taskEvent); return true; }
        return false;
    }

    private void PrepareEventDrivenTaskSurface(TaskEvent taskEvent)
    {
        ActiveTaskId = taskEvent.TaskId; ActiveTaskTitle = CleanText(taskEvent.Message, "事件流任务", 36);
        ActiveTaskTypeText = "事件流任务"; TaskSummary = string.IsNullOrWhiteSpace(taskEvent.Message) ? "来自任务事件流。" : taskEvent.Message;
        HasTaskDetails = true; HasTaskResult = false; CanRetryTask = false;
        TaskResultPreview = "任务完成后会在这里显示结果。"; TaskResultStatusText = "执行中"; TaskActionStatus = "任务事件已接入 UI。";
        TaskTimeline.Clear(); TaskToolCalls.Clear(); HasToolCallRecords = false;
        ResetComputerUsePanel(IsComputerUseEvent(taskEvent));
    }

    private bool TryMarkTaskEventApplied(TaskEvent taskEvent)
    {
        if (string.IsNullOrWhiteSpace(taskEvent.Id)) return true;
        if (!_appliedEventIds.Add(taskEvent.Id)) return false;
        _appliedEventIdOrder.Enqueue(taskEvent.Id);
        while (_appliedEventIdOrder.Count > 300) _appliedEventIds.Remove(_appliedEventIdOrder.Dequeue());
        return true;
    }

    // ── 确认/结果状态 ──
    private void ApplyConfirmationStateFromEvent(TaskEvent taskEvent, string message)
    {
        switch (taskEvent.EventType)
        {
            case TaskEventType.PermissionRequested: PendingConfirmationTitle = "等待权限确认"; PendingConfirmationDescription = ResolvePermissionDescription(taskEvent, message); PendingConfirmationRiskText = GetMetadataString(taskEvent, "permissionType", "权限确认"); IsWaitingForUserConfirmation = true; TaskActionStatus = "等待权限确认弹窗处理。"; break;
            case TaskEventType.MemorySaveRequested: PendingConfirmationTitle = "等待记忆确认"; PendingConfirmationDescription = GetMetadataString(taskEvent, "content", message); PendingConfirmationRiskText = GetMetadataString(taskEvent, "memoryType", "记忆保存"); IsWaitingForUserConfirmation = true; TaskActionStatus = "等待记忆确认弹窗处理。"; break;
            case TaskEventType.PermissionGranted or TaskEventType.MemorySaveCompleted: IsWaitingForUserConfirmation = false; TaskActionStatus = "确认已通过，任务继续执行。"; break;
            case TaskEventType.PermissionDenied or TaskEventType.MemorySaveRejected: IsWaitingForUserConfirmation = false; TaskActionStatus = "确认已拒绝，任务未继续执行。"; break;
        }
    }

    private void ApplyTaskResultStateFromEvent(TaskEvent taskEvent, string message)
    {
        switch (taskEvent.EventType)
        {
            case TaskEventType.TaskCompleted: HasTaskResult = true; TaskResultStatusText = "已完成"; TaskResultPreview = message; TaskActionStatus = "任务完成，可以复制或保存结果。"; CanRetryTask = false; if (!IsBusy) { IsTaskActive = false; CanCancelTask = false; } break;
            case TaskEventType.TaskFailed: HasTaskResult = true; TaskResultStatusText = "执行失败"; TaskResultPreview = message; TaskActionStatus = "任务失败，可以重试。"; CanRetryTask = true; if (!IsBusy) { IsTaskActive = false; CanCancelTask = false; } break;
            case TaskEventType.TaskCancelled: HasTaskResult = true; TaskResultStatusText = "已取消"; TaskResultPreview = message; TaskActionStatus = "任务已取消。"; CanRetryTask = true; if (!IsBusy) { IsTaskActive = false; CanCancelTask = false; } break;
            case TaskEventType.ToolCallStarted: TaskActionStatus = $"正在执行工具：{GetMetadataString(taskEvent, "toolName", "未知工具")}"; break;
            case TaskEventType.ToolCallCompleted: TaskActionStatus = $"工具执行完成：{GetMetadataString(taskEvent, "toolName", "未知工具")}"; break;
            case TaskEventType.ToolCallFailed: TaskActionStatus = $"工具执行失败：{GetMetadataString(taskEvent, "toolName", "未知工具")}"; break;
        }
    }

    // ── 时间线 / 工具调用 ──
    private void AddTimelineItem(MascotState state, string message, int progress, DateTime createdAt)
    {
        foreach (var item in TaskTimeline) item.IsCurrent = false;
        TaskTimeline.Add(new TaskTimelineItem(state, message, progress, createdAt, isCurrent: true));
    }

    private void AddToolCallRecord(TaskEvent taskEvent)
    {
        var detail = string.IsNullOrWhiteSpace(taskEvent.Message) ? GetDefaultStatusMessage(taskEvent.State) : taskEvent.Message;
        switch (taskEvent.EventType)
        {
            case TaskEventType.ToolCallStarted: AddToolCallRecord(GetMetadataString(taskEvent, "toolName", "未知工具"), "运行中", GetMetadataString(taskEvent, "input", detail), taskEvent.CreatedAt); return;
            case TaskEventType.ToolCallCompleted: AddToolCallRecord(GetMetadataString(taskEvent, "toolName", "未知工具"), "已完成", GetMetadataString(taskEvent, "output", detail), taskEvent.CreatedAt); return;
            case TaskEventType.ToolCallFailed: AddToolCallRecord(GetMetadataString(taskEvent, "toolName", "未知工具"), "失败", GetMetadataString(taskEvent, "output", detail), taskEvent.CreatedAt); return;
            case TaskEventType.PermissionRequested: AddToolCallRecord("权限确认", "等待确认", ResolvePermissionDescription(taskEvent, detail), taskEvent.CreatedAt); return;
            case TaskEventType.PermissionGranted: AddToolCallRecord("权限确认", "已授权", detail, taskEvent.CreatedAt); return;
            case TaskEventType.PermissionDenied: AddToolCallRecord("权限确认", "已拒绝", detail, taskEvent.CreatedAt); return;
            case TaskEventType.MemorySaveRequested: AddToolCallRecord("记忆确认", "等待确认", GetMetadataString(taskEvent, "content", detail), taskEvent.CreatedAt); return;
            case TaskEventType.MemorySaveCompleted: AddToolCallRecord("记忆保存", "已完成", detail, taskEvent.CreatedAt); return;
            case TaskEventType.MemorySaveRejected: AddToolCallRecord("记忆保存", "已拒绝", detail, taskEvent.CreatedAt); return;
        }
        var (toolName, statusText) = taskEvent.State switch
        {
            MascotState.Listening => ("任务入口", "已接收"), MascotState.Understanding => ("意图解析", "处理中"),
            MascotState.ReadingContext => ("上下文读取", "处理中"), MascotState.Planning => ("执行规划", "处理中"),
            MascotState.WaitingApproval => ("权限确认", "等待确认"), MascotState.Working => ("任务执行", "运行中"),
            MascotState.MemoryConfirm => ("记忆确认", "等待确认"), MascotState.Reporting => ("结果整理", "处理中"),
            MascotState.Completed => ("任务执行", "已完成"), MascotState.Error => ("错误处理", "异常"), _ => ("状态更新", detail)
        };
        AddToolCallRecord(toolName, statusText, detail, taskEvent.CreatedAt);
    }

    private void AddToolCallRecord(string toolName, string statusText, string detail, DateTime createdAt)
    {
        TaskToolCalls.Add(new TaskToolCallItem(toolName, statusText, detail, createdAt));
        while (TaskToolCalls.Count > 10) TaskToolCalls.RemoveAt(0);
        HasToolCallRecords = TaskToolCalls.Count > 0;
    }

    private void TrimTimeline() { while (TaskTimeline.Count > 12) TaskTimeline.RemoveAt(0); }

    // ── Computer Use 事件 ──
    private void ResetComputerUsePanel(bool isVisible)
    {
        ComputerUseActions.Clear(); ComputerUseLogItems.Clear(); IsComputerUsePanelVisible = isVisible;
        ComputerUseModeText = isVisible ? "待执行" : "未接入"; ComputerUseStatusText = isVisible ? "等待动作事件" : "等待 Computer Use 事件";
        ComputerUseTargetText = isVisible ? "当前桌面" : "暂无目标"; ComputerUseScreenshotImage = null;
        ComputerUseScreenshotStatus = isVisible ? "等待屏幕观察截图。" : "等待 Computer Use 事件。";
        ComputerUseControlStatus = isVisible ? "Computer Use 控制入口已准备。" : "等待 MiMo Computer Use 接入事件流。";
        NotifyComputerUseActionStateChanged();
        NotifyComputerUseLogStateChanged();
    }

    private void PrimeComputerUsePanel(string actionName, string target, string statusText, string detail)
    {
        if (!IsComputerUsePanelVisible) { IsComputerUsePanelVisible = true; ComputerUseStatusText = "人工控制请求"; ComputerUseTargetText = target; }
        AddComputerUseActionRecord(actionName, target, statusText, detail, DateTime.UtcNow);
        AddComputerUseLogRecord(actionName, detail, statusText, DateTime.UtcNow);
    }

    private void ApplyComputerUseStateFromEvent(TaskEvent taskEvent, string message)
    {
        var isCuEvent = IsComputerUseEvent(taskEvent);
        if (!isCuEvent && !IsComputerUsePanelVisible) return;
        IsComputerUsePanelVisible = true;
        ComputerUseModeText = ResolveComputerUseModeText(taskEvent); ComputerUseStatusText = CleanText(message, GetEventStepText(taskEvent), 80);
        ComputerUseTargetText = ResolveComputerUseTarget(taskEvent, ComputerUseTargetText); ComputerUseControlStatus = ResolveComputerUseControlStatus(taskEvent, message);
        UpdateComputerUseScreenshot(taskEvent);
        if (isCuEvent)
        {
            AddComputerUseActionRecord(taskEvent, message);
            AddComputerUseLogRecord(taskEvent, message);
        }
    }

    private void AddComputerUseActionRecord(TaskEvent taskEvent, string message) => AddComputerUseActionRecord(ResolveComputerUseActionName(taskEvent), ResolveComputerUseTarget(taskEvent, ComputerUseTargetText), ResolveComputerUseActionStatus(taskEvent), ResolveComputerUseDetail(taskEvent, message), taskEvent.CreatedAt);

    private void AddComputerUseActionRecord(string actionName, string target, string statusText, string detail, DateTime createdAt)
    {
        foreach (var item in ComputerUseActions) item.IsCurrent = false;
        ComputerUseActions.Add(new ComputerUseActionItem(CleanText(actionName, "桌面动作", 24), CleanText(target, "当前桌面", 48), CleanText(statusText, "进行中", 12), CleanText(detail, "等待事件详情", 120), createdAt) { IsCurrent = true });
        while (ComputerUseActions.Count > 8) ComputerUseActions.RemoveAt(0);
        NotifyComputerUseActionStateChanged();
    }

    private void AddComputerUseLogRecord(TaskEvent taskEvent, string message) =>
        AddComputerUseLogRecord(ResolveComputerUseActionName(taskEvent), ResolveComputerUseDetail(taskEvent, message), ResolveComputerUseActionStatus(taskEvent), taskEvent.CreatedAt);

    private void AddComputerUseLogRecord(string title, string detail, string statusText, DateTime createdAt)
    {
        ComputerUseLogItems.Insert(0, new ComputerUseLogItem(CleanText(title, "桌面事件", 28), CleanText(detail, "Computer Use 状态已更新。", 140), CleanText(statusText, "进行中", 12), createdAt));
        while (ComputerUseLogItems.Count > 12) ComputerUseLogItems.RemoveAt(ComputerUseLogItems.Count - 1);
        NotifyComputerUseLogStateChanged();
    }

    private void UpdateComputerUseScreenshot(TaskEvent taskEvent)
    {
        var path = GetFirstMetadataString(taskEvent, "screenshotPath", "screenPath", "imagePath", "capturePath", "previewPath");
        if (string.IsNullOrWhiteSpace(path)) return;
        try
        {
            if (!File.Exists(path)) { ComputerUseScreenshotStatus = $"截图文件不存在：{path}"; return; }
            ComputerUseScreenshotImage = new Avalonia.Media.Imaging.Bitmap(path);
            ComputerUseScreenshotStatus = $"屏幕截图：{Path.GetFileName(path)}";
        }
        catch (Exception ex) { ComputerUseScreenshotImage = null; ComputerUseScreenshotStatus = $"截图加载失败：{ex.Message}"; }
    }

    private void TryCancelComputerUseTask(string requestedStatus)
    {
        if (string.IsNullOrWhiteSpace(ActiveTaskId)) { ComputerUseControlStatus = $"{requestedStatus} 当前没有活动任务 ID。"; return; }
        if (_taskRouter.CancelTask(ActiveTaskId)) { CanCancelTask = false; ComputerUseControlStatus = $"{requestedStatus} 已向任务路由发送取消请求。"; TaskActionStatus = "已请求中断 Computer Use。"; StatusMessage = "正在中断 Computer Use..."; return; }
        ComputerUseControlStatus = $"{requestedStatus} 当前任务路由未接受取消请求，可能任务已结束。";
    }

    private void NotifyComputerUseActionStateChanged() { OnPropertyChanged(nameof(HasComputerUseActions)); OnPropertyChanged(nameof(HasNoComputerUseActions)); }
    private void NotifyComputerUseLogStateChanged() { OnPropertyChanged(nameof(HasComputerUseLogItems)); OnPropertyChanged(nameof(HasNoComputerUseLogItems)); }
    private void NotifyMessageStateChanged() { OnPropertyChanged(nameof(HasMessages)); OnPropertyChanged(nameof(HasNoMessages)); }
    private void NotifyTaskHistoryStateChanged() { OnPropertyChanged(nameof(HasTaskHistory)); OnPropertyChanged(nameof(HasNoTaskHistory)); }

    // ── Computer Use 判断 ──
    private static bool IsComputerUseTask(AgentTask task, string typeText, string userMessage)
    {
        if (task.Type is TaskType.ScreenUnderstand or TaskType.ComputerUse) return true;
        if (ContainsComputerUseSignal(typeText) || ContainsComputerUseSignal(task.Title) || ContainsComputerUseSignal(task.Input) || ContainsComputerUseSignal(userMessage)) return true;
        foreach (var p in task.Parameters) { if (ContainsComputerUseSignal(p.Key) || ContainsComputerUseSignal(p.Value?.ToString())) return true; }
        return false;
    }

    private static bool IsComputerUseEvent(TaskEvent taskEvent)
    {
        if (ContainsComputerUseSignal(taskEvent.Message)) return true;
        if (taskEvent.Metadata is null) return false;
        foreach (var item in taskEvent.Metadata) { if (ContainsComputerUseSignal(item.Key) || ContainsComputerUseSignal(item.Value?.ToString())) return true; }
        return false;
    }

    private static bool ContainsComputerUseSignal(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        var text = value.ToLowerInvariant();
        return text.Contains("computer use") || text.Contains("computer_use") || text.Contains("computeruse")
            || text.Contains("desktop action") || text.Contains("desktop_action") || text.Contains("screen action") || text.Contains("screen_action")
            || text.Contains("screen observe") || text.Contains("screen_observe") || text.Contains("screenshot") || text.Contains("screen capture")
            || text.Contains("actionplanned") || text.Contains("actionexecuting") || text.Contains("actioncompleted")
            || text.Contains("mouse") || text.Contains("keyboard") || text.Contains("click") || text.Contains("type_text") || text.Contains("hotkey") || text.Contains("scroll")
            || text.Contains("控制电脑") || text.Contains("操作电脑") || text.Contains("桌面操作") || text.Contains("观察屏幕") || text.Contains("规划动作")
            || text.Contains("执行动作") || text.Contains("自动桌面") || text.Contains("人工接管") || text.Contains("鼠标") || text.Contains("键盘")
            || text.Contains("点击") || text.Contains("滚动") || text.Contains("快捷键") || text.Contains("输入文本");
    }

    // ── Computer Use 状态解析 ──
    private static string ResolveComputerUseModeText(TaskEvent e) => e.EventType switch
    {
        TaskEventType.PermissionRequested => "待确认", TaskEventType.PermissionDenied => "已拒绝", TaskEventType.TaskCompleted => "完成",
        TaskEventType.TaskFailed => "失败", TaskEventType.TaskCancelled => "已停止", TaskEventType.ToolCallStarted => "执行中",
        TaskEventType.ToolCallCompleted => "已完成", TaskEventType.ToolCallFailed => "失败",
        _ => e.State switch { MascotState.WaitingApproval => "待确认", MascotState.ReadingContext => "观察中", MascotState.Planning => "规划中", MascotState.Working => "执行中", MascotState.Completed => "完成", MascotState.Error => "异常", _ => "准备中" }
    };

    private static string ResolveComputerUseActionName(TaskEvent e)
    {
        var action = GetFirstMetadataString(e, "computerAction", "action", "operation", "step", "toolName");
        if (!string.IsNullOrWhiteSpace(action)) return action;
        if (e.Message.Contains("观察屏幕")) return "观察屏幕";
        if (e.Message.Contains("规划")) return "动作规划";
        if (e.Message.Contains("等待确认")) return "等待确认";
        if (e.Message.Contains("用户已接管")) return "人工接管";
        return e.EventType switch { TaskEventType.TaskStarted => "任务接收", TaskEventType.ToolCallStarted => "工具执行", TaskEventType.ToolCallCompleted => "工具完成", TaskEventType.ToolCallFailed => "工具失败", TaskEventType.PermissionRequested => "权限确认", TaskEventType.TaskCompleted => "任务完成", TaskEventType.TaskFailed => "任务失败", TaskEventType.TaskCancelled => "任务停止", _ => GetEventStepText(e) };
    }

    private static string ResolveComputerUseActionStatus(TaskEvent e)
    {
        var status = GetFirstMetadataString(e, "status", "state");
        if (!string.IsNullOrWhiteSpace(status)) return status;
        return e.EventType switch { TaskEventType.PermissionRequested => "待确认", TaskEventType.PermissionGranted => "已授权", TaskEventType.PermissionDenied => "已拒绝", TaskEventType.ToolCallStarted => "执行中", TaskEventType.ToolCallCompleted => "已完成", TaskEventType.ToolCallFailed => "失败", TaskEventType.TaskCompleted => "已完成", TaskEventType.TaskFailed => "失败", TaskEventType.TaskCancelled => "已停止", _ => e.State switch { MascotState.ReadingContext => "观察中", MascotState.Planning => "规划中", MascotState.WaitingApproval => "待确认", MascotState.Completed => "已完成", MascotState.Error => "失败", _ => "进行中" } };
    }

    private static string ResolveComputerUseDetail(TaskEvent e, string message) => string.IsNullOrWhiteSpace(GetFirstMetadataString(e, "detail", "input", "output", "reason", "result")) ? message : GetFirstMetadataString(e, "detail", "input", "output", "reason", "result");

    private static string ResolveComputerUseTarget(TaskEvent e, string fallback)
    {
        var target = GetFirstMetadataString(e, "target", "targetName", "windowTitle", "window", "application", "app", "element", "url", "region", "coordinates");
        if (!string.IsNullOrWhiteSpace(target)) return target;
        var x = GetFirstMetadataString(e, "x", "screenX");
        var y = GetFirstMetadataString(e, "y", "screenY");
        if (!string.IsNullOrWhiteSpace(x) && !string.IsNullOrWhiteSpace(y)) return $"坐标 {x}, {y}";
        return string.IsNullOrWhiteSpace(fallback) || fallback == "暂无目标" ? "当前桌面" : fallback;
    }

    private static string ResolveComputerUseControlStatus(TaskEvent e, string message) => e.EventType switch { TaskEventType.PermissionRequested => "等待权限确认弹窗处理。", TaskEventType.PermissionDenied => "权限已拒绝，Computer Use 未继续执行。", TaskEventType.TaskCompleted => "Computer Use 任务已完成。", TaskEventType.TaskFailed => "Computer Use 任务执行失败，可交给用户接管或重试。", TaskEventType.TaskCancelled => "Computer Use 任务已停止。", TaskEventType.ToolCallStarted => "正在执行桌面动作，可随时停止或接管。", TaskEventType.ToolCallCompleted => "桌面动作已完成，等待下一步。", _ => CleanText(message, "Computer Use 状态已更新。", 80) };

    private static string GetFirstMetadataString(TaskEvent e, params string[] keys)
    {
        if (e.Metadata is null) return string.Empty;
        foreach (var key in keys) { if (e.Metadata.TryGetValue(key, out var value) && value is not null) { var text = value.ToString(); if (!string.IsNullOrWhiteSpace(text)) return text; } }
        return string.Empty;
    }
}
