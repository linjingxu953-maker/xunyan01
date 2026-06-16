using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DesktopMascot.Agent.Models;

namespace DesktopMascot.Agent.Providers;

/// <summary>
/// 本地同步提供商 - 无需服务器，文件同步到本地文件夹
/// </summary>
public class LocalSyncProvider : ICloudStorageProvider
{
    private string _syncDirectory = "";
    private string _metadataFile = "";

    public string Name => "本地同步";
    public CloudProviderType Type => CloudProviderType.LocalSync;

    public Task<bool> ConfigureAsync(Dictionary<string, string> config, CancellationToken ct = default)
    {
        _syncDirectory = config.GetValueOrDefault("sync_directory", 
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), 
            "DesktopMascot", "sync"));
        _metadataFile = Path.Combine(_syncDirectory, ".sync_metadata.json");

        Directory.CreateDirectory(_syncDirectory);
        return Task.FromResult(true);
    }

    public Task<bool> TestConnectionAsync(CancellationToken ct = default)
    {
        return Task.FromResult(Directory.Exists(_syncDirectory));
    }

    public Task<List<CloudFileInfo>> ListFilesAsync(string path = "/", CancellationToken ct = default)
    {
        var fullPath = Path.Combine(_syncDirectory, path.TrimStart('/'));
        var result = new List<CloudFileInfo>();

        if (!Directory.Exists(fullPath))
            return Task.FromResult(result);

        foreach (var file in Directory.GetFiles(fullPath))
        {
            var info = new FileInfo(file);
            result.Add(new CloudFileInfo
            {
                Id = ComputeHash(file),
                Name = info.Name,
                Path = file,
                Size = info.Length,
                ModifiedAt = info.LastWriteTime,
                IsFolder = false
            });
        }

        foreach (var dir in Directory.GetDirectories(fullPath))
        {
            var info = new DirectoryInfo(dir);
            result.Add(new CloudFileInfo
            {
                Id = ComputeHash(dir),
                Name = info.Name,
                Path = dir,
                ModifiedAt = info.LastWriteTime,
                IsFolder = true
            });
        }

        return Task.FromResult(result);
    }

    public async Task<CloudFileInfo> UploadFileAsync(string localPath, string remotePath, CancellationToken ct = default)
    {
        var targetPath = Path.Combine(_syncDirectory, remotePath.TrimStart('/'));
        var targetDir = Path.GetDirectoryName(targetPath);
        
        if (!string.IsNullOrEmpty(targetDir))
            Directory.CreateDirectory(targetDir);

        File.Copy(localPath, targetPath, true);

        var info = new FileInfo(targetPath);
        return new CloudFileInfo
        {
            Id = ComputeHash(targetPath),
            Name = info.Name,
            Path = targetPath,
            Size = info.Length,
            ModifiedAt = info.LastWriteTime
        };
    }

    public async Task<string> DownloadFileAsync(string remotePath, string localPath, CancellationToken ct = default)
    {
        var sourcePath = Path.Combine(_syncDirectory, remotePath.TrimStart('/'));
        
        if (!File.Exists(sourcePath))
            throw new FileNotFoundException($"文件不存在: {remotePath}");

        var targetDir = Path.GetDirectoryName(localPath);
        if (!string.IsNullOrEmpty(targetDir))
            Directory.CreateDirectory(targetDir);

        File.Copy(sourcePath, localPath, true);
        return localPath;
    }

    public Task<bool> DeleteFileAsync(string remotePath, CancellationToken ct = default)
    {
        var fullPath = Path.Combine(_syncDirectory, remotePath.TrimStart('/'));
        
        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
            return Task.FromResult(true);
        }
        
        if (Directory.Exists(fullPath))
        {
            Directory.Delete(fullPath, true);
            return Task.FromResult(true);
        }

        return Task.FromResult(false);
    }

    public Task<bool> CreateFolderAsync(string path, CancellationToken ct = default)
    {
        var fullPath = Path.Combine(_syncDirectory, path.TrimStart('/'));
        Directory.CreateDirectory(fullPath);
        return Task.FromResult(true);
    }

    public Task<SyncStatus> GetSyncStatusAsync(CancellationToken ct = default)
    {
        var fileCount = Directory.Exists(_syncDirectory) 
            ? Directory.GetFiles(_syncDirectory, "*.*", SearchOption.AllDirectories).Length 
            : 0;

        return Task.FromResult(new SyncStatus
        {
            Provider = "本地同步",
            IsConnected = Directory.Exists(_syncDirectory),
            FilesSynced = fileCount,
            FilesPending = 0,
            LastSyncTime = DateTime.UtcNow
        });
    }

    private static string ComputeHash(string path)
    {
        using var sha256 = SHA256.Create();
        var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(path));
        return Convert.ToHexString(bytes)[..16];
    }
}
