using System.Text.Json;

namespace DesktopMascot.Core.Configuration;

/// <summary>
/// 配置管理器接口
/// </summary>
public interface IConfigurationManager
{
    /// <summary>获取应用设置</summary>
    Task<AppSettings> GetAppSettingsAsync(CancellationToken ct = default);
    
    /// <summary>保存应用设置</summary>
    Task SaveAppSettingsAsync(AppSettings settings, CancellationToken ct = default);
    
    /// <summary>获取用户偏好</summary>
    Task<UserPreferences> GetUserPreferencesAsync(CancellationToken ct = default);
    
    /// <summary>保存用户偏好</summary>
    Task SaveUserPreferencesAsync(UserPreferences preferences, CancellationToken ct = default);
    
    /// <summary>获取项目设置</summary>
    Task<ProjectSettings?> GetProjectSettingsAsync(string projectId, CancellationToken ct = default);
    
    /// <summary>保存项目设置</summary>
    Task SaveProjectSettingsAsync(ProjectSettings settings, CancellationToken ct = default);
    
    /// <summary>获取权限设置</summary>
    Task<PermissionSettings> GetPermissionSettingsAsync(CancellationToken ct = default);
    
    /// <summary>保存权限设置</summary>
    Task SavePermissionSettingsAsync(PermissionSettings settings, CancellationToken ct = default);
    
    /// <summary>重置为默认设置</summary>
    Task ResetToDefaultsAsync(CancellationToken ct = default);
}

/// <summary>
/// 文件配置管理器
/// </summary>
public class FileConfigurationManager : IConfigurationManager
{
    private readonly string _configDirectory;
    private readonly object _lock = new();
    
    private const string AppSettingsFile = "app_settings.json";
    private const string UserPreferencesFile = "user_preferences.json";
    private const string PermissionSettingsFile = "permission_settings.json";
    private const string ProjectsDirectory = "projects";

    public FileConfigurationManager(string? configDirectory = null)
    {
        _configDirectory = configDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "DesktopMascot",
            "config");
        Directory.CreateDirectory(_configDirectory);
        Directory.CreateDirectory(Path.Combine(_configDirectory, ProjectsDirectory));
    }

    private string GetFilePath(string fileName) => Path.Combine(_configDirectory, fileName);
    private string GetProjectFilePath(string projectId) => 
        Path.Combine(_configDirectory, ProjectsDirectory, $"{projectId}.json");

    private T? LoadJson<T>(string filePath) where T : class
    {
        if (!File.Exists(filePath))
            return null;

        lock (_lock)
        {
            var json = File.ReadAllText(filePath);
            return JsonSerializer.Deserialize<T>(json);
        }
    }

    private Task SaveJsonAsync<T>(string filePath, T data) where T : class
    {
        var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });

        lock (_lock)
        {
            File.WriteAllText(filePath, json);
        }

        return Task.CompletedTask;
    }

    public Task<AppSettings> GetAppSettingsAsync(CancellationToken ct = default)
    {
        var filePath = GetFilePath(AppSettingsFile);
        var settings = LoadJson<AppSettings>(filePath);
        return Task.FromResult(settings ?? new AppSettings());
    }

    public async Task SaveAppSettingsAsync(AppSettings settings, CancellationToken ct = default)
    {
        var filePath = GetFilePath(AppSettingsFile);
        await SaveJsonAsync(filePath, settings);
    }

    public Task<UserPreferences> GetUserPreferencesAsync(CancellationToken ct = default)
    {
        var filePath = GetFilePath(UserPreferencesFile);
        var preferences = LoadJson<UserPreferences>(filePath);
        return Task.FromResult(preferences ?? new UserPreferences());
    }

    public async Task SaveUserPreferencesAsync(UserPreferences preferences, CancellationToken ct = default)
    {
        var filePath = GetFilePath(UserPreferencesFile);
        await SaveJsonAsync(filePath, preferences);
    }

    public Task<ProjectSettings?> GetProjectSettingsAsync(string projectId, CancellationToken ct = default)
    {
        var filePath = GetProjectFilePath(projectId);
        return Task.FromResult(LoadJson<ProjectSettings>(filePath));
    }

    public async Task SaveProjectSettingsAsync(ProjectSettings settings, CancellationToken ct = default)
    {
        var filePath = GetProjectFilePath(settings.ProjectId);
        await SaveJsonAsync(filePath, settings);
    }

    public Task<PermissionSettings> GetPermissionSettingsAsync(CancellationToken ct = default)
    {
        var filePath = GetFilePath(PermissionSettingsFile);
        var settings = LoadJson<PermissionSettings>(filePath);
        return Task.FromResult(settings ?? new PermissionSettings());
    }

    public async Task SavePermissionSettingsAsync(PermissionSettings settings, CancellationToken ct = default)
    {
        var filePath = GetFilePath(PermissionSettingsFile);
        await SaveJsonAsync(filePath, settings);
    }

    public async Task ResetToDefaultsAsync(CancellationToken ct = default)
    {
        await SaveAppSettingsAsync(new AppSettings(), ct);
        await SaveUserPreferencesAsync(new UserPreferences(), ct);
        await SavePermissionSettingsAsync(new PermissionSettings(), ct);
    }
}
