namespace DesktopMascot.Agent.Models;

/// <summary>
/// 云存储文件信息
/// </summary>
public class CloudFileInfo
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public long Size { get; set; }
    public DateTime ModifiedAt { get; set; }
    public string MimeType { get; set; } = string.Empty;
    public bool IsFolder { get; set; }
}

/// <summary>
/// 云存储同步状态
/// </summary>
public class SyncStatus
{
    public string Provider { get; set; } = string.Empty;
    public bool IsConnected { get; set; }
    public int FilesSynced { get; set; }
    public int FilesPending { get; set; }
    public DateTime LastSyncTime { get; set; }
    public string? LastError { get; set; }
}

/// <summary>
/// 云存储提供商类型
/// </summary>
public enum CloudProviderType
{
    GoogleDrive,
    OneDrive,
    LocalSync
}
