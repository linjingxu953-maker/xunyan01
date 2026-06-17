using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using DesktopMascot.Core.Enums;
using DesktopMascot.Core.Interfaces;
using DesktopMascot.Core.Memory;
using DesktopMascot.Core.Models;
using DesktopMascot.Core.Security;
using DesktopMascot.Core.Storage;
using DesktopMascot.UI.Services;

namespace DesktopMascot.UI.ViewModels;

/// <summary>FloatingWindowViewModel — 可观察属性和计算属性</summary>
public partial class FloatingWindowViewModel
{
    // ── 状态 ──
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsMascotBusy))]
    [NotifyPropertyChangedFor(nameof(IsMascotWaiting))]
    [NotifyPropertyChangedFor(nameof(IsMascotError))]
    [NotifyPropertyChangedFor(nameof(IsMascotCompleted))]
    private MascotState _currentState = MascotState.Idle;
    [ObservableProperty] private string _currentStateText = "空闲";
    [ObservableProperty] private string _statusMessage = "点击小人开始";
    [ObservableProperty] private string _stateHint = "待命";
    [ObservableProperty] private string _mascotMoodText = "Ready";
    [ObservableProperty] private int _currentProgress;
    [ObservableProperty] private bool _isTaskActive;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanUseTaskResultActions))]
    [NotifyPropertyChangedFor(nameof(CanRetryCurrentTask))]
    [NotifyPropertyChangedFor(nameof(IsMascotBusy))]
    private bool _isBusy;
    [ObservableProperty] private bool _hasTaskDetails;
    [ObservableProperty] private bool _canCancelTask;
    [ObservableProperty] private string _activeTaskId = string.Empty;
    [ObservableProperty] private string _activeTaskTitle = "当前任务";
    [ObservableProperty] private string _activeTaskTypeText = "普通问答";
    [ObservableProperty] private string _taskSummary = "暂无任务";
    [ObservableProperty] private string _activeStepText = "等待任务";
    [ObservableProperty] private string _taskProgressText = "0%";

    // ── 任务结果 ──
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNoTaskResult))]
    [NotifyPropertyChangedFor(nameof(CanUseTaskResultActions))]
    private bool _hasTaskResult;
    [ObservableProperty] private string _taskResultPreview = "任务完成后会在这里显示结果。";
    [ObservableProperty] private string _taskResultStatusText = "暂无结果";
    [ObservableProperty] private string _taskActionStatus = "等待任务执行。";

    // ── Computer Use 面板 ──
    [ObservableProperty] private bool _isComputerUsePanelVisible;
    [ObservableProperty] private string _computerUseModeText = "未接入";
    [ObservableProperty] private string _computerUseStatusText = "等待 Computer Use 事件";
    [ObservableProperty] private string _computerUseTargetText = "暂无目标";
    [ObservableProperty] private string _computerUseControlStatus = "等待 MiMo Computer Use 接入事件流。";
    [ObservableProperty] private string _computerUseScreenshotStatus = "等待屏幕观察截图。";
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasComputerUseScreenshot))]
    [NotifyPropertyChangedFor(nameof(HasNoComputerUseScreenshot))]
    private IImage? _computerUseScreenshotImage;

    // ── 重试 / 确认 ──
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanRetryCurrentTask))]
    private bool _canRetryTask;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanRetryCurrentTask))]
    private bool _isWaitingForUserConfirmation;
    [ObservableProperty] private string _pendingConfirmationTitle = "等待确认";
    [ObservableProperty] private string _pendingConfirmationDescription = "请确认后继续执行。";
    [ObservableProperty] private string _pendingConfirmationRiskText = "低风险";
    [ObservableProperty] private IBrush _stateAccentBrush = BrushFrom("#2563EB");
    [ObservableProperty] private IBrush _mascotBackgroundBrush = BrushFrom("#EEF6FF");

    // ── 聊天 / 面板可见性 ──
    [ObservableProperty] private bool _isChatVisible;
    [ObservableProperty] private bool _isCharacterPanelVisible;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSendMessage))]
    private string _inputText = string.Empty;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(VoiceInputButtonText))]
    private bool _isVoiceRecording;
    [ObservableProperty] private string _voiceInputStatus = "语音输入待命";
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(VoiceReplyButtonText))]
    [NotifyPropertyChangedFor(nameof(CanStopVoiceReply))]
    private bool _isVoiceReplyPlaying;
    [ObservableProperty] private string _voiceReplyStatus = "语音回复待命";
    [ObservableProperty] private bool _isMainAreaHitTestVisible = true;
    [ObservableProperty] private bool _isChatPanelHitTestVisible = true;
    [ObservableProperty] private bool _isMascotIconVisible = true;
    [ObservableProperty] private bool _isChatDialogVisible;
    [ObservableProperty] private bool _isChatDialogHitTestVisible = true;
    [ObservableProperty] private bool _isSidebarVisible = true;
    [ObservableProperty] private bool _isChatPageVisible = true;
    [ObservableProperty] private bool _isSettingsPageVisible;
    [ObservableProperty] private string _inlineSettingsTitle = "设置";
    [ObservableProperty] private string _inlineSettingsDescription = "模型、权限、记忆、快捷键、数据目录和角色外观都在这里管理。";
    [ObservableProperty] private string _inlineSettingsStatus = "选择左侧设置项查看当前配置入口。";

    // ── 角色 ──
    [ObservableProperty] private string _characterName = "妍";
    [ObservableProperty] private string _characterRole = "寻研桌面助手";
    [ObservableProperty] private string _characterAvatarText = "妍";
    [ObservableProperty] private string _characterDescription = "主动理解屏幕与任务上下文，清晰地给出下一步。";
    [ObservableProperty] private string _characterPersonality = "沉稳可靠";
    [ObservableProperty] private string _characterToneStyle = "友善";
    [ObservableProperty] private string _characterLanguageStyle = "标准";
    [ObservableProperty] private string _characterReplyLength = "平衡";
    [ObservableProperty] private bool _characterUseEmoji;
    [ObservableProperty] private string _characterSystemPromptSuffix = string.Empty;
    [ObservableProperty] private string _characterCatchphrase = "我在桌面待命，随时可以接任务。";
    [ObservableProperty] private string _characterAccentColor = "#2563EB";
    [ObservableProperty] private string _characterBackgroundColor = "#EEF6FF";
    [ObservableProperty] private string _characterImageFolder = "assets/characters/default";
    [ObservableProperty] private string _characterAvatarImage = "avatar.png";
    [ObservableProperty] private string _characterImageStatus = "未找到角色图片时会使用文字头像。";
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNoCharacterImage))]
    private bool _hasCharacterImage;
    [ObservableProperty] private IImage? _characterImageSource;
    [ObservableProperty] private string _characterSaveStatus = "角色配置会自动保存在本机。";
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNoToolCallRecords))]
    private bool _hasToolCallRecords;

    // ── 集合 ──
    public ObservableCollection<string> Messages { get; } = new();
    public ObservableCollection<MessageItem> MessageItems { get; } = new();
    public ObservableCollection<TaskHistoryItem> TaskHistory { get; } = new();
    public ObservableCollection<TaskTimelineItem> TaskTimeline { get; } = new();
    public ObservableCollection<TaskToolCallItem> TaskToolCalls { get; } = new();
    public ObservableCollection<ComputerUseActionItem> ComputerUseActions { get; } = new();
    public ObservableCollection<ComputerUseLogItem> ComputerUseLogItems { get; } = new();
    public SettingsWindowViewModel InlineSettings { get; }

    // ── 计算属性 ──
    public bool CanSendMessage => !IsBusy && !IsWaitingForUserConfirmation && !string.IsNullOrWhiteSpace(InputText);
    public bool CanStartScreenSelection => !IsBusy && !IsWaitingForUserConfirmation;
    public bool HasNoCharacterImage => !HasCharacterImage;
    public bool HasNoTaskResult => !HasTaskResult;
    public bool HasNoToolCallRecords => !HasToolCallRecords;
    public bool HasComputerUseActions => ComputerUseActions.Count > 0;
    public bool HasNoComputerUseActions => !HasComputerUseActions;
    public bool HasComputerUseLogItems => ComputerUseLogItems.Count > 0;
    public bool HasNoComputerUseLogItems => !HasComputerUseLogItems;
    public bool HasComputerUseScreenshot => ComputerUseScreenshotImage is not null;
    public bool HasNoComputerUseScreenshot => !HasComputerUseScreenshot;
    public bool HasMessages => MessageItems.Count > 0;
    public bool HasNoMessages => !HasMessages;
    public bool HasTaskHistory => TaskHistory.Count > 0;
    public bool HasNoTaskHistory => !HasTaskHistory;
    public bool CanUseTaskResultActions => HasTaskResult && !IsBusy;
    public string VoiceInputButtonText => IsVoiceRecording ? "结束录音" : "麦克风";
    public string VoiceReplyButtonText => IsVoiceReplyPlaying ? "停止朗读" : "朗读待命";
    public bool CanStopVoiceReply => IsVoiceReplyPlaying;
    public bool CanRetryCurrentTask => CanRetryTask && !IsBusy && !IsWaitingForUserConfirmation && !string.IsNullOrWhiteSpace(_lastUserMessage);
    public bool CanResolvePendingConfirmation => IsWaitingForUserConfirmation && _pendingConfirmationTask is not null && !IsBusy;
    public bool IsMascotBusy => IsBusy || CurrentState is MascotState.Understanding or MascotState.ReadingContext or MascotState.Planning or MascotState.Working or MascotState.Reporting;
    public bool IsMascotWaiting => CurrentState is MascotState.WaitingApproval or MascotState.MemoryConfirm;
    public bool IsMascotError => CurrentState == MascotState.Error;
    public bool IsMascotCompleted => CurrentState == MascotState.Completed;

    // ── 事件 ──
    public event EventHandler? HideRequested;
    public event EventHandler? ExitRequested;
    public event EventHandler? ScreenSelectionRequested;

    // ── 字段 ──
    private readonly ITaskRouter _taskRouter;
    private readonly ITaskEventBus _eventBus;
    private readonly ITaskEventStream _eventStream;
    private readonly ITaskHistoryStore _taskHistoryStore;
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
    private string _currentConversationId = Guid.NewGuid().ToString("N");
    private DateTime _currentConversationCreatedAt = DateTime.UtcNow;
    private Dictionary<string, string> _characterStateImages = new();
    private List<string> _characterPersonalityTraits = ["可靠", "主动"];
    private bool _isApplyingCharacterProfile;
    private bool _hasLoadedInlineSettings;
    private Window? _inlineSettingsOwner;
}
