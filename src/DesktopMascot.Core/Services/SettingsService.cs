using DesktopMascot.Core.Configuration;

namespace DesktopMascot.Core.Services;

/// <summary>
/// 设置服务接口 - 供 UI 层调用
/// </summary>
public interface ISettingsService
{
    /// <summary>获取应用设置</summary>
    Task<AppSettings> GetSettingsAsync(CancellationToken ct = default);

    /// <summary>更新应用设置</summary>
    Task UpdateSettingsAsync(AppSettings settings, CancellationToken ct = default);

    /// <summary>获取 API Key</summary>
    Task<string?> GetApiKeyAsync(string provider, CancellationToken ct = default);

    /// <summary>设置 API Key</summary>
    Task SetApiKeyAsync(string provider, string apiKey, CancellationToken ct = default);

    /// <summary>删除 API Key</summary>
    Task RemoveApiKeyAsync(string provider, CancellationToken ct = default);

    /// <summary>获取所有已配置的 Provider</summary>
    Task<List<string>> GetConfiguredProvidersAsync(CancellationToken ct = default);

    /// <summary>测试 API Key 连接</summary>
    Task<ApiTestResult> TestApiKeyAsync(string provider, string apiKey, CancellationToken ct = default);
}

/// <summary>
/// API 测试结果
/// </summary>
public class ApiTestResult
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public TimeSpan? Latency { get; set; }
    public string? ModelInfo { get; set; }
}
