using DesktopMascot.Core.Enums;
using CommunityToolkit.Mvvm.ComponentModel;

namespace DesktopMascot.UI.ViewModels;

/// <summary>
/// UI 层任务步骤展示项。
/// </summary>
public sealed partial class TaskTimelineItem : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotCurrent))]
    private bool _isCurrent;

    public TaskTimelineItem(MascotState state, string message, int progress, DateTime createdAt, bool isCurrent)
    {
        StateText = GetStateText(state);
        Message = string.IsNullOrWhiteSpace(message) ? GetFallbackMessage(state) : message;
        ProgressText = progress >= 0 ? $"{progress}%" : "进行中";
        TimeText = createdAt.ToLocalTime().ToString("HH:mm:ss");
        MarkerText = state switch
        {
            MascotState.Completed => "完成",
            MascotState.Error => "异常",
            MascotState.WaitingApproval => "确认",
            _ => "步骤"
        };
        IsCurrent = isCurrent;
    }

    public string StateText { get; }
    public string Message { get; }
    public string ProgressText { get; }
    public string TimeText { get; }
    public string MarkerText { get; }
    public bool IsNotCurrent => !IsCurrent;

    private static string GetFallbackMessage(MascotState state) => state switch
    {
        MascotState.Idle => "空闲",
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

    private static string GetStateText(MascotState state) => state switch
    {
        MascotState.Idle => "空闲",
        MascotState.Listening => "聆听",
        MascotState.Understanding => "理解",
        MascotState.ReadingContext => "读取上下文",
        MascotState.Planning => "规划",
        MascotState.WaitingApproval => "等待确认",
        MascotState.Working => "执行",
        MascotState.MemoryConfirm => "记忆确认",
        MascotState.Reporting => "汇报",
        MascotState.Completed => "完成",
        MascotState.Error => "错误",
        _ => "未知"
    };
}
