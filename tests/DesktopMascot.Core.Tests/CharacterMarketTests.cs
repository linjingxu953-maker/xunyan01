using DesktopMascot.Core.Character;

namespace DesktopMascot.Core.Tests;

/// <summary>
/// 角色包市场模型 + 加载器集成测试
/// </summary>
public class CharacterMarketTests
{
    private readonly CharacterPackageLoader _loader = new();
    private readonly CharacterManager _manager = new(new CharacterPackageLoader(), new PetdexImportConverter());

    [Fact]
    public void MarketInfo_DefaultValues_ShouldBeEmpty()
    {
        var market = new CharacterMarketInfo();
        Assert.Equal("", market.StoreId);
        Assert.Equal("", market.PreviewImage);
        Assert.Equal(0f, market.Rating);
        Assert.Equal(0, market.Downloads);
        Assert.False(market.IsFeatured);
    }

    [Fact]
    public void MarketInfo_InMetadata_ShouldSerializeCorrectly()
    {
        var manifest = new CharacterManifest
        {
            Schema = CharacterSchema.CurrentVersion,
            Name = "Market Test",
            Slug = "market-test",
            Metadata = new CharacterMetadata
            {
                Author = "tester",
                Market = new CharacterMarketInfo
                {
                    StoreId = "store-001",
                    Rating = 4.5f,
                    Downloads = 100,
                    IsFeatured = true,
                    PublishedAt = new DateTime(2026, 1, 1)
                }
            }
        };

        var json = System.Text.Json.JsonSerializer.Serialize(manifest);
        var loaded = System.Text.Json.JsonSerializer.Deserialize<CharacterManifest>(json);

        Assert.NotNull(loaded);
        Assert.NotNull(loaded.Metadata.Market);
        Assert.Equal("store-001", loaded.Metadata.Market.StoreId);
        Assert.Equal(4.5f, loaded.Metadata.Market.Rating);
        Assert.Equal(100, loaded.Metadata.Market.Downloads);
        Assert.True(loaded.Metadata.Market.IsFeatured);
    }

    [Fact]
    public void Load_WithMarketInfo_ShouldPreserveData()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"cmkt_preserve_{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);

        try
        {
            // 直接写 JSON，确保 Market 字段存在
            var json = """
            {
                "schema": "xunyan.character.v1",
                "name": "Market Character",
                "slug": "market-char",
                "profile": { "toneStyle": "Friendly" },
                "states": { "Idle": { "displayName": "空闲" } },
                "metadata": {
                    "author": "test",
                    "market": {
                        "storeId": "store-002",
                        "previewImage": "preview.png",
                        "description": "A test character",
                        "rating": 4.8,
                        "downloads": 500,
                        "downloadUrl": "https://example.com/download",
                        "compatibleVersion": "1.0",
                        "isFeatured": false
                    }
                }
            }
            """;

            File.WriteAllText(Path.Combine(dir, "character.json"), json);

            var result = _loader.LoadFromDirectory(dir);
            Assert.True(result.Success);
            Assert.NotNull(result.Manifest.Metadata.Market);
            Assert.Equal("store-002", result.Manifest.Metadata.Market.StoreId);
            Assert.Equal(4.8f, result.Manifest.Metadata.Market.Rating);
            Assert.Equal(500, result.Manifest.Metadata.Market.Downloads);
        }
        finally { Cleanup(dir); }
    }

    [Fact]
    public void Load_NoMarketInfo_ShouldBeNull()
    {
        var dir = CreateTempDir("no-market");
        try
        {
            var result = _loader.LoadFromDirectory(dir);
            Assert.True(result.Success);
            Assert.Null(result.Manifest.Metadata.Market);
        }
        finally { Cleanup(dir); }
    }

    [Fact]
    public void Validate_WithMarketInfo_ShouldPass()
    {
        var manifest = new CharacterManifest
        {
            Schema = CharacterSchema.CurrentVersion,
            Name = "Valid",
            Slug = "valid",
            Profile = new CharacterProfile { ToneStyle = "Friendly" },
            States = new Dictionary<string, CharacterStateMapping>
            {
                ["Idle"] = new() { DisplayName = "空闲" }
            },
            Metadata = new CharacterMetadata
            {
                Market = new CharacterMarketInfo
                {
                    StoreId = "store-003",
                    Rating = 5.0f
                }
            }
        };

        var result = _loader.Validate(manifest);
        Assert.True(result.IsValid);
    }

    private static string CreateTempDir(string slug)
    {
        var dir = Path.Combine(Path.GetTempPath(), $"cmkt_{slug}_{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);

        var manifest = new CharacterManifest
        {
            Schema = CharacterSchema.CurrentVersion,
            Name = slug,
            Slug = slug,
            Profile = new CharacterProfile { ToneStyle = "Friendly" },
            States = new Dictionary<string, CharacterStateMapping>
            {
                ["Idle"] = new() { DisplayName = "空闲" }
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
