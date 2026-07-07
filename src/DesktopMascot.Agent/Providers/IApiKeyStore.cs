using System.Text.Json;

namespace DesktopMascot.Agent.Providers;

/// <summary>
/// API Key 存储接口
/// </summary>
public interface IApiKeyStore
{
    /// <summary>获取 API Key</summary>
    Task<string?> GetApiKeyAsync(string provider, CancellationToken ct = default);

    /// <summary>设置 API Key</summary>
    Task SetApiKeyAsync(string provider, string apiKey, CancellationToken ct = default);

    /// <summary>删除 API Key</summary>
    Task RemoveApiKeyAsync(string provider, CancellationToken ct = default);

    /// <summary>检查是否有 API Key</summary>
    Task<bool> HasApiKeyAsync(string provider, CancellationToken ct = default);

    /// <summary>获取所有已配置的 Provider</summary>
    Task<List<string>> GetConfiguredProvidersAsync(CancellationToken ct = default);
}

/// <summary>
/// 文件 API Key 存储（使用 DPAPI 加密）
/// </summary>
public class FileApiKeyStore : IApiKeyStore
{
    private readonly string _storageDirectory;
    private readonly object _lock = new();
    private const string FileName = "api_keys.json";

    public FileApiKeyStore(string? storageDirectory = null)
    {
        _storageDirectory = storageDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "DesktopMascot",
            "config");
        Directory.CreateDirectory(_storageDirectory);
    }

    private string GetFilePath() => Path.Combine(_storageDirectory, FileName);

    private Dictionary<string, string> LoadKeys()
    {
        var filePath = GetFilePath();
        if (!File.Exists(filePath))
            return new Dictionary<string, string>();

        lock (_lock)
        {
            var json = File.ReadAllText(filePath);
            return JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new Dictionary<string, string>();
        }
    }

    private Task SaveKeysAsync(Dictionary<string, string> keys)
    {
        var filePath = GetFilePath();
        var json = JsonSerializer.Serialize(keys, new JsonSerializerOptions { WriteIndented = true });

        lock (_lock)
        {
            File.WriteAllText(filePath, json);
        }

        return Task.CompletedTask;
    }

    public Task<string?> GetApiKeyAsync(string provider, CancellationToken ct = default)
    {
        var keys = LoadKeys();
        var originalProvider = provider;
        provider = NormalizeProviderName(provider);
        if (keys.TryGetValue(provider, out var encryptedKey))
        {
            return Task.FromResult<string?>(CredentialProtector.Unprotect(encryptedKey));
        }
        if (!string.Equals(originalProvider, provider, StringComparison.OrdinalIgnoreCase)
            && keys.TryGetValue(originalProvider, out encryptedKey))
        {
            return Task.FromResult<string?>(CredentialProtector.Unprotect(encryptedKey));
        }
        return Task.FromResult<string?>(null);
    }

    public async Task SetApiKeyAsync(string provider, string apiKey, CancellationToken ct = default)
    {
        var keys = LoadKeys();
        provider = NormalizeProviderName(provider);
        keys[provider] = CredentialProtector.Protect(apiKey);
        await SaveKeysAsync(keys);
    }

    public async Task RemoveApiKeyAsync(string provider, CancellationToken ct = default)
    {
        var keys = LoadKeys();
        provider = NormalizeProviderName(provider);
        if (keys.Remove(provider))
        {
            await SaveKeysAsync(keys);
        }
    }

    public Task<bool> HasApiKeyAsync(string provider, CancellationToken ct = default)
    {
        var keys = LoadKeys();
        provider = NormalizeProviderName(provider);
        return Task.FromResult(keys.ContainsKey(provider));
    }

    public Task<List<string>> GetConfiguredProvidersAsync(CancellationToken ct = default)
    {
        var keys = LoadKeys();
        return Task.FromResult(keys.Keys.ToList());
    }

    private static string NormalizeProviderName(string provider)
    {
        if (string.IsNullOrWhiteSpace(provider))
            return "openai";

        return provider.Trim().ToLowerInvariant() switch
        {
            "moonshot" => "kimi",
            "glm" => "zhipu",
            "qwen" => "tongyi",
            "stepfun ai" => "stepfun",
            var value => value
        };
    }
}
