using DesktopMascot.Core.Character;

namespace DesktopMascot.Core.Tests;

public class CharacterManagerTests
{
    private readonly CharacterManager _manager;

    public CharacterManagerTests()
    {
        _manager = new CharacterManager(new CharacterPackageLoader(), new PetdexImportConverter());
    }

    [Fact]
    public void Initial_State_ShouldNotBeReady()
    {
        Assert.False(_manager.IsReady);
        Assert.Null(_manager.Current);
    }

    [Fact]
    public void Load_ValidManifest_ShouldSetCurrent()
    {
        var dir = CreateTempDir("test-char");
        try
        {
            var result = _manager.Load(dir);
            Assert.True(result.Success);
            Assert.True(_manager.IsReady);
            Assert.NotNull(_manager.Current);
            Assert.Equal("test-char", _manager.Current.Slug);
        }
        finally { Cleanup(dir); }
    }

    [Fact]
    public void Load_MultipleCharacters_ShouldTrackAll()
    {
        var dir1 = CreateTempDir("char-a");
        var dir2 = CreateTempDir("char-b");
        try
        {
            _manager.Load(dir1);
            _manager.Load(dir2);

            var list = _manager.ListLoaded();
            Assert.Equal(2, list.Count);
            Assert.Contains(list, c => c.Slug == "char-a");
            Assert.Contains(list, c => c.Slug == "char-b");
        }
        finally { Cleanup(dir1); Cleanup(dir2); }
    }

    [Fact]
    public void SwitchTo_ExistingSlug_ShouldSwitch()
    {
        var dir1 = CreateTempDir("char-x");
        var dir2 = CreateTempDir("char-y");
        try
        {
            _manager.Load(dir1);
            _manager.Load(dir2);

            Assert.Equal("char-y", _manager.Current.Slug);

            var switched = _manager.SwitchTo("char-x");
            Assert.True(switched);
            Assert.Equal("char-x", _manager.Current.Slug);
        }
        finally { Cleanup(dir1); Cleanup(dir2); }
    }

    [Fact]
    public void SwitchTo_NonExistent_ShouldReturnFalse()
    {
        var dir = CreateTempDir("only-one");
        try
        {
            _manager.Load(dir);
            var result = _manager.SwitchTo("nonexistent");
            Assert.False(result);
            Assert.Equal("only-one", _manager.Current.Slug);
        }
        finally { Cleanup(dir); }
    }

    [Fact]
    public void ResetToDefault_ShouldGoBackToFirst()
    {
        var dir1 = CreateTempDir("first");
        var dir2 = CreateTempDir("second");
        try
        {
            _manager.Load(dir1);
            _manager.Load(dir2);
            _manager.SwitchTo("first");

            Assert.Equal("first", _manager.Current.Slug);

            _manager.ResetToDefault();
            Assert.Equal("first", _manager.Current.Slug);
        }
        finally { Cleanup(dir1); Cleanup(dir2); }
    }

    [Fact]
    public void GetResourceReport_MissingImages_ShouldReport()
    {
        var dir = CreateTempDir("report-test", includeImages: false);
        try
        {
            var report = _manager.GetResourceReport(dir);
            Assert.False(report.IsComplete);
            Assert.NotEmpty(report.MissingResources);
        }
        finally { Cleanup(dir); }
    }

    [Fact]
    public void GetResourceReport_ExistingImages_ShouldBeComplete()
    {
        var dir = CreateTempDir("report-ok", includeImages: true);
        try
        {
            var report = _manager.GetResourceReport(dir);
            Assert.True(report.IsComplete);
            Assert.Empty(report.MissingResources);
        }
        finally { Cleanup(dir); }
    }

    [Fact]
    public void ResolveStateImage_ExistingState_ShouldReturnPath()
    {
        var dir = CreateTempDir("resolve-test", includeImages: true);
        try
        {
            _manager.Load(dir);
            var (state, path) = _manager.ResolveStateImage("Idle", dir);
            Assert.Equal("Idle", state);
            Assert.NotNull(path);
            Assert.True(File.Exists(path));
        }
        finally { Cleanup(dir); }
    }

    [Fact]
    public void ResolveStateImage_MissingState_ShouldFallbackToIdle()
    {
        var dir = CreateTempDir("fallback-test", includeImages: true);
        try
        {
            _manager.Load(dir);
            var (state, path) = _manager.ResolveStateImage("Working", dir);
            Assert.Equal("Idle", state);
            Assert.NotNull(path);
        }
        finally { Cleanup(dir); }
    }

    [Fact]
    public void ConvertToPersonality_ShouldMapCorrectly()
    {
        var dir = CreateTempDir("personality-test");
        try
        {
            _manager.Load(dir);
            var manifest = _manager.Current;

            Assert.Equal("personality-test", manifest.Name);
            Assert.Equal("Friendly", manifest.Profile.ToneStyle);
            Assert.Equal("Standard", manifest.Profile.LanguageStyle);
        }
        finally { Cleanup(dir); }
    }

    private static string CreateTempDir(string slug, bool includeImages = true)
    {
        var dir = Path.Combine(Path.GetTempPath(), $"cm_test_{slug}_{Guid.NewGuid():N}");
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
