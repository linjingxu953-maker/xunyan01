using DesktopMascot.Core.Configuration;

namespace DesktopMascot.Core.Tests;

public class ConfigurationManagerTests : IDisposable
{
    private readonly FileConfigurationManager _manager;
    private readonly string _testDir;

    public ConfigurationManagerTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"config_test_{Guid.NewGuid():N}");
        _manager = new FileConfigurationManager(_testDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
            Directory.Delete(_testDir, true);
    }

    [Fact]
    public async Task AppSettings_ShouldPersist()
    {
        var settings = new AppSettings
        {
            ApiKey = "test-key",
            ModelName = "gpt-4",
            Language = "zh-CN"
        };

        await _manager.SaveAppSettingsAsync(settings);
        var loaded = await _manager.GetAppSettingsAsync();

        Assert.Equal("test-key", loaded.ApiKey);
        Assert.Equal("gpt-4", loaded.ModelName);
        Assert.Equal("zh-CN", loaded.Language);
    }

    [Fact]
    public async Task UserPreferences_ShouldPersist()
    {
        var preferences = new UserPreferences
        {
            UserName = "测试用户",
            NotificationsEnabled = false,
            FavoriteTools = new List<string> { "tool1", "tool2" }
        };

        await _manager.SaveUserPreferencesAsync(preferences);
        var loaded = await _manager.GetUserPreferencesAsync();

        Assert.Equal("测试用户", loaded.UserName);
        Assert.False(loaded.NotificationsEnabled);
        Assert.Equal(2, loaded.FavoriteTools.Count);
    }

    [Fact]
    public async Task ProjectSettings_ShouldPersist()
    {
        var settings = new ProjectSettings
        {
            ProjectId = "project-1",
            ProjectName = "测试项目",
            TechStack = new List<string> { "C#", "Avalonia" }
        };

        await _manager.SaveProjectSettingsAsync(settings);
        var loaded = await _manager.GetProjectSettingsAsync("project-1");

        Assert.NotNull(loaded);
        Assert.Equal("测试项目", loaded!.ProjectName);
        Assert.Contains("C#", loaded.TechStack);
    }

    [Fact]
    public async Task PermissionSettings_ShouldPersist()
    {
        var settings = new PermissionSettings
        {
            AutoApproveLevel = 2,
            AuditLogRetentionDays = 60,
            BlockedCommands = new List<string> { "del", "rm" }
        };

        await _manager.SavePermissionSettingsAsync(settings);
        var loaded = await _manager.GetPermissionSettingsAsync();

        Assert.Equal(2, loaded.AutoApproveLevel);
        Assert.Equal(60, loaded.AuditLogRetentionDays);
        Assert.Contains("del", loaded.BlockedCommands);
    }

    [Fact]
    public async Task ResetToDefaults_ShouldResetAll()
    {
        await _manager.SaveAppSettingsAsync(new AppSettings { ApiKey = "test" });
        await _manager.SaveUserPreferencesAsync(new UserPreferences { UserName = "user" });

        await _manager.ResetToDefaultsAsync();

        var appSettings = await _manager.GetAppSettingsAsync();
        var userPrefs = await _manager.GetUserPreferencesAsync();

        Assert.Equal("", appSettings.ApiKey);
        Assert.Equal("", userPrefs.UserName);
    }

    [Fact]
    public async Task GetAppSettings_WhenNotExists_ShouldReturnDefaults()
    {
        var settings = await _manager.GetAppSettingsAsync();

        Assert.Equal("gpt-4", settings.ModelName);
        Assert.Equal("zh-CN", settings.Language);
    }
}

public class ConfigurationValidationTests
{
    [Fact]
    public void AppSettings_EmptyApiKey_ShouldHaveError()
    {
        var settings = new AppSettings { ApiKey = "" };
        var errors = settings.Validate();

        Assert.Contains(errors, e => e.Contains("API Key"));
    }

    [Fact]
    public void AppSettings_Valid_ShouldHaveNoErrors()
    {
        var settings = new AppSettings
        {
            ApiKey = "test-key",
            ModelName = "gpt-4",
            ApiEndpoint = "https://api.openai.com/v1"
        };
        var errors = settings.Validate();

        Assert.Empty(errors);
    }

    [Fact]
    public void PermissionSettings_InvalidLevel_ShouldHaveError()
    {
        var settings = new PermissionSettings { AutoApproveLevel = 10 };
        var errors = settings.Validate();

        Assert.Contains(errors, e => e.Contains("级别"));
    }

    [Fact]
    public void UserPreferences_NegativeInterval_ShouldHaveError()
    {
        var preferences = new UserPreferences { AutoSaveInterval = -1 };
        var errors = preferences.Validate();

        Assert.Contains(errors, e => e.Contains("间隔"));
    }
}
