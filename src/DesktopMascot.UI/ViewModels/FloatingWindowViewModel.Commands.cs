using CommunityToolkit.Mvvm.Input;
using DesktopMascot.Core.Enums;
using DesktopMascot.Core.Memory;
using DesktopMascot.Core.Models;
using DesktopMascot.UI.Services;

namespace DesktopMascot.UI.ViewModels;

/// <summary>FloatingWindowViewModel — 所有 RelayCommand</summary>
public partial class FloatingWindowViewModel
{
    [RelayCommand]
    private void ToggleChat()
    {
        if (IsChatDialogVisible) { CollapseDialog(); return; }
        ExpandDialog();
    }

    [RelayCommand] private void CloseChat() { CollapseDialog(); IsCharacterPanelVisible = false; }

    [RelayCommand]
    private void ToggleCharacterPanel() { IsCharacterPanelVisible = !IsCharacterPanelVisible; IsChatVisible = true; }

    [RelayCommand]
    private void OpenAppearanceSettings() { IsCharacterPanelVisible = false; OpenSettingsPanel("appearance"); }

    [RelayCommand(CanExecute = nameof(CanSendMessage))]
    private async Task SendMessage()
    {
        if (string.IsNullOrWhiteSpace(InputText)) return;
        var userMessage = InputText;
        InputText = string.Empty;
        MessageItems.Add(new MessageItem { Role = "user", Content = userMessage });
        Messages.Add($"你：{userMessage}");

        var metadata = BuildTaskMetadata(userMessage);
        var task = new AgentTask { Title = metadata.Title, Input = userMessage, Type = metadata.Type, RequiredPermission = metadata.Permission };
        PrepareTaskSurface(task, metadata.TypeText, userMessage);

        if (RequiresUserConfirmation(task)) { if (!await ShowPendingConfirmationAsync(task, userMessage)) return; }
        await ExecutePreparedTaskAsync(task);
    }

    [RelayCommand(CanExecute = nameof(CanCancelCurrentTask))]
    private void CancelCurrentTask()
    {
        if (string.IsNullOrWhiteSpace(ActiveTaskId)) return;
        if (_taskRouter.CancelTask(ActiveTaskId)) { CanCancelTask = false; StatusMessage = "正在中断任务..."; Messages.Add($"{CharacterName}：已请求中断当前任务。"); }
    }

    [RelayCommand(CanExecute = nameof(CanResolvePendingConfirmation))]
    private async Task ApprovePendingTask()
    {
        if (_pendingConfirmationTask is null) return;
        var task = _pendingConfirmationTask;
        _pendingConfirmationTask = null; _pendingConfirmationInput = string.Empty; IsWaitingForUserConfirmation = false;
        TaskActionStatus = "已确认，开始执行任务。"; Messages.Add($"{CharacterName}：已确认，开始执行。");
        await ExecutePreparedTaskAsync(task);
    }

    [RelayCommand(CanExecute = nameof(CanResolvePendingConfirmation))]
    private void DenyPendingTask()
    {
        if (_pendingConfirmationTask is null) return;
        _pendingConfirmationTask = null; _pendingConfirmationInput = string.Empty; IsWaitingForUserConfirmation = false;
        IsTaskActive = false; CanCancelTask = false; IsBusy = false;
        CurrentState = MascotState.Error; CurrentStateText = GetStateText(CurrentState); CurrentProgress = 100; TaskProgressText = "100%";
        ActiveStepText = "已拒绝"; StateHint = GetStateHint(CurrentState); StateAccentBrush = GetAccentBrush(CurrentState); MascotBackgroundBrush = GetMascotBackgroundBrush(CurrentState);
        StatusMessage = "用户拒绝了操作"; HasTaskResult = true; TaskResultPreview = "用户拒绝了操作。"; TaskResultStatusText = "已拒绝";
        TaskActionStatus = "操作已拒绝，未执行任务。"; CanRetryTask = true;
        AddTimelineItem(MascotState.Error, "用户拒绝了操作", CurrentProgress, DateTime.UtcNow);
        AddToolCallRecord("权限确认", "已拒绝", PendingConfirmationRiskText, DateTime.UtcNow);
        Messages.Add($"{CharacterName}：已取消该操作。");
    }

