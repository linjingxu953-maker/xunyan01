using DesktopMascot.UI.Services;

namespace DesktopMascot.UI.Tests;

public sealed class MascotCharacterPackageExporterTests
{
    [Fact]
    public async Task ExportAsync_WritesPortableManifestAndCopiesReferencedImages()
    {
        using var source = CharacterPackageExportTestRoot.CreateSource(
            ("avatar.png", [1, 2, 3]),
            ("idle.png", [4, 5, 6]),
            ("working.png", [7, 8, 9]));
        using var destination = CharacterPackageExportTestRoot.CreateDestination();
        var profile = new MascotCharacterProfile
        {
            Name = "月光",
            AvatarText = "月",
            ImageFolder = source.RootPath,
            AvatarImage = "avatar.png",
            StateImages = new Dictionary<string, string>
            {
                ["Idle"] = "idle.png",
                ["Working"] = "working.png"
            }
        };
        var manifest = MascotCharacterManifestFactory.Create(
            profile,
            new Dictionary<string, string>
            {
                ["Idle"] = "空闲",
                ["Working"] = "工作中"
            });

        var result = await MascotCharacterPackageExporter.ExportAsync(
            manifest,
            destination.RootPath);

        Assert.True(result.Success);
        Assert.Equal(3, result.CopiedImageCount);
        Assert.True(File.Exists(Path.Combine(result.PackageDirectory, "character.manifest.json")));
        Assert.True(File.Exists(Path.Combine(result.PackageDirectory, "assets", "avatar.png")));
        Assert.True(File.Exists(Path.Combine(result.PackageDirectory, "assets", "idle.png")));
        Assert.True(File.Exists(Path.Combine(result.PackageDirectory, "assets", "working.png")));

        var exportedJson = await File.ReadAllTextAsync(result.ManifestPath);
        var exportedManifest = MascotCharacterManifestFactory.FromJson(exportedJson);

        Assert.NotNull(exportedManifest);
        Assert.Equal("assets", exportedManifest.Appearance.ImageFolder);
        Assert.Equal("avatar.png", exportedManifest.Appearance.AvatarImage);
        Assert.Equal("idle.png", exportedManifest.States["Idle"].Image);
        Assert.Equal("working.png", exportedManifest.States["Working"].Image);
    }

    [Fact]
    public async Task ExportAsync_ReportsMissingImagesWithoutFailingManifestExport()
    {
        using var source = CharacterPackageExportTestRoot.CreateSource(("avatar.png", [1]));
        using var destination = CharacterPackageExportTestRoot.CreateDestination();
        var profile = new MascotCharacterProfile
        {
            Name = "月光",
            ImageFolder = source.RootPath,
            AvatarImage = "avatar.png",
            StateImages = new Dictionary<string, string>
            {
                ["Idle"] = "missing-idle.png"
            }
        };
        var manifest = MascotCharacterManifestFactory.Create(
            profile,
            new Dictionary<string, string> { ["Idle"] = "空闲" });

        var result = await MascotCharacterPackageExporter.ExportAsync(
            manifest,
            destination.RootPath);

        Assert.True(result.Success);
        Assert.Equal(1, result.CopiedImageCount);
        Assert.Contains("空闲", result.MissingItems);
        Assert.True(File.Exists(result.ManifestPath));
    }
}

file sealed class CharacterPackageExportTestRoot : IDisposable
{
    private CharacterPackageExportTestRoot(string rootPath) => RootPath = rootPath;

    public string RootPath { get; }

    public static CharacterPackageExportTestRoot CreateSource(params (string FileName, byte[] Content)[] files)
    {
        var root = CreateRoot("DesktopMascotCharacterPackageSource");
        foreach (var (fileName, content) in files)
        {
            File.WriteAllBytes(Path.Combine(root, fileName), content);
        }

        return new CharacterPackageExportTestRoot(root);
    }

    public static CharacterPackageExportTestRoot CreateDestination()
    {
        return new CharacterPackageExportTestRoot(CreateRoot("DesktopMascotCharacterPackageDestination"));
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

    private static string CreateRoot(string prefix)
    {
        var root = Path.Combine(Path.GetTempPath(), prefix, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }
}
