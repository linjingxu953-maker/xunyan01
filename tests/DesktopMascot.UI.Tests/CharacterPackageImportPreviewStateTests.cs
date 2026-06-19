using DesktopMascot.UI.Services;
using DesktopMascot.UI.ViewModels;

namespace DesktopMascot.UI.Tests;

public sealed class CharacterPackageImportPreviewStateTests
{
    [Fact]
    public void FromManifest_SummarizesAvailableAndMissingStateImages()
    {
        using var package = CharacterPackageTestRoot.Create(
            ("characters", "avatar.png"),
            ("characters", "idle.png"));
        var manifest = new MascotCharacterManifest
        {
            Schema = MascotCharacterManifestFactory.Schema,
            Name = "月光",
            Slug = "yue-guang",
            Appearance = new MascotCharacterAppearanceManifest
            {
                AvatarText = "月",
                ImageFolder = "characters",
                AvatarImage = "avatar.png"
            },
            States = new Dictionary<string, MascotCharacterStateManifest>
            {
                ["Idle"] = new() { DisplayName = "空闲", Image = "idle.png" },
                ["Working"] = new() { DisplayName = "工作中", Image = "working.png" }
            }
        };

        var state = CharacterPackageImportPreviewState.FromManifest(manifest, package.ManifestPath);

        Assert.True(state.HasPreview);
        Assert.True(state.CanImport);
        Assert.Equal("月光 · xunyan.character.v1 · 1/2 状态图可用", state.SummaryText);
        Assert.Equal("缺少：工作中", state.MissingStateImageText);
        Assert.NotNull(state.Profile);
        Assert.Equal(Path.Combine(package.RootPath, "characters"), state.Profile.ImageFolder);
    }

    [Fact]
    public void FromManifest_DisablesImportForUnsupportedSchema()
    {
        using var package = CharacterPackageTestRoot.Create();
        var manifest = new MascotCharacterManifest
        {
            Schema = "petdex.character.v1",
            Name = "外部角色"
        };

        var state = CharacterPackageImportPreviewState.FromManifest(manifest, package.ManifestPath);

        Assert.False(state.CanImport);
        Assert.Contains("暂不支持", state.StatusText);
    }
}

file sealed class CharacterPackageTestRoot : IDisposable
{
    private CharacterPackageTestRoot(string rootPath)
    {
        RootPath = rootPath;
        ManifestPath = Path.Combine(rootPath, "character.manifest.json");
        File.WriteAllText(ManifestPath, "{}");
    }

    public string RootPath { get; }
    public string ManifestPath { get; }

    public static CharacterPackageTestRoot Create(params (string FolderName, string FileName)[] files)
    {
        var root = Path.Combine(Path.GetTempPath(), "DesktopMascotCharacterPackage", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        foreach (var (folderName, fileName) in files)
        {
            var directory = Path.Combine(root, folderName);
            Directory.CreateDirectory(directory);
            File.WriteAllBytes(Path.Combine(directory, fileName), [0]);
        }

        return new CharacterPackageTestRoot(root);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(RootPath))
            {
                Directory.Delete(RootPath, recursive: true);
            }
        }
        catch
        {
        }
    }
}
