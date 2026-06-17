using System.Text.Json;
using DesktopMascot.Agent.Models;
using DesktopMascot.Core.Character;
using DesktopMascot.Core.Tools;

namespace DesktopMascot.Agent.Tools;

/// <summary>
/// 角色切换工具 — 运行时切换角色，加载角色包，更新人格设定
/// </summary>
public class CharacterSwitchTool : ITool
{
    private readonly ICharacterManager _characterManager;
    private readonly Action<AgentPersonality>? _onPersonalityChanged;

    public string Name => "character_switch";
    public string Description => "切换角色：列出已加载角色、按 slug 切换、从目录导入新角色、查看角色资源报告。";
    public string ParametersSchema => """
    {
        "type": "object",
        "properties": {
            "action": { "type": "string", "enum": ["list", "switch", "import", "current", "resource_report"], "description": "操作类型" },
            "slug": { "type": "string", "description": "角色标识（switch 时必填）" },
            "character_dir": { "type": "string", "description": "角色包目录路径（import 时必填）" },
            "petdex_dir": { "type": "string", "description": "Petdex 目录路径（从 Petdex 导入时使用）" },
            "report_dir": { "type": "string", "description": "要检查的目录（resource_report 时必填）" }
        },
        "required": ["action"]
    }
    """;

    public CharacterSwitchTool(ICharacterManager characterManager, Action<AgentPersonality>? onPersonalityChanged = null)
    {
        _characterManager = characterManager;
        _onPersonalityChanged = onPersonalityChanged;
    }

    public Task<ToolResult> ExecuteAsync(string arguments, CancellationToken ct = default)
    {
        try
        {
            var doc = JsonDocument.Parse(arguments);
            var root = doc.RootElement;
            var action = root.TryGetProperty("action", out var aEl) ? aEl.GetString() ?? "" : "";

            return action switch
            {
                "list" => ListCharacters(),
                "switch" => SwitchCharacter(root),
                "import" => ImportCharacter(root),
                "current" => GetCurrentCharacter(),
                "resource_report" => GetResourceReport(root),
                _ => Task.FromResult(Fail($"不支持的操作：{action}"))
            };
        }
        catch (Exception ex)
        {
            return Task.FromResult(Fail($"角色切换失败：{ex.Message}"));
        }
    }

    private Task<ToolResult> ListCharacters()
    {
        var list = _characterManager.ListLoaded();
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("已加载角色：");
        foreach (var c in list)
        {
            var current = c.IsCurrent ? " ← 当前" : "";
            sb.AppendLine($"  [{c.Slug}] {c.Name} (v{c.Version}){current}");
        }
        if (list.Count == 0)
            sb.AppendLine("  （无）");

        return Task.FromResult(new ToolResult { Name = Name, Success = true, Content = sb.ToString() });
    }

    private Task<ToolResult> SwitchCharacter(JsonElement root)
    {
        var slug = root.TryGetProperty("slug", out var sEl) ? sEl.GetString() ?? "" : "";
        if (string.IsNullOrEmpty(slug))
            return Task.FromResult(Fail("缺少 slug 参数"));

        var switched = _characterManager.SwitchTo(slug);
        if (!switched)
        {
            var available = string.Join(", ", _characterManager.ListLoaded().Select(c => c.Slug));
            return Task.FromResult(Fail($"角色 [{slug}] 不存在。可用角色：{available}"));
        }

        var manifest = _characterManager.Current;
        var personality = CharacterToPersonalityConverter.Convert(manifest!);
        _onPersonalityChanged?.Invoke(personality);

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"已切换到角色：{manifest.Name}");
        sb.AppendLine($"标识：{manifest.Slug}");
        sb.AppendLine($"版本：{manifest.Version}");
        sb.AppendLine($"语气：{personality.Tone}");
        sb.AppendLine($"语言风格：{personality.Language}");

