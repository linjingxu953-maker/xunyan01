using DesktopMascot.Core.Character;

namespace DesktopMascot.Core.Tests;

public class CharacterPackageTests
{
    private readonly CharacterPackageLoader _loader = new();
    private readonly PetdexImportConverter _converter = new();

    [Fact]
    public void Validate_ValidManifest_ShouldSucceed()
    {
        var manifest = CreateValidManifest();
        var result = _loader.Validate(manifest);
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_EmptyName_ShouldFail()
    {
        var manifest = CreateValidManifest();
        manifest.Name = "";
        var result = _loader.Validate(manifest);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("名称"));
    }

    [Fact]
    public void Validate_EmptySlug_ShouldFail()
    {
        var manifest = CreateValidManifest();
        manifest.Slug = "";
        var result = _loader.Validate(manifest);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("slug"));
    }

    [Fact]
    public void Validate_NoStates_ShouldFail()
    {
        var manifest = CreateValidManifest();
        manifest.States = new();
        var result = _loader.Validate(manifest);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("状态"));
    }

    [Fact]
    public void Validate_NoIdleState_ShouldFail()
    {
        var manifest = CreateValidManifest();
        manifest.States.Remove("Idle");
        var result = _loader.Validate(manifest);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("Idle"));
    }

    [Fact]
    public void Validate_WrongSchema_ShouldFail()
    {
        var manifest = CreateValidManifest();
        manifest.Schema = "wrong.version";
        var result = _loader.Validate(manifest);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("Schema"));
    }

    [Fact]
    public void LoadFromJson_Valid_ShouldSucceed()
    {
        var json = System.Text.Json.JsonSerializer.Serialize(CreateValidManifest());
        var result = _loader.LoadFromJson(json);
        Assert.True(result.Success);
        Assert.NotNull(result.Manifest);
        Assert.Equal("test-character", result.Manifest.Slug);
    }

    [Fact]
    public void LoadFromJson_InvalidJson_ShouldFail()
    {
        var result = _loader.LoadFromJson("{invalid json}");
        Assert.False(result.Success);
        Assert.Contains(result.Errors, e => e.Contains("解析错误"));
    }

    [Fact]
    public void LoadFromJson_EmptyName_ShouldFail()
    {
        var manifest = CreateValidManifest();
        manifest.Name = "";
        var json = System.Text.Json.JsonSerializer.Serialize(manifest);
        var result = _loader.LoadFromJson(json);
        Assert.False(result.Success);
    }

    [Fact]
    public void ImportPetdexJson_ValidPet_ShouldConvert()
    {
        var petJson = """
        {
            "name": "Boba",
            "slug": "boba",
            "kind": "cat",
            "frameSize": { "width": 192, "height": 208 },
            "grid": { "rows": 8, "cols": 9 },
            "animationStates": {
                "idle": { "row": 0, "frames": 6, "loopMs": 1100 },
                "wave": { "row": 1, "frames": 6, "loopMs": 900 },
                "run": { "row": 2, "frames": 6, "loopMs": 800 },
                "failed": { "row": 3, "frames": 6, "loopMs": 1100 },
                "review": { "row": 4, "frames": 6, "loopMs": 1100 }
            },
            "tags": ["cute", "sleepy"]
        }
        """;

        var result = _converter.ImportFromPetdexJson(petJson);
        Assert.True(result.Success);
        Assert.NotNull(result.Manifest);
        Assert.Equal("Boba", result.Manifest.Name);
        Assert.Equal("boba", result.Manifest.Slug);

        // 应映射到寻研状态
        Assert.True(result.Manifest.States.ContainsKey("Idle"));
        Assert.True(result.Manifest.States.ContainsKey("Working"));
        Assert.True(result.Manifest.States.ContainsKey("Completed"));
        Assert.True(result.Manifest.States.ContainsKey("Error"));
        Assert.True(result.Manifest.States.ContainsKey("WaitingApproval"));

        // Petdex 兼容信息应存在
        Assert.True(result.Manifest.Animation.PetdexCompatibility.Enabled);
        Assert.Equal(5, result.Manifest.Animation.PetdexCompatibility.StateMappings.Count);

        // 应有警告
        Assert.NotEmpty(result.Warnings);
    }

    [Fact]
    public void ImportPetdexJson_MissingStates_ShouldFillDefaults()
    {
        var petJson = """
        {
            "name": "Minimal",
            "slug": "minimal",
            "animationStates": {
                "idle": { "row": 0, "frames": 6, "loopMs": 1100 }
            }
        }
        """;

        var result = _converter.ImportFromPetdexJson(petJson);
        Assert.True(result.Success);

        // 缺失状态应被补全
        Assert.True(result.Manifest.States.ContainsKey("Working"));
        Assert.True(result.Manifest.States.ContainsKey("Completed"));
        Assert.True(result.Manifest.States.ContainsKey("Error"));
        Assert.True(result.Manifest.States.ContainsKey("WaitingApproval"));
    }

    [Fact]
    public void LoadFromDirectory_Nonexistent_ShouldFail()
    {
        var result = _loader.LoadFromDirectory("/nonexistent/path");
        Assert.False(result.Success);
    }

    [Fact]
    public void PetdexStateMapping_ShouldBeCorrect()
    {
        var petJson = """
        {
            "name": "Test",
            "slug": "test",
            "animationStates": {
                "idle": { "row": 0, "frames": 6, "loopMs": 1100 },
                "wave": { "row": 1, "frames": 6, "loopMs": 900 },
                "run": { "row": 2, "frames": 6, "loopMs": 800 },
                "failed": { "row": 3, "frames": 6, "loopMs": 1100 },
                "review": { "row": 4, "frames": 6, "loopMs": 1100 }
            }
        }
        """;

        var result = _converter.ImportFromPetdexJson(petJson);
        var manifest = result.Manifest;

        Assert.Equal("idle", manifest.States["Idle"].PetdexState);
        Assert.Equal("wave", manifest.States["Completed"].PetdexState);
        Assert.Equal("run", manifest.States["Working"].PetdexState);
        Assert.Equal("failed", manifest.States["Error"].PetdexState);
        Assert.Equal("review", manifest.States["WaitingApproval"].PetdexState);
    }

    private static CharacterManifest CreateValidManifest()
    {
        return new CharacterManifest
        {
            Schema = CharacterSchema.CurrentVersion,
            Name = "Test Character",
            Slug = "test-character",
            Profile = new CharacterProfile
            {
                ToneStyle = "Friendly",
                LanguageStyle = "Standard"
            },
            Appearance = new CharacterAppearance(),
            States = new Dictionary<string, CharacterStateMapping>
            {
                ["Idle"] = new() { DisplayName = "空闲", Image = "idle.png" },
                ["Working"] = new() { DisplayName = "工作中", Image = "working.png" },
                ["Completed"] = new() { DisplayName = "已完成", Image = "completed.png" }
            }
        };
    }
}
