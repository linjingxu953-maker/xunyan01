namespace DesktopMascot.Core.Models;

/// <summary>
/// 任务执行结果
/// </summary>
public class TaskResult
{
    public string TaskId { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string Content { get; set; } = string.Empty;
    public string? Error { get; set; }
    public DateTime CompletedAt { get; set; } = DateTime.UtcNow;

    public static TaskResult Denied(string taskId) => new()
    {
        TaskId = taskId,
        Success = false,
        Content = "用户拒绝了操作"
    };

    public static TaskResult Failed(string taskId, string error) => new()
    {
        TaskId = taskId,
        Success = false,
        Error = error
    };
}
