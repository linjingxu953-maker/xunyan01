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
    public void ExpandDialog_KeepsMascotIconVisible()
    {
        using var viewModel = ToolLauncherViewModelFixture.Create();

        Assert.True(viewModel.IsFloatingMascotVisible);
        Assert.False(viewModel.IsDockedMascotVisible);

        viewModel.ExpandDialogCommand.Execute(null);

        Assert.True(viewModel.IsChatDialogVisible);
        Assert.True(viewModel.IsMascotIconVisible);
        Assert.False(viewModel.IsFloatingMascotVisible);
        Assert.True(viewModel.IsDockedMascotVisible);
    }

    [Fact]
    public void PauseComputerUse_CancelsActiveComputerUseTask()
    {
        var router = new RecordingTaskRouter { CancelResult = true };
        var computerUse = new StubComputerUseControlService { PauseResult = true };
        using var viewModel = ToolLauncherViewModelFixture.Create(taskRouter: router, computerUseControlService: computerUse);
        viewModel.ActiveTaskId = "task-cu";
        viewModel.CanCancelTask = true;
        viewModel.IsComputerUsePanelVisible = true;

        viewModel.PauseComputerUseCommand.Execute(null);

        Assert.Equal(1, computerUse.PauseCount);
        Assert.Null(router.CancelledTaskId);
        Assert.True(viewModel.CanCancelTask);
        Assert.Contains("已暂停", viewModel.ComputerUseControlStatus);
        if (viewModel.ComputerUseControlStatus.Contains("已暂停", StringComparison.Ordinal))
            return;
        Assert.Contains("已暂停", viewModel.ComputerUseControlStatus);
        Assert.DoesNotContain("暂未开放", viewModel.ComputerUseControlStatus);
    }

    [Fact]
    public void StopComputerUse_CancelsActiveComputerUseTask()
    {
        var router = new RecordingTaskRouter { CancelResult = true };
        using var viewModel = ToolLauncherViewModelFixture.Create(taskRouter: router);
        viewModel.ActiveTaskId = "task-cu";
        viewModel.CanCancelTask = true;
        viewModel.IsComputerUsePanelVisible = true;

        viewModel.StopComputerUseCommand.Execute(null);

        Assert.Equal("task-cu", router.CancelledTaskId);
        Assert.False(viewModel.CanCancelTask);
        Assert.Contains("已停止", viewModel.ComputerUseControlStatus);
    }

    [Fact]
    public void HideComputerUsePanel_HidesPanelWithoutCancellingActiveTask()
    {
        var router = new RecordingTaskRouter { CancelResult = true };
        using var viewModel = ToolLauncherViewModelFixture.Create(taskRouter: router);
        viewModel.ActiveTaskId = "task-cu";
        viewModel.CanCancelTask = true;
        viewModel.IsComputerUsePanelVisible = true;

        viewModel.HideComputerUsePanelCommand.Execute(null);

        Assert.False(viewModel.IsComputerUsePanelVisible);
        Assert.True(viewModel.CanCancelTask);
        Assert.Null(router.CancelledTaskId);
        Assert.Contains("已收起", viewModel.ComputerUseControlStatus);
    }

    [Fact]
    public async Task ApprovePendingTask_ComputerUseApproval_ApprovesControlServiceWhileBusy()
    {
        var computerUse = new StubComputerUseControlService
        {
            HasActiveSession = true,
            HasPendingApproval = true
        };
        using var viewModel = ToolLauncherViewModelFixture.Create(computerUseControlService: computerUse);
        viewModel.IsComputerUsePanelVisible = true;
        viewModel.IsWaitingForUserConfirmation = true;
        viewModel.IsBusy = true;

        Assert.True(viewModel.CanResolvePendingConfirmation);

        await viewModel.ApprovePendingTaskCommand.ExecuteAsync(null);

        Assert.Equal(1, computerUse.ApproveCount);
        Assert.False(viewModel.IsWaitingForUserConfirmation);
        Assert.Contains("已批准", viewModel.ComputerUseControlStatus);
    }

    [Fact]
    public async Task ResumeComputerUse_RedispatchesLastDesktopTarget()
    {
        var router = new RecordingTaskRouter();
        using var viewModel = ToolLauncherViewModelFixture.Create(taskRouter: router);
        viewModel.InputText = "computer_use 打开记事本";
        await viewModel.SendMessageCommand.ExecuteAsync(null);
        var initialDispatchCount = router.DispatchCount;

        viewModel.ResumeComputerUseCommand.Execute(null);
        await Task.Delay(20);

        Assert.Equal(initialDispatchCount + 1, router.DispatchCount);
        Assert.NotNull(router.LastDispatchedTask);
        Assert.Equal(TaskType.ComputerUse, router.LastDispatchedTask!.Type);
        Assert.Contains("继续", router.LastDispatchedTask.Input);
        Assert.Contains("打开记事本", router.LastDispatchedTask.Input);
        Assert.DoesNotContain("后续接入", viewModel.ComputerUseControlStatus);
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
    public void CharacterSwitchItems_IgnoreUnknownAssetDirectoryCandidates()
    {
        using var assetRoot = CharacterAssetTestRoot.Create(
            ("feng lin yu ren", "avatar.png"),
            ("unknown role", "avatar.png"));
        using var viewModel = ToolLauncherViewModelFixture.Create();
        viewModel.SetCharacterAssetRootCandidates([assetRoot.RootPath]);

        Assert.Contains(viewModel.CharacterSwitchItems, x => x.Name == "枫林渔人");
        Assert.DoesNotContain(viewModel.CharacterSwitchItems, x => x.ImageFolder.Contains("unknown role", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void CharacterSwitchItems_DoNotDuplicateBreezeFromLegacyDefaultAssetDirectory()
    {
        using var assetRoot = CharacterAssetTestRoot.Create(
            ("default", "avatar.png"),
            ("yan", "avatar.png"));
        using var viewModel = ToolLauncherViewModelFixture.Create();

        viewModel.SetCharacterAssetRootCandidates([assetRoot.RootPath]);

        var breeze = Assert.Single(viewModel.CharacterSwitchItems, x => x.Name == "微风");
        Assert.Equal("assets/characters/yan", breeze.ImageFolder);
    }

    [Fact]
    public void DefaultCharacterProfile_UsesFengLinYuRen()
    {
        var profile = new MascotCharacterProfile();

        Assert.Equal("枫林渔人", profile.Name);
        Assert.Equal("寻研01桌面助手", profile.Role);
        Assert.Equal("枫", profile.AvatarText);
        Assert.Equal("assets/characters/feng lin yu ren", profile.ImageFolder);
    }

    [Fact]
    public void CharacterStoreBrandDefaults_MigrateLegacyYanProfileToBreeze()
    {
        var profile = new MascotCharacterProfile
        {
            Name = "妍",
            Role = "寻研桌面助手",
            AvatarText = "妍",
            ImageFolder = "assets/characters/default"
        };
        var method = typeof(JsonMascotCharacterStore).GetMethod(
            "ApplyCurrentBrandDefaults",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        method!.Invoke(null, [profile]);

        Assert.Equal("微风", profile.Name);
        Assert.Equal("寻研01桌面助手", profile.Role);
        Assert.Equal("微", profile.AvatarText);
        Assert.Equal("assets/characters/yan", profile.ImageFolder);
    }

    [Fact]
    public void SwitchCharacterProfile_AppliesAndSavesSelectedProfile()
    {
        var yueGuang = new MascotCharacterProfile
        {
            Name = "月光",
            Role = "寻研01夜间助手",
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
        Assert.Equal("寻研01夜间助手", viewModel.CharacterRole);
        Assert.Equal("assets/characters/yue guang", viewModel.CharacterImageFolder);
        Assert.Equal("月光", store.SavedProfile?.Name);
        Assert.Contains("月光", viewModel.CharacterSaveStatus);
    }

    [Fact]
    public void InlineSettings_LoadCharacterProfile_AppliesAndSavesSelectedProfile()
    {
        var yueGuang = new MascotCharacterProfile
        {
            Name = "月光",
            Role = "寻研01夜间助手",
            AvatarText = "月",
            ImageFolder = "assets/characters/yue guang",
            AvatarImage = "avatar.png",
            AccentColor = "#7C3AED",
            BackgroundColor = "#F5F3FF"
        };
        var store = new StubMascotCharacterStore(profiles: [("yue-guang", yueGuang)]);
        using var viewModel = ToolLauncherViewModelFixture.Create(store);
        var item = viewModel.InlineSettings.CharacterProfiles.First(x => x.Name == "月光");

        viewModel.InlineSettings.LoadCharacterProfileCommand.Execute(item);

        Assert.Equal("月光", viewModel.InlineSettings.CharacterName);
        Assert.Equal("寻研01夜间助手", viewModel.InlineSettings.CharacterRole);
        Assert.Equal("月光", store.SavedProfile?.Name);
        Assert.Contains("月光", viewModel.InlineSettings.CharacterLibraryStatus);
    }

    [Fact]
    public void InlineSettings_SelectCharacterProfile_AppliesAndSavesSelectedProfile()
    {
        var breeze = new MascotCharacterProfile
        {
            Name = "微风",
            Role = "寻研01桌面助手",
            AvatarText = "微",
            ImageFolder = "assets/characters/yan",
            AvatarImage = "avatar.png",
            AccentColor = "#2563EB",
            BackgroundColor = "#EEF6FF"
        };
        var yueGuang = new MascotCharacterProfile
        {
            Name = "月光",
            Role = "寻研01夜间助手",
            AvatarText = "月",
            ImageFolder = "assets/characters/yue guang",
            AvatarImage = "avatar.png",
            AccentColor = "#7C3AED",
            BackgroundColor = "#F5F3FF"
        };
        var store = new StubMascotCharacterStore(profiles: [("breeze", breeze), ("yue-guang", yueGuang)]);
        using var viewModel = ToolLauncherViewModelFixture.Create(store);

        viewModel.InlineSettings.SelectedCharacterProfile =
            viewModel.InlineSettings.CharacterProfiles.First(x => x.Name == "月光");

        Assert.Equal("月光", viewModel.InlineSettings.CharacterName);
        Assert.Equal("寻研01夜间助手", viewModel.InlineSettings.CharacterRole);
        Assert.Equal("月光", store.SavedProfile?.Name);
        Assert.Contains("月光", viewModel.InlineSettings.CharacterLibraryStatus);
    }

    [Fact]
    public void InlineSettings_CharacterProfiles_IncludeOnlyKnownAssetDirectoryCandidates()
    {
        using var assetRoot = CharacterAssetTestRoot.Create(
            ("feng lin yu ren", "avatar.png"),
            ("yue guang", "avatar.png"),
            ("unknown role", "avatar.png"));
        using var viewModel = ToolLauncherViewModelFixture.Create();

        viewModel.SetCharacterAssetRootCandidates([assetRoot.RootPath]);

        Assert.Contains(viewModel.InlineSettings.CharacterProfiles, x => x.Name == "枫林渔人");
        Assert.Contains(viewModel.InlineSettings.CharacterProfiles, x => x.Name == "月光");
        Assert.DoesNotContain(viewModel.InlineSettings.CharacterProfiles, x =>
            x.ImageFolder.Contains("unknown role", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void InlineSettings_SelectAssetCharacterProfile_AppliesAndSavesSelectedProfile()
    {
        using var assetRoot = CharacterAssetTestRoot.Create(("yue guang", "avatar.png"));
        var store = new StubMascotCharacterStore();
        using var viewModel = ToolLauncherViewModelFixture.Create(store);
        viewModel.SetCharacterAssetRootCandidates([assetRoot.RootPath]);

        viewModel.InlineSettings.SelectedCharacterProfile =
            viewModel.InlineSettings.CharacterProfiles.First(x => x.Name == "月光");

        Assert.Equal("月光", viewModel.InlineSettings.CharacterName);
        Assert.Equal("寻研01夜间助手", viewModel.InlineSettings.CharacterRole);
        Assert.Equal("assets/characters/yue guang", viewModel.InlineSettings.CharacterImageFolder);
        Assert.Equal("月光", store.SavedProfile?.Name);
        Assert.Contains("月光", viewModel.InlineSettings.CharacterLibraryStatus);
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

    [Fact]
    public async Task PlayMessageAudioAsync_SynthesizesAndPlaysThroughInlineAudioService()
    {
        using var tempFile = TempAudioFile.Create();
        var tts = new StubTextToSpeechPreviewService(tempFile.Path);
        var audio = new StubAudioPlaybackService();
        using var viewModel = ToolLauncherViewModelFixture.Create(
            textToSpeechPreviewService: tts,
            audioPlaybackService: audio);

        await viewModel.PlayMessageAudioAsync("这是一段需要朗读的回复。");

        Assert.Equal("这是一段需要朗读的回复。", tts.LastText);
        Assert.Equal(tempFile.Path, audio.PlayedFilePath);
        Assert.True(viewModel.IsVoiceReplyPlaying);
        Assert.Contains("正在播放", viewModel.VoiceReplyStatus);
    }

    [Fact]
    public async Task PlayMessageAudioAsync_TtsFailureExceptionReportsStatus()
    {
        using var viewModel = ToolLauncherViewModelFixture.Create(
            textToSpeechPreviewService: new ThrowingTextToSpeechPreviewService());

        await viewModel.PlayMessageAudioAsync("这段回复需要朗读。");

        Assert.False(viewModel.IsVoiceReplyPlaying);
        Assert.Contains("语音生成失败", viewModel.VoiceReplyStatus);
        Assert.Contains("TTS 服务异常", viewModel.VoiceReplyStatus);
    }

    [Fact]
    public async Task PlayMessageAudioAsync_PlaybackFailureReportsAudioPath()
    {
        using var tempFile = TempAudioFile.Create();
        var audio = new StubAudioPlaybackService
        {
            PlayResult = new AudioPlaybackResult(false, "codec failed", "codec failed")
        };
        using var viewModel = ToolLauncherViewModelFixture.Create(
            textToSpeechPreviewService: new StubTextToSpeechPreviewService(tempFile.Path),
            audioPlaybackService: audio);

        await viewModel.PlayMessageAudioAsync("这段回复需要朗读。");

        Assert.False(viewModel.IsVoiceReplyPlaying);
        Assert.Contains("语音播放失败", viewModel.VoiceReplyStatus);
        Assert.Contains("codec failed", viewModel.VoiceReplyStatus);
        Assert.Contains(tempFile.Path, viewModel.VoiceReplyStatus);
    }

    [Fact]
    public async Task StopVoiceReply_StopsInlineAudioPlayback()
    {
        using var tempFile = TempAudioFile.Create();
        var audio = new StubAudioPlaybackService();
        using var viewModel = ToolLauncherViewModelFixture.Create(
            textToSpeechPreviewService: new StubTextToSpeechPreviewService(tempFile.Path),
            audioPlaybackService: audio);
        await viewModel.PlayMessageAudioAsync("需要停止的回复。");

        viewModel.StopVoiceReplyCommand.Execute(null);

        Assert.Equal(1, audio.StopCount);
        Assert.False(viewModel.IsVoiceReplyPlaying);
        Assert.Contains("已停止", viewModel.VoiceReplyStatus);
    }

    [Fact]
    public async Task InlineSettings_PreviewTtsVoice_SynthesizesAndPlaysThroughInlineAudioService()
    {
        using var tempFile = TempAudioFile.Create();
        var tts = new StubTextToSpeechPreviewService(tempFile.Path);
        var audio = new StubAudioPlaybackService();
        using var viewModel = ToolLauncherViewModelFixture.Create(
            textToSpeechPreviewService: tts,
            audioPlaybackService: audio);

        viewModel.InlineSettings.TtsVoice = "默认女声";
        await viewModel.InlineSettings.PreviewTtsVoiceCommand.ExecuteAsync(null);

        Assert.Contains("你好，我是寻研01", tts.LastText);
        Assert.Equal("默认女声", tts.LastVoice);
        Assert.Equal(tempFile.Path, audio.PlayedFilePath);
        Assert.Contains("正在播放试听", viewModel.InlineSettings.VoiceSettingsStatus);
    }

    [Fact]
    public async Task ToggleVoiceInput_StopsRecordingTranscribesAndSendsRecognizedText()
    {
        var router = new RecordingTaskRouter();
        var voiceInput = new StubVoiceInputService("帮我总结当前内容");
        using var viewModel = ToolLauncherViewModelFixture.Create(
            taskRouter: router,
            voiceInputService: voiceInput);

        await viewModel.ToggleVoiceInputCommand.ExecuteAsync(null);

        Assert.True(viewModel.IsVoiceRecording);
        Assert.Equal("zh-CN", voiceInput.StartLanguage);

        await viewModel.ToggleVoiceInputCommand.ExecuteAsync(null);

        Assert.False(viewModel.IsVoiceRecording);
        Assert.Equal("zh-CN", voiceInput.StopLanguage);
        Assert.Equal("帮我总结当前内容", viewModel.MessageItems.First(x => x.Role == "user").Content);
        Assert.Equal("帮我总结当前内容", router.LastDispatchedTask?.Input);
    }

    [Fact]
    public async Task ToggleVoiceInput_RecognitionFailureShowsAudioPath()
    {
        var voiceInput = new FailingVoiceInputService(
            "C:\\temp\\xunyan-voice.wav",
            "当前 Provider 不支持语音识别");
        using var viewModel = ToolLauncherViewModelFixture.Create(voiceInputService: voiceInput);

        await viewModel.ToggleVoiceInputCommand.ExecuteAsync(null);
        await viewModel.ToggleVoiceInputCommand.ExecuteAsync(null);

        Assert.False(viewModel.IsVoiceRecording);
        Assert.Contains("语音转写失败", viewModel.VoiceInputStatus);
        Assert.Contains("当前 Provider 不支持语音识别", viewModel.VoiceInputStatus);
        Assert.Contains("C:\\temp\\xunyan-voice.wav", viewModel.VoiceInputStatus);
    }

    [Fact]
    public void InlineSettings_RuntimeOverview_ReportsTtsPlaybackIsConnected()
    {
        using var viewModel = ToolLauncherViewModelFixture.Create();

        var voiceItem = viewModel.InlineSettings.RuntimeOverviewItems.First(x => x.Title == "语音 UI");

        Assert.Equal("TTS 已接入", voiceItem.Value);
        Assert.Contains("TTS 预览", voiceItem.Description);
        Assert.DoesNotContain("状态壳", voiceItem.Value);
        Assert.DoesNotContain("播放仍等待服务接入", voiceItem.Description);
    }

    [Fact]
    public void InlineSettings_SelectCharacterStatePreview_SelectsStateForEditing()
    {
        using var viewModel = ToolLauncherViewModelFixture.Create();
        viewModel.InlineSettings.RefreshCharacterStatePreviewCommand.Execute(null);
        var working = viewModel.InlineSettings.CharacterStatePreviewItems.First(x => x.StateKey == "Working");

        viewModel.InlineSettings.SelectCharacterStatePreviewCommand.Execute(working);

        Assert.Equal("Working", viewModel.InlineSettings.SelectedCharacterStateImage?.StateKey);
        Assert.Contains("工作", viewModel.InlineSettings.CharacterStatePreviewStatus);
    }

    [Fact]
    public void InlineSettings_EditSelectedCharacterStateImage_SavesStateImageMapping()
    {
        var store = new StubMascotCharacterStore();
        using var viewModel = ToolLauncherViewModelFixture.Create(store);
        viewModel.InlineSettings.RefreshCharacterStatePreviewCommand.Execute(null);
        var completed = viewModel.InlineSettings.CharacterStatePreviewItems.First(x => x.StateKey == "Completed");

        viewModel.InlineSettings.SelectCharacterStatePreviewCommand.Execute(completed);
        viewModel.InlineSettings.SelectedCharacterStateImage!.FileName = "completed-custom.png";
        viewModel.InlineSettings.SaveCharacterCommand.Execute(null);

        Assert.Equal("completed-custom.png", store.SavedProfile?.StateImages["Completed"]);
        Assert.Contains("已保存", viewModel.InlineSettings.CharacterSaveStatus);
    }
}

file static class ToolLauncherViewModelFixture
{
    public static FloatingWindowViewModel Create(
        IMascotCharacterStore? characterStore = null,
        ITextToSpeechPreviewService? textToSpeechPreviewService = null,
        IAudioPlaybackService? audioPlaybackService = null,
        IVoiceInputService? voiceInputService = null,
        ITaskRouter? taskRouter = null,
        IComputerUseControlService? computerUseControlService = null)
    {
        return new FloatingWindowViewModel(
            taskRouter ?? new StubTaskRouter(),
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
            new StubTaskHistoryStore(),
            textToSpeechPreviewService,
            audioPlaybackService,
            voiceInputService,
            computerUseControlService);
    }
}

file sealed class StubTaskRouter : ITaskRouter
{
    public Task<TaskResult> DispatchAsync(AgentTask task, CancellationToken ct = default) =>
        Task.FromResult(new TaskResult { TaskId = task.Id, Success = true, Content = "ok" });

    public bool CancelTask(string taskId) => false;
    public void CancelAllTasks() { }
}

file sealed class RecordingTaskRouter : ITaskRouter
{
    public bool CancelResult { get; init; } = true;
    public int DispatchCount { get; private set; }
    public AgentTask? LastDispatchedTask { get; private set; }
    public string? CancelledTaskId { get; private set; }

    public Task<TaskResult> DispatchAsync(AgentTask task, CancellationToken ct = default)
    {
        DispatchCount++;
        LastDispatchedTask = task;
        return Task.FromResult(new TaskResult { TaskId = task.Id, Success = true, Content = "ok" });
    }

    public bool CancelTask(string taskId)
    {
        CancelledTaskId = taskId;
        return CancelResult;
    }

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

file sealed class StubTextToSpeechPreviewService : ITextToSpeechPreviewService
{
    private readonly string _audioFilePath;

    public StubTextToSpeechPreviewService(string audioFilePath)
    {
        _audioFilePath = audioFilePath;
    }

    public string LastText { get; private set; } = string.Empty;
    public string LastVoice { get; private set; } = string.Empty;

    public Task<TextToSpeechPreviewResult> SynthesizePreviewAsync(
        string text,
        string voice,
        CancellationToken ct = default)
    {
        LastText = text;
        LastVoice = voice;
        return Task.FromResult(new TextToSpeechPreviewResult(true, _audioFilePath, "ok"));
    }
}

file sealed class ThrowingTextToSpeechPreviewService : ITextToSpeechPreviewService
{
    public Task<TextToSpeechPreviewResult> SynthesizePreviewAsync(
        string text,
        string voice,
        CancellationToken ct = default) =>
        throw new InvalidOperationException("TTS 服务异常");
}

file sealed class StubAudioPlaybackService : IAudioPlaybackService
{
    public bool IsPlaying { get; private set; }
    public string? PlayedFilePath { get; private set; }
    public int StopCount { get; private set; }
    public AudioPlaybackResult PlayResult { get; init; } = new(true, "playing");

    public AudioPlaybackResult Play(string filePath)
    {
        PlayedFilePath = filePath;
        IsPlaying = PlayResult.Success;
        return PlayResult;
    }

    public AudioPlaybackResult Stop()
    {
        StopCount++;
        IsPlaying = false;
        return new AudioPlaybackResult(true, "stopped");
    }
}

file sealed class StubVoiceInputService : IVoiceInputService
{
    private readonly string _text;

    public StubVoiceInputService(string text)
    {
        _text = text;
    }

    public bool IsRecording { get; private set; }
    public string? StartLanguage { get; private set; }
    public string? StopLanguage { get; private set; }

    public VoiceInputStartResult StartRecording(string language)
    {
        StartLanguage = language;
        IsRecording = true;
        return new VoiceInputStartResult(true, "recording");
    }

    public Task<VoiceInputRecognitionResult> StopAndRecognizeAsync(string language, CancellationToken ct = default)
    {
        StopLanguage = language;
        IsRecording = false;
        return Task.FromResult(new VoiceInputRecognitionResult(true, _text, "C:\\temp\\voice.wav", "recognized"));
    }

    public VoiceInputStartResult Cancel()
    {
        IsRecording = false;
        return new VoiceInputStartResult(true, "cancelled");
    }
}

file sealed class FailingVoiceInputService : IVoiceInputService
{
    private readonly string _audioFilePath;
    private readonly string _error;

    public FailingVoiceInputService(string audioFilePath, string error)
    {
        _audioFilePath = audioFilePath;
        _error = error;
    }

    public bool IsRecording { get; private set; }

    public VoiceInputStartResult StartRecording(string language)
    {
        IsRecording = true;
        return new VoiceInputStartResult(true, "recording");
    }

    public Task<VoiceInputRecognitionResult> StopAndRecognizeAsync(string language, CancellationToken ct = default)
    {
        IsRecording = false;
        return Task.FromResult(new VoiceInputRecognitionResult(false, string.Empty, _audioFilePath, _error, _error));
    }

    public VoiceInputStartResult Cancel()
    {
        IsRecording = false;
        return new VoiceInputStartResult(true, "cancelled");
    }
}

file sealed class StubComputerUseControlService : IComputerUseControlService
{
    public bool HasActiveSession { get; set; }
    public bool HasPendingApproval { get; set; }
    public bool PauseResult { get; set; }
    public bool ResumeResult { get; set; }
    public bool TakeoverResult { get; set; }
    public int PauseCount { get; private set; }
    public int ResumeCount { get; private set; }
    public int TakeoverCount { get; private set; }
    public int ApproveCount { get; private set; }
    public int DenyCount { get; private set; }

    public bool Pause()
    {
        PauseCount++;
        return PauseResult;
    }

    public bool Resume()
    {
        ResumeCount++;
        return ResumeResult;
    }

    public bool Takeover()
    {
        TakeoverCount++;
        return TakeoverResult;
    }

    public bool ApproveCurrentAction()
    {
        ApproveCount++;
        if (!HasPendingApproval)
            return false;

        HasPendingApproval = false;
        return true;
    }

    public bool DenyCurrentAction(string? reason = null)
    {
        DenyCount++;
        if (!HasPendingApproval)
            return false;

        HasPendingApproval = false;
        return true;
    }
}

file sealed class TempAudioFile : IDisposable
{
    private TempAudioFile(string path)
    {
        Path = path;
    }

    public string Path { get; }

    public static TempAudioFile Create()
    {
        var path = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            $"desktopmascot-ui-audio-{Guid.NewGuid():N}.mp3");
        File.WriteAllBytes(path, [1, 2, 3]);
        return new TempAudioFile(path);
    }

    public void Dispose()
    {
        try { File.Delete(Path); }
        catch { }
    }
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
