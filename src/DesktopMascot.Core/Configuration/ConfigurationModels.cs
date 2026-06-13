namespace DesktopMascot.Core.Configuration;

/// <summary>
/// 应用设置
/// </summary>
public class AppSettings
{
    /// <summary>模型服务商</summary>
    public string ProviderName { get; set; } = "OpenAI";

    /// <summary>API 密钥</summary>
    public string ApiKey { get; set; } = string.Empty;
    
    /// <summary>模型名称</summary>
    public string ModelName { get; set; } = "gpt-4";
    
    /// <summary>API 端点</summary>
    public string ApiEndpoint { get; set; } = "https://api.openai.com/v1";
    
    /// <summary>数据目录</summary>
    public string DataDirectory { get; set; } = string.Empty;
    
    /// <summary>快捷键</summary>
    public string Hotkey { get; set; } = "Ctrl+Shift+Space";
    
    /// <summary>开机自启</summary>
    public bool AutoStart { get; set; } = false;
    
    /// <summary>记忆功能启用</summary>
    public bool MemoryEnabled { get; set; } = true;

    /// <summary>Mimo Code 接入启用</summary>
    public bool MimoCodeEnabled { get; set; } = false;

    /// <summary>Mimo Code 可执行文件路径</summary>
    public string MimoCodeExecutablePath { get; set; } = "mimo";

    /// <summary>Mimo Code 工作目录</summary>
    public string MimoCodeWorkspaceDirectory { get; set; } = string.Empty;

    /// <summary>Mimo Code 模型配置来源：AppProvider 或 MimoLocalConfig</summary>
    public string MimoCodeModelConfigMode { get; set; } = "AppProvider";
    
    /// <summary>日志级别</summary>
    public string LogLevel { get; set; } = "Information";
    
    /// <summary>语言</summary>
    public string Language { get; set; } = "zh-CN";
    
    /// <summary>主题</summary>
    public string Theme { get; set; } = "system";

    /// <summary>首次启动向导是否已完成</summary>
    public bool OnboardingCompleted { get; set; } = false;
    
    /// <summary>窗口位置 X</summary>
    public int WindowX { get; set; } = 100;
    
    /// <summary>窗口位置 Y</summary>
    public int WindowY { get; set; } = 100;
    
    /// <summary>窗口缩放</summary>
    public double WindowScale { get; set; } = 1.0;
}

/// <summary>
/// 用户偏好
/// </summary>
public class UserPreferences
{
    /// <summary>用户名称</summary>
    public string UserName { get; set; } = string.Empty;
    
    /// <summary>工作目录</summary>
    public string WorkingDirectory { get; set; } = string.Empty;
    
    /// <summary>常用工具</summary>
    public List<string> FavoriteTools { get; set; } = new();
    
    /// <summary>通知设置</summary>
    public bool NotificationsEnabled { get; set; } = true;
    
    /// <summary>自动保存间隔（秒）</summary>
    public int AutoSaveInterval { get; set; } = 300;
}

/// <summary>
/// 项目设置
/// </summary>
public class ProjectSettings
{
    /// <summary>项目 ID</summary>
    public string ProjectId { get; set; } = string.Empty;
    
    /// <summary>项目名称</summary>
    public string ProjectName { get; set; } = string.Empty;
    
    /// <summary>技术栈</summary>
    public List<string> TechStack { get; set; } = new();
    
    /// <summary>项目目录</summary>
    public string ProjectDirectory { get; set; } = string.Empty;
    
    /// <summary>授权目录</summary>
    public List<string> AuthorizedDirectories { get; set; } = new();
}

/// <summary>
/// 权限设置
/// </summary>
public class PermissionSettings
{
    /// <summary>自动批准级别（0-6）</summary>
    public int AutoApproveLevel { get; set; } = 0;
    
    /// <summary>永久授权列表</summary>
    public Dictionary<string, int> PermanentPermissions { get; set; } = new();
    
    /// <summary>黑名单命令</summary>
    public List<string> BlockedCommands { get; set; } = new();
    
    /// <summary>审计日志保留天数</summary>
    public int AuditLogRetentionDays { get; set; } = 30;
}
