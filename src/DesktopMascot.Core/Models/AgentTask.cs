using DesktopMascot.Core.Enums;

namespace DesktopMascot.Core.Models;

/// <summary>
/// Agent 任务模型
/// </summary>
public class AgentTask
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Title { get; set; } = string.Empty;
    public string Input { get; set; } = string.Empty;
    public TaskType Type { get; set; }
    public AppTaskStatus Status { get; set; } = AppTaskStatus.Created;
    public PermissionLevel RequiredPermission { get; set; } = PermissionLevel.L0_Chat;
    public Dictionary<string, object> Parameters { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
}
