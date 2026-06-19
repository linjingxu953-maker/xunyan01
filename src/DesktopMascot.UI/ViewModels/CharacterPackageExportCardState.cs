using DesktopMascot.UI.Services;

namespace DesktopMascot.UI.ViewModels;

public sealed class CharacterPackageExportCardState
{
    private CharacterPackageExportCardState(
        string summaryText,
        string schemaText,
        string exportFileName,
        string compatibilityText,
        string importHint)
    {
        SummaryText = summaryText;
        SchemaText = schemaText;
        ExportFileName = exportFileName;
        CompatibilityText = compatibilityText;
        ImportHint = importHint;
    }

    public static CharacterPackageExportCardState Empty { get; } = new(
        "寻研01角色包 · 未命名 · 0 个状态图",
        MascotCharacterManifestFactory.Schema,
        "character/character.manifest.json",
        "Petdex 仅作为可选导入兼容信息",
        "导出会生成 character.manifest.json 和 assets 图片目录；导入角色包会读取这个结构。");

    public string SummaryText { get; }
    public string SchemaText { get; }
    public string ExportFileName { get; }
    public string CompatibilityText { get; }
    public string ImportHint { get; }

    public static CharacterPackageExportCardState FromManifest(MascotCharacterManifest manifest)
    {
        var name = CleanText(manifest.Name, "未命名");
        var slug = CleanText(manifest.Slug, "character");
        var stateCount = manifest.States.Count;
        var compatibilityText = manifest.Animation.PetdexCompatibility.Enabled
            ? "Petdex 导入兼容已启用"
            : "Petdex 仅作为可选导入兼容信息";

        return new CharacterPackageExportCardState(
            $"寻研01角色包 · {name} · {stateCount} 个状态图",
            CleanText(manifest.Schema, MascotCharacterManifestFactory.Schema),
            $"{slug}/character.manifest.json",
            compatibilityText,
            "导出会生成 character.manifest.json 和 assets 图片目录；导入角色包会读取这个结构。");
    }

    private static string CleanText(string? value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    }
}
