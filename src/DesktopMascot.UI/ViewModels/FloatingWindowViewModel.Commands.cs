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
        if (_pendingConfirmationTask is null)
        {
            if (ApproveComputerUseAction())
                return;

            return;
        }
        var task = _pendingConfirmationTask;
        _pendingConfirmationTask = null; _pendingConfirmationInput = string.Empty; IsWaitingForUserConfirmation = false;
        TaskActionStatus = "已确认，开始执行任务。"; Messages.Add($"{CharacterName}：已确认，开始执行。");
        await ExecutePreparedTaskAsync(task);
    }

    [RelayCommand(CanExecute = nameof(CanResolvePendingConfirmation))]
    private void DenyPendingTask()
    {
        if (_pendingConfirmationTask is null)
        {
            if (DenyComputerUseAction())
                return;

            return;
        }
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
        if (_computerUseControlService.Pause())
        {
            PrimeComputerUsePanel("Pause Computer Use", "Current desktop", "Paused", "Computer Use is paused and the current session is kept alive.");
            ComputerUseModeText = "Paused";
            ComputerUseStatusText = "Computer Use is paused";
            ComputerUseControlStatus = "已暂停 Computer Use，会话保留，可点击继续恢复。";
            CanRetryTask = true;
            return;
        }

        PrimeComputerUsePanel("暂停操作", "当前任务", "已暂停", "已暂停自动桌面操作，可人工处理后继续。");
        ComputerUseModeText = "已暂停";
        ComputerUseStatusText = "自动桌面操作已暂停";
        ComputerUseControlStatus = TryCancelComputerUseTask("已暂停 Computer Use。可点击继续重新执行上一桌面目标。");
        CanRetryTask = true;
    }

    [RelayCommand]
    private async Task ResumeComputerUse()
    {
        if (_computerUseControlService.Resume())
        {
            PrimeComputerUsePanel("Resume Computer Use", "Current desktop", "Resumed", "Computer Use session resumed.");
            ComputerUseModeText = "Executing";
            ComputerUseControlStatus = "Computer Use 已恢复，等待 Agent 返回后续动作事件。";
            return;
        }

        if (string.IsNullOrWhiteSpace(_lastUserMessage))
        {
            PrimeComputerUsePanel("继续操作", "当前桌面", "无法继续", "没有可继续的上一条桌面目标。");
            ComputerUseModeText = "待执行";
            ComputerUseControlStatus = "没有可继续的上一条 Computer Use 目标，请重新输入桌面操作需求。";
            return;
        }

        var resumeInput = BuildComputerUseResumeInput(_lastUserMessage);
        var task = new AgentTask
        {
            Title = "继续桌面自动操作",
            Input = resumeInput,
            Type = TaskType.ComputerUse,
            RequiredPermission = PermissionLevel.L2_ScreenBrowser
        };

        PrepareTaskSurface(task, "Computer Use", resumeInput);
        AddComputerUseActionRecord("继续操作", ResolveComputerUseTarget(task, resumeInput), "重新派发", resumeInput, DateTime.UtcNow);
        ComputerUseModeText = "执行中";
        ComputerUseControlStatus = "已重新派发上一桌面目标，等待 Agent 返回动作事件。";
        await ExecutePreparedTaskAsync(task);
    }

    [RelayCommand]
    private void TakeOverComputerUse()
    {
        if (_computerUseControlService.Takeover())
        {
            PrimeComputerUsePanel("Take over", "Current desktop", "Taken over", "User took over the Computer Use session.");
            ComputerUseModeText = "Takeover";
            ComputerUseStatusText = "User took over the desktop";
            ComputerUseControlStatus = "已接管 Computer Use，自动动作已停止等待用户。";
            return;
        }

        PrimeComputerUsePanel("人工接管", "当前桌面", "已接管", "已切换为人工接管，自动桌面操作会停止。");
        ComputerUseModeText = "人工接管";
        ComputerUseStatusText = "用户正在接管桌面";
        ComputerUseControlStatus = TryCancelComputerUseTask("已接管 Computer Use，正在停止自动桌面操作。");
    }

    [RelayCommand]
    private void StopComputerUse()
    {
        _computerUseControlService.Takeover();

        PrimeComputerUsePanel("停止操作", "当前任务", "已停止", "已停止 Computer Use 自动操作。");
        ComputerUseModeText = "已停止";
        ComputerUseStatusText = "自动桌面操作已停止";
        ComputerUseControlStatus = TryCancelComputerUseTask("已停止 Computer Use。");
    }

    [RelayCommand]
    private void HideComputerUsePanel()
    {
        _isComputerUsePanelDismissedByUser = true;
        IsComputerUsePanelVisible = false;
        ComputerUseControlStatus = "Computer Use 面板已收起，任务状态仍会在后台更新。";
    }

    [RelayCommand]
    private async Task ToggleVoiceInput()
    {
        if (!IsVoiceRecording)
        {
            var startResult = _voiceInputService.StartRecording(InlineSettings.SpeechRecognitionLanguage);
            if (!startResult.Success)
            {
                IsVoiceRecording = false;
                VoiceInputStatus = $"录音启动失败：{startResult.Error ?? startResult.Message}";
                return;
            }

            IsVoiceRecording = true;
            VoiceInputStatus = $"正在录音：{InlineSettings.SpeechRecognitionLanguage}，再次点击结束并转写。";
            return;
        }

        IsVoiceRecording = false;
        VoiceInputStatus = "录音已结束，正在转写...";

        try
        {
            var result = await _voiceInputService.StopAndRecognizeAsync(InlineSettings.SpeechRecognitionLanguage);
            if (!result.Success || string.IsNullOrWhiteSpace(result.Text))
            {
                var detail = result.Error ?? result.Message;
                if (!string.IsNullOrWhiteSpace(result.AudioFilePath))
                    detail = $"{detail}（录音文件：{result.AudioFilePath}）";
                VoiceInputStatus = $"语音转写失败：{detail}";
                return;
            }

            InputText = result.Text.Trim();
            VoiceInputStatus = $"已识别：{CleanText(InputText, InputText, 42)}";

            if (CanSendMessage)
                await SendMessage();
        }
        catch (Exception ex)
        {
            VoiceInputStatus = $"语音输入失败：{ex.Message}";
        }
    }

    [RelayCommand]
    private void StopVoiceReply()
    {
        if (!IsVoiceReplyPlaying)
        {
            VoiceReplyStatus = "当前没有正在朗读的回复。";
            return;
        }

        var result = _audioPlaybackService.Stop();
        IsVoiceReplyPlaying = false;
        VoiceReplyStatus = result.Success
            ? "语音播放已停止。"
            : $"停止播放失败：{result.Error ?? result.Message}";
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

    private bool ApproveComputerUseAction()
    {
        if (!IsComputerUsePanelVisible || !_computerUseControlService.ApproveCurrentAction())
            return false;

        IsWaitingForUserConfirmation = false;
        PendingConfirmationDescription = "Computer Use action approved.";
        ComputerUseControlStatus = "已批准当前 Computer Use 动作，等待 Agent 继续执行。";
        AddComputerUseLogRecord("Approve action", PendingConfirmationDescription, "Approved", DateTime.UtcNow);
        OnPropertyChanged(nameof(CanResolvePendingConfirmation));
        ApprovePendingTaskCommand.NotifyCanExecuteChanged();
        DenyPendingTaskCommand.NotifyCanExecuteChanged();
        return true;
    }

    private bool DenyComputerUseAction()
    {
        if (!IsComputerUsePanelVisible || !_computerUseControlService.DenyCurrentAction("用户拒绝了 Computer Use 动作。"))
            return false;

        IsWaitingForUserConfirmation = false;
        PendingConfirmationDescription = "Computer Use action denied.";
        ComputerUseControlStatus = "已拒绝当前 Computer Use 动作，Agent 会停止该自动操作。";
        AddComputerUseLogRecord("Deny action", PendingConfirmationDescription, "Denied", DateTime.UtcNow);
        OnPropertyChanged(nameof(CanResolvePendingConfirmation));
        ApprovePendingTaskCommand.NotifyCanExecuteChanged();
        DenyPendingTaskCommand.NotifyCanExecuteChanged();
        return true;
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
            "breeze" or "yan" => new MascotCharacterProfile { Name = "微风", Role = "寻研01桌面助手", AvatarText = "微", Personality = "沉稳可靠", Catchphrase = "我是微风，可以继续接任务。", AccentColor = "#2563EB", BackgroundColor = "#EEF6FF", ImageFolder = "assets/characters/yan" },
            "moonlight" or "yue guang" => new MascotCharacterProfile { Name = "月光", Role = "寻研01夜间助手", AvatarText = "月", Personality = "安静细致", Catchphrase = "我是月光，会安静地帮你整理任务。", AccentColor = "#7C3AED", BackgroundColor = "#F5F3FF", ImageFolder = "assets/characters/yue guang" },
            "fisher" or "feng lin yu ren" => new MascotCharacterProfile(),
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
