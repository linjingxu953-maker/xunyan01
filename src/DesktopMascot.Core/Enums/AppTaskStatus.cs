namespace DesktopMascot.Core.Enums;

/// <summary>
/// 任务状态
/// </summary>
public enum AppTaskStatus
{
    /// <summary>已创建</summary>
    Created,
    /// <summary>进行中</summary>
    Running,
    /// <summary>已完成</summary>
    Completed,
    /// <summary>失败</summary>
    Failed,
    /// <summary>已取消</summary>
    Cancelled
}
