using System.Text.Json;

namespace DesktopMascot.Core.Character;

/// <summary>
/// 角色管理器接口 — 运行时加载/切换/获取当前角色
/// </summary>
public interface ICharacterManager
{
    /// <summary>当前角色</summary>
    CharacterManifest? Current { get; }

    /// <summary>当前角色是否就绪</summary>
    bool IsReady { get; }

    /// <summary>加载指定目录的角色包</summary>
    CharacterLoadResult Load(string characterDirectory);

    /// <summary>从 JSON 字符串加载角色包</summary>
    CharacterLoadResult LoadFromJson(string json);

    /// <summary>切换到已加载的角色（按 slug）</summary>
    bool SwitchTo(string slug);

    /// <summary>获取所有已加载角色的 slug 和名称</summary>
    IReadOnlyList<CharacterSummary> ListLoaded();

    /// <summary>获取角色资源完整性报告</summary>
    CharacterResourceReport GetResourceReport(string characterDirectory);

    /// <summary>从 Petdex 目录导入并切换</summary>
    CharacterLoadResult ImportFromPetdex(string petdexDirectory, bool switchTo = true);

    /// <summary>恢复到默认角色</summary>
    void ResetToDefault();
}

public class CharacterSummary
{
    public string Slug { get; set; } = "";
    public string Name { get; set; } = "";
    public string Version { get; set; } = "";
    public bool IsCurrent { get; set; }
}

public class CharacterResourceReport
{
    public string CharacterDirectory { get; set; } = "";
    public bool IsComplete { get; set; }
    public string AvatarStatus { get; set; } = "";
    public Dictionary<string, string> StateImageStatus { get; set; } = new();
    public List<string> MissingResources { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}
