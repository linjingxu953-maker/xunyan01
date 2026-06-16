using DesktopMascot.Agent.Models;

namespace DesktopMascot.Agent.Providers;

/// <summary>
/// 云存储提供商接口
/// </summary>
public interface ICloudStorageProvider
{
    /// <summary>提供商名称</summary>
    string Name { get; }
    
    /// <summary>提供商类型</summary>
    CloudProviderType Type { get; }
    
    /// <summary>配置连接</summary>
    Task<bool> ConfigureAsync(Dictionary<string, string> config, CancellationToken ct = default);
    
    /// <summary>测试连接</summary>
    Task<bool> TestConnectionAsync(CancellationToken ct = default);
    
    /// <summary>列出文件</summary>
    Task<List<CloudFileInfo>> ListFilesAsync(string path = "/", CancellationToken ct = default);
    
    /// <summary>上传文件</summary>
    Task<CloudFileInfo> UploadFileAsync(string localPath, string remotePath, CancellationToken ct = default);
    
    /// <summary>下载文件</summary>
    Task<string> DownloadFileAsync(string remotePath, string localPath, CancellationToken ct = default);
    
    /// <summary>删除文件</summary>
    Task<bool> DeleteFileAsync(string remotePath, CancellationToken ct = default);
    
    /// <summary>创建文件夹</summary>
    Task<bool> CreateFolderAsync(string path, CancellationToken ct = default);
    
    /// <summary>获取存储信息</summary>
    Task<SyncStatus> GetSyncStatusAsync(CancellationToken ct = default);
}
