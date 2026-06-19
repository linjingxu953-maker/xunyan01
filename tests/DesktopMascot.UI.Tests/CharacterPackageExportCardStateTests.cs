using DesktopMascot.UI.Services;
using DesktopMascot.UI.ViewModels;

namespace DesktopMascot.UI.Tests;

public sealed class CharacterPackageExportCardStateTests
{
    [Fact]
    public void FromManifest_SummarizesXunyanPackageWithoutMakingPetdexPrimary()
    {
        var manifest = new MascotCharacterManifest
        {
            Schema = MascotCharacterManifestFactory.Schema,
            Name = "月光",
            Slug = "yue-guang",
            States = new Dictionary<string, MascotCharacterStateManifest>
            {
                ["Idle"] = new() { DisplayName = "空闲", Image = "idle.png" },
                ["Working"] = new() { DisplayName = "工作中", Image = "working.png" }
            }
        };

        var state = CharacterPackageExportCardState.FromManifest(manifest);

        Assert.Equal("寻研01角色包 · 月光 · 2 个状态图", state.SummaryText);
        Assert.Equal("yue-guang/character.manifest.json", state.ExportFileName);
        Assert.Equal("Petdex 仅作为可选导入兼容信息", state.CompatibilityText);
    }
}
