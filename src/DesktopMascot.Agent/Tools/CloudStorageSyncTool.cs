using System.Text.Json;
using DesktopMascot.Agent.Models;
using DesktopMascot.Agent.Providers;

namespace DesktopMascot.Agent.Tools;

/// <summary>
/// 云存储同步工具 - Google Drive/OneDrive/本地同步
/// </summary>
public class CloudStorageSyncTool : ITool
{
    private readonly Dictionary<CloudProviderType, ICloudStorageProvider> _providers = new();
    private ICloudStorageProvider? _activeProvider;

    public string Name => "cloud_sync";
    public string Description => "云存储同步：Google Drive/OneDrive/本地文件夹同步。可选功能。";
    public string ParametersSchema => """
    {
        "type": "object",
        "properties": {
            "action": { "type": "string", "enum": ["configure", "list", "upload", "download", "delete", "sync", "status"], "description": "操作类型" },
            "provider": { "type": "string", "enum": ["google_drive", "onedrive", "local"], "description": "存储提供商" },
            "config": { "type": "object", "description": "配置信息" },
            "local_path": { "type": "string", "description": "本地文件路径" },
            "remote_path": { "type": "string", "description": "远程文件路径" },
            "folder": { "type": "string", "description": "文件夹路径" }
        },
        "required": ["action"]
    }
    """;

    public CloudStorageSyncTool()
    {
        _providers[CloudProviderType.LocalSync] = new LocalSyncProvider();
    }

    public async Task<ToolResult> ExecuteAsync(string arguments, CancellationToken ct = default)
    {
        try
        {
            var doc = JsonDocument.Parse(arguments);
            var root = doc.RootElement;
            var action = root.TryGetProperty("action", out var aEl) ? aEl.GetString() ?? "" : "";

            return action switch
            {
                "configure" => await ConfigureProviderAsync(root, ct),
                "list" => await ListFilesAsync(root, ct),
                "upload" => await UploadFileAsync(root, ct),
                "download" => await DownloadFileAsync(root, ct),
                "delete" => await DeleteFileAsync(root, ct),
                "sync" => await SyncFilesAsync(root, ct),
                "status" => await GetStatusAsync(ct),
                _ => Fail($"不支持的操作：{action}")
            };
        }
        catch (Exception ex)
        {
            return Fail($"云存储操作失败：{ex.Message}");
        }
    }

    private async Task<ToolResult> ConfigureProviderAsync(JsonElement root, CancellationToken ct)
    {
        var providerType = root.TryGetProperty("provider", out var pEl) ? pEl.GetString() ?? "" : "";
        var config = root.TryGetProperty("config", out var cEl)
            ? JsonSerializer.Deserialize<Dictionary<string, string>>(cEl.GetRawText()) ?? new()
            : new Dictionary<string, string>();

        if (string.IsNullOrEmpty(providerType))
            return Fail("缺少 provider 参数");

        var type = providerType.ToLower() switch
        {
            "google_drive" or "gdrive" => CloudProviderType.GoogleDrive,
            "onedrive" => CloudProviderType.OneDrive,
            "local" => CloudProviderType.LocalSync,
            _ => CloudProviderType.LocalSync
        };

        if (!_providers.ContainsKey(type))
        {
            _providers[type] = type switch
            {
                CloudProviderType.LocalSync => new LocalSyncProvider(),
                _ => throw new NotSupportedException($"暂不支持 {providerType}")
            };
        }

        var provider = _providers[type];
        var success = await provider.ConfigureAsync(config, ct);

        if (success)
        {
            _activeProvider = provider;
            return new ToolResult
            {
                Name = Name,
                Success = true,
                Content = $"已配置存储提供商：{provider.Name}"
            };
        }

        return Fail("配置失败");
    }

    private async Task<ToolResult> ListFilesAsync(JsonElement root, CancellationToken ct)
    {
        if (_activeProvider == null)
            return Fail("请先配置存储提供商");

        var path = root.TryGetProperty("folder", out var fEl) ? fEl.GetString() ?? "/" : "/";
        var files = await _activeProvider.ListFilesAsync(path, ct);

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"文件列表：{path}");
        sb.AppendLine();

