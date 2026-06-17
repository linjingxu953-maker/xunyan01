using DesktopMascot.Core.Character;

namespace DesktopMascot.Core.Tests;

/// <summary>
/// 角色包端到端集成测试 — 覆盖加载、校验、导入、缺图回退、状态映射
/// </summary>
public class CharacterIntegrationTests
{
    private readonly CharacterPackageLoader _loader = new();
    private readonly PetdexImportConverter _converter = new();

    [Fact]
    public void EndToEnd_LoadValidPackage_ShouldWork()
    {
        var dir = CreateTempCharacterDir("valid-character", includeImages: true);
        try
        {
            var result = _loader.LoadFromDirectory(dir);
            Assert.True(result.Success);
            Assert.NotNull(result.Manifest);
            Assert.Equal("valid-character", result.Manifest.Slug);
            Assert.Equal("test-user", result.Manifest.Metadata.Author);
        }
        finally { CleanupDir(dir); }
    }

    [Fact]
    public void EndToEnd_MissingImages_ShouldWarn()
    {
        var dir = CreateTempCharacterDir("missing-images", includeImages: false);
        try
        {
            var result = _loader.LoadFromDirectory(dir);
            Assert.True(result.Success);
            Assert.NotEmpty(result.Warnings);
            Assert.Contains(result.Warnings, w => w.Contains("不存在"));
        }
        finally { CleanupDir(dir); }
    }

    [Fact]
    public void EndToEnd_InvalidJson_ShouldFail()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"char_invalid_{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "character.json"), "{ broken json !!!");
        try
        {
            var result = _loader.LoadFromDirectory(dir);
            Assert.False(result.Success);
            Assert.Contains(result.Errors, e => e.Contains("解析错误"));
        }
        finally { CleanupDir(dir); }
    }

    [Fact]
    public void EndToEnd_PetdexImportThenValidate_ShouldWork()
    {
        var petJson = """
        {
            "name": "Pixel Cat",
            "slug": "pixel-cat",
            "kind": "cat",
            "frameSize": { "width": 192, "height": 208 },
            "grid": { "rows": 8, "cols": 9 },
            "animationStates": {
                "idle": { "row": 0, "frames": 6, "loopMs": 1100 },
                "wave": { "row": 1, "frames": 6, "loopMs": 900 },
                "run": { "row": 2, "frames": 6, "loopMs": 800 },
                "failed": { "row": 3, "frames": 6, "loopMs": 1100 },
                "review": { "row": 4, "frames": 6, "loopMs": 1100 },
                "jump": { "row": 5, "frames": 6, "loopMs": 700 }
            },
            "tags": ["pixel", "cat", "cute"]
        }
        """;

        // Step 1: 导入
        var importResult = _converter.ImportFromPetdexJson(petJson);
        Assert.True(importResult.Success);
        var manifest = importResult.Manifest;

        // Step 2: 校验
        var validationResult = _loader.Validate(manifest!);
        Assert.True(validationResult.IsValid);

        // Step 3: 校验状态完整性
        Assert.Equal(5, manifest.States.Count); // Idle/Working/Completed/Error/WaitingApproval
        Assert.Contains("Idle", manifest.States.Keys);
        Assert.Contains("Working", manifest.States.Keys);
        Assert.Contains("Completed", manifest.States.Keys);
        Assert.Contains("Error", manifest.States.Keys);
        Assert.Contains("WaitingApproval", manifest.States.Keys);

        // Step 4: 校验 Petdex 兼容信息
        Assert.True(manifest.Animation.PetdexCompatibility.Enabled);
        Assert.Equal(6, manifest.Animation.PetdexCompatibility.StateMappings.Count);
    }

    [Fact]
    public void EndToEnd_PetdexImportMinimalPet_ShouldFillDefaults()
    {
        var petJson = """
        {
            "name": "Minimal",
            "animationStates": {
                "idle": { "row": 0, "frames": 6, "loopMs": 1100 }
            }
        }
        """;

        var result = _converter.ImportFromPetdexJson(petJson);
        Assert.True(result.Success);

        var manifest = result.Manifest;

        // slug 应自动生成
        Assert.Equal("minimal", manifest.Slug);

        // 缺失状态应被补全
        Assert.True(manifest.States.ContainsKey("Working"));
        Assert.True(manifest.States.ContainsKey("Completed"));
        Assert.True(manifest.States.ContainsKey("Error"));
        Assert.True(manifest.States.ContainsKey("WaitingApproval"));

        // 人格设定应有默认值
        Assert.Equal("Friendly", manifest.Profile.ToneStyle);
        Assert.Equal("Standard", manifest.Profile.LanguageStyle);
        Assert.Equal("Balanced", manifest.Profile.ReplyLength);

        // 元数据应有标签
        Assert.Contains("imported-from-petdex", manifest.Metadata.Tags);
    }

    [Fact]
    public void EndToEnd_StateMapping_ShouldBeBidirectional()
    {
        var petJson = """
        {
            "name": "Bidirectional",
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

        // 寻研 → Petdex 反向映射
        Assert.Equal("idle", manifest.States["Idle"].PetdexState);
        Assert.Equal("wave", manifest.States["Completed"].PetdexState);
        Assert.Equal("run", manifest.States["Working"].PetdexState);
        Assert.Equal("failed", manifest.States["Error"].PetdexState);
        Assert.Equal("review", manifest.States["WaitingApproval"].PetdexState);
    }

    [Fact]
    public void EndToEnd_ExportAndReImport_ShouldPreserveData()
    {
        var original = new CharacterManifest
        {
            Schema = CharacterSchema.CurrentVersion,
            Name = "RoundTrip",
            Slug = "roundtrip",
            Profile = new CharacterProfile
            {
                ToneStyle = "Professional",
                LanguageStyle = "Technical",
                Traits = new List<string> { "precise", "thorough" }
            },
            States = new Dictionary<string, CharacterStateMapping>
            {
                ["Idle"] = new() { DisplayName = "空闲", Image = "idle.png" },
                ["Working"] = new() { DisplayName = "工作中", Image = "work.png" }
            }
        };

        // 导出为 JSON
        var json = System.Text.Json.JsonSerializer.Serialize(original);

        // 重新导入
        var loaded = _loader.LoadFromJson(json);
        Assert.True(loaded.Success);

        var restored = loaded.Manifest;
        Assert.Equal("RoundTrip", restored.Name);
        Assert.Equal("Professional", restored.Profile.ToneStyle);
        Assert.Equal(2, restored.States.Count);
        Assert.Equal("idle.png", restored.States["Idle"].Image);
    }

    private static string CreateTempCharacterDir(string slug, bool includeImages)
    {
        var dir = Path.Combine(Path.GetTempPath(), $"char_{slug}_{Guid.NewGuid():N}");
        var imgDir = Path.Combine(dir, "assets");
        Directory.CreateDirectory(imgDir);

        if (includeImages)
        {
            // 最小合法 PNG（1x1 白色像素，70 bytes）
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
            },
            Metadata = new CharacterMetadata { Author = "test-user" }
        };

        File.WriteAllText(
            Path.Combine(dir, "character.json"),
            System.Text.Json.JsonSerializer.Serialize(manifest));

        return dir;
    }

    private static void CleanupDir(string dir)
    {
        try { if (Directory.Exists(dir)) Directory.Delete(dir, true); } catch { }
    }
}
