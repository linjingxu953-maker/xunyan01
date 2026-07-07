using DesktopMascot.Agent.Providers;

namespace DesktopMascot.Agent.Tests;

public sealed class ApiKeyStoreTests : IDisposable
{
    private readonly string _storageDirectory = Path.Combine(
        Path.GetTempPath(),
        $"desktop_mascot_key_store_{Guid.NewGuid():N}");

    [Fact]
    public async Task FileApiKeyStore_ShouldStoreProtectedValueWithoutPlaintext()
    {
        Directory.CreateDirectory(_storageDirectory);
        var store = new FileApiKeyStore(_storageDirectory);

        await store.SetApiKeyAsync("openai", "plain-secret");

        var rawJson = await File.ReadAllTextAsync(Path.Combine(_storageDirectory, "api_keys.json"));
        var restored = await store.GetApiKeyAsync("openai");

        Assert.DoesNotContain("plain-secret", rawJson);
        Assert.Equal("plain-secret", restored);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_storageDirectory))
            {
                Directory.Delete(_storageDirectory, recursive: true);
            }
        }
        catch
        {
            // Best-effort cleanup.
        }
    }
}