        return Task.FromResult(new ToolResult { Name = Name, Success = true, Content = sb.ToString() });
    }

    private Task<ToolResult> ImportCharacter(JsonElement root)
    {
        var characterDir = root.TryGetProperty("character_dir", out var cEl) ? cEl.GetString() ?? "" : "";
        var petdexDir = root.TryGetProperty("petdex_dir", out var pEl) ? pEl.GetString() : null;

        if (!string.IsNullOrEmpty(petdexDir))
        {
            var importResult = _characterManager.ImportFromPetdex(petdexDir, switchTo: true);
            if (!importResult.Success)
                return Task.FromResult(Fail($"Petdex 导入失败：{string.Join("; ", importResult.Errors)}"));

            var manifest = importResult.Manifest!;
            var personality = CharacterToPersonalityConverter.Convert(manifest);
            _onPersonalityChanged?.Invoke(personality);

            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"已从 Petdex 导入并切换到角色：{manifest.Name}");
            sb.AppendLine($"标识：{manifest.Slug}");
            foreach (var w in importResult.Warnings)
                sb.AppendLine($"⚠ {w}");

            return Task.FromResult(new ToolResult { Name = Name, Success = true, Content = sb.ToString() });
        }

        if (string.IsNullOrEmpty(characterDir))
            return Task.FromResult(Fail("缺少 character_dir 或 petdex_dir 参数"));

        var loadResult = _characterManager.Load(characterDir);
        if (!loadResult.Success)
            return Task.FromResult(Fail($"加载失败：{string.Join("; ", loadResult.Errors)}"));

        var loaded = loadResult.Manifest!;
        var personality2 = CharacterToPersonalityConverter.Convert(loaded);
        _onPersonalityChanged?.Invoke(personality2);

        var result = new System.Text.StringBuilder();
        result.AppendLine($"已加载并切换到角色：{loaded.Name}");
        result.AppendLine($"标识：{loaded.Slug}");
        foreach (var w in loadResult.Warnings)
            result.AppendLine($"⚠ {w}");

        return Task.FromResult(new ToolResult { Name = Name, Success = true, Content = result.ToString() });
    }

    private Task<ToolResult> GetCurrentCharacter()
    {
        if (!_characterManager.IsReady)
            return Task.FromResult(Fail("当前无活动角色"));

        var manifest = _characterManager.Current!;
        var personality = CharacterToPersonalityConverter.Convert(manifest);

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("当前角色：");
        sb.AppendLine($"  名称：{manifest.Name}");
        sb.AppendLine($"  标识：{manifest.Slug}");
        sb.AppendLine($"  版本：{manifest.Version}");
        sb.AppendLine($"  角色定位：{manifest.Profile?.Role}");
        sb.AppendLine($"  语气：{personality.Tone}");
        sb.AppendLine($"  语言风格：{personality.Language}");
        sb.AppendLine($"  回复长度：{personality.LengthPreference}");
        sb.AppendLine($"  口头禅：{personality.Catchphrase}");
        if (personality.Traits.Count > 0)
            sb.AppendLine($"  性格特征：{string.Join(", ", personality.Traits)}");

        return Task.FromResult(new ToolResult { Name = Name, Success = true, Content = sb.ToString() });
    }

    private Task<ToolResult> GetResourceReport(JsonElement root)
    {
        var dir = root.TryGetProperty("report_dir", out var rEl) ? rEl.GetString() ?? "" : "";
        if (string.IsNullOrEmpty(dir))
            return Task.FromResult(Fail("缺少 report_dir 参数"));

        var report = _characterManager.GetResourceReport(dir);
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"角色资源报告：{Path.GetFileName(dir)}");
        sb.AppendLine($"完整性：{(report.IsComplete ? "完整" : "有缺失")}");
        sb.AppendLine($"头像：{report.AvatarStatus}");

        if (report.StateImageStatus.Count > 0)
        {
            sb.AppendLine("状态图片：");
            foreach (var (state, status) in report.StateImageStatus)
                sb.AppendLine($"  [{state}] {status}");
        }

        if (report.MissingResources.Count > 0)
        {
            sb.AppendLine("缺失资源：");
            foreach (var missing in report.MissingResources)
                sb.AppendLine($"  - {missing}");
        }

        if (report.Warnings.Count > 0)
        {
            sb.AppendLine("警告：");
            foreach (var warning in report.Warnings)
                sb.AppendLine($"  - {warning}");
        }

        return Task.FromResult(new ToolResult { Name = Name, Success = true, Content = sb.ToString() });
    }

    private static ToolResult Fail(string error) => new() { Name = "character_switch", Success = false, Error = error };
}
