using DesktopMascot.Core.Enums;

namespace DesktopMascot.Core.Models;

/// <summary>
/// 任务事件
/// </summary>
public class TaskEvent
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string TaskId { get; set; } = string.Empty;
    public TaskEventType EventType { get; set; }
    public MascotState State { get; set; }
    public string Message { get; set; } = string.Empty;
    public int Progress { get; set; }
    public Dictionary<string, object>? Metadata { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 向后兼容：使用 Timestamp
    /// </summary>
    public DateTime CreatedAt
    {
        get => Timestamp;
        set => Timestamp = value;
    }

    /// <summary>
    /// 创建任务开始事件
    /// </summary>
    public static TaskEvent TaskStarted(string taskId, string message = "任务开始")
    {
        return new TaskEvent
        {
            TaskId = taskId,
            EventType = TaskEventType.TaskStarted,
            State = MascotState.Listening,
            Message = message,
            Progress = 0
        };
    }

    /// <summary>
    /// 创建任务完成事件
    /// </summary>
    public static TaskEvent TaskCompleted(string taskId, string message = "任务完成")
    {
        return new TaskEvent
        {
            TaskId = taskId,
            EventType = TaskEventType.TaskCompleted,
            State = MascotState.Completed,
            Message = message,
            Progress = 100
        };
    }

    /// <summary>
    /// 创建任务失败事件
    /// </summary>
    public static TaskEvent TaskFailed(string taskId, string error)
    {
        return new TaskEvent
        {
            TaskId = taskId,
            EventType = TaskEventType.TaskFailed,
            State = MascotState.Error,
            Message = error,
            Progress = 0
        };
    }

    /// <summary>
    /// 创建工具调用开始事件
    /// </summary>
    public static TaskEvent ToolCallStarted(string taskId, string toolName, string? input = null)
    {
        return new TaskEvent
        {
            TaskId = taskId,
            EventType = TaskEventType.ToolCallStarted,
            State = MascotState.Working,
            Message = $"正在执行工具: {toolName}",
            Metadata = new Dictionary<string, object>
            {
                ["toolName"] = toolName,
                ["input"] = input ?? ""
            }
        };
    }

    /// <summary>
    /// 创建工具调用完成事件
    /// </summary>
    public static TaskEvent ToolCallCompleted(string taskId, string toolName, bool success, string? output = null)
    {
        return new TaskEvent
        {
            TaskId = taskId,
            EventType = success ? TaskEventType.ToolCallCompleted : TaskEventType.ToolCallFailed,
            State = MascotState.Working,
            Message = success ? $"工具 {toolName} 执行完成" : $"工具 {toolName} 执行失败",
            Metadata = new Dictionary<string, object>
            {
                ["toolName"] = toolName,
                ["success"] = success,
                ["output"] = output ?? ""
            }
        };
    }

    /// <summary>
    /// 创建权限请求事件
    /// </summary>
    public static TaskEvent PermissionRequested(string taskId, string permissionType, string scope, string reason)
    {
        return new TaskEvent
        {
            TaskId = taskId,
            EventType = TaskEventType.PermissionRequested,
            State = MascotState.WaitingApproval,
            Message = $"需要权限确认: {permissionType}",
            Metadata = new Dictionary<string, object>
            {
                ["permissionType"] = permissionType,
                ["scope"] = scope,
                ["reason"] = reason
            }
        };
    }

    /// <summary>
    /// 创建记忆保存请求事件
    /// </summary>
    public static TaskEvent MemorySaveRequested(string taskId, string memoryType, string content)
    {
        return new TaskEvent
        {
            TaskId = taskId,
            EventType = TaskEventType.MemorySaveRequested,
            State = MascotState.MemoryConfirm,
            Message = "是否保存到长期记忆？",
            Metadata = new Dictionary<string, object>
            {
                ["memoryType"] = memoryType,
                ["content"] = content
            }
        };
    }

    /// <summary>
    /// 创建进度更新事件
    /// </summary>
    public static TaskEvent ProgressUpdated(string taskId, int progress, string message)
    {
        return new TaskEvent
        {
            TaskId = taskId,
            EventType = TaskEventType.ProgressUpdated,
            State = MascotState.Working,
            Message = message,
            Progress = progress
        };
    }
}
