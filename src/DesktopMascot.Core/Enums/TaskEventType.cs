namespace DesktopMascot.Core.Enums;

/// <summary>
/// 任务事件类型
/// </summary>
public enum TaskEventType
{
    // 任务生命周期
    /// <summary>任务开始</summary>
    TaskStarted,
    /// <summary>任务完成</summary>
    TaskCompleted,
    /// <summary>任务失败</summary>
    TaskFailed,
    /// <summary>任务取消</summary>
    TaskCancelled,

    // 步骤变化
    /// <summary>步骤变化</summary>
    StepChanged,
    /// <summary>步骤完成</summary>
    StepCompleted,

    // 工具调用
    /// <summary>工具调用开始</summary>
    ToolCallStarted,
    /// <summary>工具调用完成</summary>
    ToolCallCompleted,
    /// <summary>工具调用失败</summary>
    ToolCallFailed,

    // 权限请求
    /// <summary>权限请求</summary>
    PermissionRequested,
    /// <summary>权限已授予</summary>
    PermissionGranted,
    /// <summary>权限被拒绝</summary>
    PermissionDenied,

    // 记忆请求
    /// <summary>记忆保存请求</summary>
    MemorySaveRequested,
    /// <summary>记忆保存完成</summary>
    MemorySaveCompleted,
    /// <summary>记忆保存被拒绝</summary>
    MemorySaveRejected,

    // 进度更新
    /// <summary>进度更新</summary>
    ProgressUpdated,

    // 上下文读取
    /// <summary>上下文读取开始</summary>
    ContextReadingStarted,
    /// <summary>上下文读取完成</summary>
    ContextReadingCompleted,

    // LLM 调用
    /// <summary>LLM 调用开始</summary>
    LlmCallStarted,
    /// <summary>LLM 调用完成</summary>
    LlmCallCompleted,
    /// <summary>LLM 流式块</summary>
    LlmStreamChunk
}