    [RelayCommand]
    private void PauseComputerUse()
    {
        PrimeComputerUsePanel("暂停请求", "人工控制", "已请求", "已请求暂停自动桌面操作。");
        ComputerUseModeText = "暂停中";
        ComputerUseControlStatus = "暂停请求已记录；底层暂停 API 暂未开放，敏感动作仍会走权限确认。";
    }

    [RelayCommand]
    private void ResumeComputerUse()
    {
        PrimeComputerUsePanel("继续请求", "人工控制", "已请求", "已请求继续自动桌面操作。");
        ComputerUseModeText = "执行中";
        ComputerUseControlStatus = "继续请求已记录；后续接入 Agent 控制 API 后会恢复自动动作。";
    }

    [RelayCommand]
    private void TakeOverComputerUse()
    {
        PrimeComputerUsePanel("人工接管", "当前桌面", "已请求", "已请求人工接管当前桌面操作。");
        ComputerUseModeText = "接管中";
        TryCancelComputerUseTask("已请求人工接管，正在停止自动桌面操作。");
    }

    [RelayCommand]
    private void StopComputerUse()
    {
        PrimeComputerUsePanel("停止请求", "当前任务", "已请求", "已请求停止 Computer Use 自动操作。");
        ComputerUseModeText = "停止中";
        TryCancelComputerUseTask("已请求停止 Computer Use 自动操作。");
    }

    [RelayCommand]
    private void ToggleVoiceInput()
    {
        IsVoiceRecording = !IsVoiceRecording;
        VoiceInputStatus = IsVoiceRecording
            ? $"录音状态：{InlineSettings.SpeechRecognitionLanguage}，点击结束录音。"
            : "录音已结束；STT 服务接入后会自动转写并发送。";
    }

    [RelayCommand]
    private void StopVoiceReply()
    {
        if (!IsVoiceReplyPlaying)
        {
            VoiceReplyStatus = "当前没有正在朗读的回复。";
            return;
        }

        IsVoiceReplyPlaying = false;
        VoiceReplyStatus = "语音播放已停止。";
    }

    [RelayCommand]
    private void OpenComputerUsePermission()
    {
        PrimeComputerUsePanel("权限入口", "权限确认", "已打开", "已定位到当前 Computer Use 权限状态。");
        if (IsWaitingForUserConfirmation)
        {
            ComputerUseControlStatus = $"等待用户处理：{PendingConfirmationTitle} / {PendingConfirmationRiskText}。";
            return;
        }
        OpenSettingsPanel();
        SelectInlineSettingsSection("permission");
        ComputerUseControlStatus = "当前没有待确认权限，已打开权限设置页。";
    }

    [RelayCommand(CanExecute = nameof(CanUseTaskResultActions))]
    private async Task CopyTaskResult() { var copied = await _taskResultActionService.CopyToClipboardAsync(TaskResultPreview); TaskActionStatus = copied ? "结果已复制到剪贴板。" : "复制失败：当前没有可用剪贴板。"; }

    [RelayCommand(CanExecute = nameof(CanUseTaskResultActions))]
    private async Task SaveTaskResult() { var path = await _taskResultActionService.SaveResultAsync(ActiveTaskTitle, BuildTaskResultDocument()); TaskActionStatus = $"结果已保存：{path}"; }

    [RelayCommand(CanExecute = nameof(CanRetryCurrentTask))]
    private async Task RetryTask()
    {
        if (string.IsNullOrWhiteSpace(_lastUserMessage)) return;
        var metadata = BuildTaskMetadata(_lastUserMessage);
        var task = new AgentTask { Title = metadata.Title, Input = _lastUserMessage, Type = metadata.Type, RequiredPermission = metadata.Permission };
        PrepareTaskSurface(task, metadata.TypeText, _lastUserMessage);
        if (RequiresUserConfirmation(task)) { if (!await ShowPendingConfirmationAsync(task, _lastUserMessage)) return; }
        await ExecutePreparedTaskAsync(task);
    }

