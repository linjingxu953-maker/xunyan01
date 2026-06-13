namespace DesktopMascot.Core.Interfaces;

/// <summary>
/// 桌面体验接口 - 由 UI 层实现
/// </summary>
public interface IDesktopExperience
{
    /// <summary>显示悬浮窗</summary>
    void ShowFloatingWindow();

    /// <summary>隐藏悬浮窗</summary>
    void HideFloatingWindow();

    /// <summary>显示托盘菜单</summary>
    void ShowTrayMenu();

    /// <summary>隐藏托盘菜单</summary>
    void HideTrayMenu();

    /// <summary>显示任务详情面板</summary>
    void ShowTaskPanel(string taskId);

    /// <summary>隐藏任务详情面板</summary>
    void HideTaskPanel();

    /// <summary>显示设置页面</summary>
    void ShowSettings();

    /// <summary>显示记忆中心</summary>
    void ShowMemoryCenter();

    /// <summary>显示权限中心</summary>
    void ShowPermissionCenter();

    /// <summary>显示通知</summary>
    void ShowNotification(string title, string message, NotificationType type = NotificationType.Info);
}

/// <summary>
/// 通知类型
/// </summary>
public enum NotificationType
{
    Info,
    Success,
    Warning,
    Error
}