        foreach (var file in files)
        {
            var icon = file.IsFolder ? "📁" : "📄";
            sb.AppendLine($"  {icon} {file.Name} ({file.Size / 1024.0:F1} KB)");
        }

        return new ToolResult { Name = Name, Success = true, Content = sb.ToString() };
    }

    private async Task<ToolResult> UploadFileAsync(JsonElement root, CancellationToken ct)
    {
        if (_activeProvider == null)
            return Fail("请先配置存储提供商");

        var localPath = root.TryGetProperty("local_path", out var lEl) ? lEl.GetString() ?? "" : "";
        var remotePath = root.TryGetProperty("remote_path", out var rEl) ? rEl.GetString() ?? "" : "";

        if (string.IsNullOrEmpty(localPath)) return Fail("缺少 local_path 参数");
        if (string.IsNullOrEmpty(remotePath)) return Fail("缺少 remote_path 参数");
        if (!File.Exists(localPath)) return Fail($"文件不存在：{localPath}");

        var result = await _activeProvider.UploadFileAsync(localPath, remotePath, ct);

        return new ToolResult
        {
            Name = Name,
            Success = true,
            Content = $"已上传文件\n本地：{localPath}\n远程：{result.Path}\n大小：{result.Size / 1024.0:F1} KB"
        };
    }

    private async Task<ToolResult> DownloadFileAsync(JsonElement root, CancellationToken ct)
    {
        if (_activeProvider == null)
            return Fail("请先配置存储提供商");

        var remotePath = root.TryGetProperty("remote_path", out var rEl) ? rEl.GetString() ?? "" : "";
        var localPath = root.TryGetProperty("local_path", out var lEl) ? lEl.GetString() ?? "" : "";

        if (string.IsNullOrEmpty(remotePath)) return Fail("缺少 remote_path 参数");
        if (string.IsNullOrEmpty(localPath)) return Fail("缺少 local_path 参数");

        var result = await _activeProvider.DownloadFileAsync(remotePath, localPath, ct);

        return new ToolResult
        {
            Name = Name,
            Success = true,
            Content = $"已下载文件\n远程：{remotePath}\n本地：{result}"
        };
    }

    private async Task<ToolResult> DeleteFileAsync(JsonElement root, CancellationToken ct)
    {
        if (_activeProvider == null)
            return Fail("请先配置存储提供商");

        var remotePath = root.TryGetProperty("remote_path", out var rEl) ? rEl.GetString() ?? "" : "";
        if (string.IsNullOrEmpty(remotePath)) return Fail("缺少 remote_path 参数");

        var success = await _activeProvider.DeleteFileAsync(remotePath, ct);

        return new ToolResult
        {
            Name = Name,
            Success = success,
            Content = success ? $"已删除文件：{remotePath}" : $"删除失败：{remotePath}"
        };
    }

    private async Task<ToolResult> SyncFilesAsync(JsonElement root, CancellationToken ct)
    {
        if (_activeProvider == null)
            return Fail("请先配置存储提供商");

        var status = await _activeProvider.GetSyncStatusAsync(ct);

        return new ToolResult
        {
            Name = Name,
            Success = true,
            Content = $"同步状态\n提供商：{status.Provider}\n已同步：{status.FilesSynced} 文件\n待同步：{status.FilesPending} 文件\n上次同步：{status.LastSyncTime:yyyy-MM-dd HH:mm}"
        };
    }

    private async Task<ToolResult> GetStatusAsync(CancellationToken ct)
    {
        if (_activeProvider == null)
            return Fail("请先配置存储提供商");

        var status = await _activeProvider.GetSyncStatusAsync(ct);

        return new ToolResult
        {
            Name = Name,
            Success = true,
            Content = $"存储状态\n提供商：{status.Provider}\n连接：{(status.IsConnected ? "已连接" : "未连接")}\n文件数：{status.FilesSynced}\n待同步：{status.FilesPending}"
        };
    }

    private static ToolResult Fail(string error) => new() { Name = "cloud_sync", Success = false, Error = error };
}
