namespace DesktopMascot.Core.Enums;

/// <summary>
/// 小人状态枚举
/// </summary>
public enum MascotState
{
    /// <summary>空闲</summary>
    Idle,
    /// <summary>正在听/接收任务</summary>
    Listening,
    /// <summary>理解意图</summary>
    Understanding,
    /// <summary>读取上下文</summary>
    ReadingContext,
    /// <summary>规划步骤</summary>
    Planning,
    /// <summary>等待确认</summary>
    WaitingApproval,
    /// <summary>执行任务</summary>
    Working,
    /// <summary>记忆确认</summary>
    MemoryConfirm,
    /// <summary>生成汇报</summary>
    Reporting,
    /// <summary>完成</summary>
    Completed,
    /// <summary>失败</summary>
    Error
}
