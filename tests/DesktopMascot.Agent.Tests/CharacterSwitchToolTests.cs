using DesktopMascot.Core.Character;
using DesktopMascot.Agent.Tools;
using DesktopMascot.Agent.Models;

namespace DesktopMascot.Agent.Tests;

public class CharacterSwitchToolTests
{
    private readonly CharacterManager _manager;
    private readonly CharacterSwitchTool _tool;
    private AgentPersonality? _lastPersonality;

    public CharacterSwitchToolTests()
    {
        _manager = new CharacterManager(new CharacterPackageLoader(), new PetdexImportConverter());
        _tool = new CharacterSwitchTool(_manager, p => _lastPersonality = p);

        _manager.Load(CreateTempDir("alpha"));
        _manager.Load(CreateTempDir("beta"));
    }

    [Fact]
    public async Task List_ShouldShowAllCharacters()
    {
        var result = await _tool.ExecuteAsync("""{"action":"list"}""");
        Assert.True(result.Success);
        Assert.Contains("alpha", result.Content);
        Assert.Contains("beta", result.Content);
        Assert.Contains("当前", result.Content);
    }

    [Fact]
    public async Task Switch_ShouldChangeCurrent()
    {
        var result = await _tool.ExecuteAsync("""{"action":"switch","slug":"alpha"}""");
        Assert.True(result.Success);
        Assert.Contains("alpha", result.Content);
        Assert.Equal("alpha", _manager.Current.Slug);
        Assert.NotNull(_lastPersonality);
        Assert.Equal("alpha", _lastPersonality.Name);
    }

    [Fact]
    public async Task Switch_NonExistent_ShouldFail()
    {
        var result = await _tool.ExecuteAsync("""{"action":"switch","slug":"nonexistent"}""");
        Assert.False(result.Success);
        Assert.Contains("不存在", result.Error);
    }

    [Fact]
    public async Task Current_ShouldReturnInfo()
    {
        var result = await _tool.ExecuteAsync("""{"action":"current"}""");
        Assert.True(result.Success);
        Assert.Contains("当前角色", result.Content);
        Assert.Contains("beta", result.Content);
    }

    [Fact]
    public async Task Import_FromDir_ShouldLoadAndSwitch()
    {
        var dir = CreateTempDir("gamma");
        try
        {
            var escaped = dir.Replace("\\", "/");
            var args = "{\"action\":\"import\",\"character_dir\":\"" + escaped + "\"}";
            var result = await _tool.ExecuteAsync(args);
            Assert.True(result.Success);
            Assert.Contains("gamma", result.Content);
            Assert.Equal("gamma", _manager.Current.Slug);
            Assert.NotNull(_lastPersonality);
        }
        finally { Cleanup(dir); }
    }

    [Fact]
    public async Task ResourceReport_Complete_ShouldReportOk()
    {
        var dir = CreateTempDir("report-ok", includeImages: true);
        try
        {
            var escaped = dir.Replace("\\", "/");
            var args = "{\"action\":\"resource_report\",\"report_dir\":\"" + escaped + "\"}";
            var result = await _tool.ExecuteAsync(args);
            Assert.True(result.Success);
            Assert.Contains("完整", result.Content);
        }
        finally { Cleanup(dir); }
    }

    [Fact]
    public async Task ResourceReport_Missing_ShouldReportIssues()
    {
        var dir = CreateTempDir("report-miss", includeImages: false);
        try
        {
            var escaped = dir.Replace("\\", "/");
            var args = "{\"action\":\"resource_report\",\"report_dir\":\"" + escaped + "\"}";
            var result = await _tool.ExecuteAsync(args);
            Assert.True(result.Success);
            Assert.Contains("缺失", result.Content);
        }
        finally { Cleanup(dir); }
    }

    private static string CreateTempDir(string slug, bool includeImages = true)
    {
        var dir = Path.Combine(Path.GetTempPath(), $"cst_{slug}_{Guid.NewGuid():N}");
        var imgDir = Path.Combine(dir, "assets");
        Directory.CreateDirectory(imgDir);

        if (includeImages)
        {
            var pngBytes = new byte[]
            {
                0x89,0x50,0x4E,0x47,0x0D,0x0A,0x1A,0x0A,0x00,0x00,0x00,0x0D,0x49,0x48,0x44,0x52,
                0x00,0x00,0x00,0x01,0x00,0x00,0x00,0x01,0x08,0x02,0x00,0x00,0x00,0x90,0x77,0x53,
                0xDE,0x00,0x00,0x00,0x0C,0x49,0x44,0x41,0x54,0x08,0xD7,0x63,0xF8,0xCF,0xC0,0x00,
                0x00,0x00,0x02,0x00,0x01,0xE2,0x21,0xBC,0x33,0x00,0x00,0x00,0x00,0x49,0x45,0x4E,
                0x44,0xAE,0x42,0x60,0x82
            };
            File.WriteAllBytes(Path.Combine(imgDir, "avatar.png"), pngBytes);
            File.WriteAllBytes(Path.Combine(imgDir, "idle.png"), pngBytes);
        }

        var manifest = new CharacterManifest
        {
            Schema = CharacterSchema.CurrentVersion,
            Name = slug,
            Slug = slug,
            Profile = new CharacterProfile { ToneStyle = "Friendly" },
            Appearance = new CharacterAppearance
            {
                ImageFolder = "assets",
                AvatarImage = "avatar.png"
            },
            States = new Dictionary<string, CharacterStateMapping>
            {
                ["Idle"] = new() { DisplayName = "空闲", Image = "idle.png" }
            }
        };

        File.WriteAllText(
            Path.Combine(dir, "character.json"),
            System.Text.Json.JsonSerializer.Serialize(manifest));

        return dir;
    }

    private static void Cleanup(string dir)
    {
        try { if (Directory.Exists(dir)) Directory.Delete(dir, true); } catch { }
    }
}