    [RelayCommand]
    private void SaveCharacter() { ApplyCharacterProfile(BuildCurrentCharacterProfile(), save: true); CharacterSaveStatus = $"已保存 {CharacterName} 的角色设定。"; }

    [RelayCommand]
    private void ResetCharacter() { ApplyCharacterProfile(new MascotCharacterProfile(), save: true); CharacterSaveStatus = "已恢复默认角色。"; }

    [RelayCommand]
    private void UseCharacterPreset(string? presetId)
    {
        var profile = presetId switch
        {
            "developer" => new MascotCharacterProfile { Name = "码伴", Role = "开发调试伙伴", AvatarText = "</>", Personality = "直接严谨", Catchphrase = "我会优先帮你定位问题和验证结果。", AccentColor = "#0F766E", BackgroundColor = "#F0FDFA", ImageFolder = "assets/characters/developer" },
            "operator" => new MascotCharacterProfile { Name = "桌管家", Role = "桌面任务管家", AvatarText = "管", Personality = "有序高效", Catchphrase = "我会把任务拆清楚，再一步步执行。", AccentColor = "#7C2D12", BackgroundColor = "#FFF7ED", ImageFolder = "assets/characters/operator" },
            "study" => new MascotCharacterProfile { Name = "小研", Role = "阅读研究助手", AvatarText = "研", Personality = "耐心清晰", Catchphrase = "我会帮你提炼重点、整理脉络。", AccentColor = "#7C3AED", BackgroundColor = "#F5F3FF", ImageFolder = "assets/characters/study" },
            _ => new MascotCharacterProfile()
        };
        ApplyCharacterProfile(profile, save: true); CharacterSaveStatus = $"已切换到 {CharacterName}。";
    }

    [RelayCommand]
    private void ChooseAccentColor(string? color)
    {
        if (string.IsNullOrWhiteSpace(color)) return;
        CharacterAccentColor = color;
        CharacterBackgroundColor = color switch
        {
            "#0F766E" => "#F0FDFA", "#7C2D12" => "#FFF7ED", "#7C3AED" => "#F5F3FF",
            "#C026D3" => "#FDF4FF", "#DC2626" => "#FEF2F2", "#2563EB" => "#EEF6FF",
            _ => "#F9FAFB"
        };
        RefreshCharacterBrushes();
    }

    [RelayCommand] private void HideWindow() => HideRequested?.Invoke(this, EventArgs.Empty);
    [RelayCommand] private void Exit() => ExitRequested?.Invoke(this, EventArgs.Empty);
    [RelayCommand] private void OpenSettings() => OpenSettingsPanel();

    [RelayCommand]
    private void ExpandDialog() { IsMascotIconVisible = true; IsChatDialogVisible = true; IsChatVisible = true; IsChatPageVisible = true; IsSettingsPageVisible = false; }

    [RelayCommand]
    private void CollapseDialog() { IsChatDialogVisible = false; IsMascotIconVisible = true; IsChatVisible = false; IsChatPageVisible = true; IsSettingsPageVisible = false; }

    [RelayCommand] private void BackToChat() { IsSettingsPageVisible = false; IsChatPageVisible = true; }

    [RelayCommand]
    private void SelectInlineSettingsSection(string? section) { InlineSettings.SelectSectionById(section); ApplyInlineSettingsSection(section); }

    [RelayCommand]
    private async Task NewChat()
    {
        BackToChat();
        await SaveCurrentConversationAsync();
        StartNewConversation();
        MessageItems.Clear(); Messages.Clear();
    }

    [RelayCommand(CanExecute = nameof(CanStartScreenSelection))] private void StartScreenSelection() => RequestScreenSelection();
}
