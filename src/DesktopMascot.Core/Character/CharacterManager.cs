namespace DesktopMascot.Core.Character;

/// <summary>
/// 角色管理器实现 — 运行时角色加载/切换/缺图回退
/// </summary>
public class CharacterManager : ICharacterManager
{
    private readonly CharacterPackageLoader _loader;
    private readonly PetdexImportConverter _converter;
    private readonly Dictionary<string, CharacterManifest> _loadedCharacters = new();
    private readonly object _lock = new();
    private CharacterManifest? _current;
    private CharacterManifest? _default;

    public CharacterManager(CharacterPackageLoader loader, PetdexImportConverter converter)
    {
        _loader = loader;
        _converter = converter;
    }

    public CharacterManifest? Current => _current;
    public bool IsReady => _current != null;

    public CharacterLoadResult Load(string characterDirectory)
    {
        var result = _loader.LoadFromDirectory(characterDirectory);
        if (!result.Success || result.Manifest == null)
            return result;

        var manifest = result.Manifest;

        lock (_lock)
        {
            _loadedCharacters[manifest.Slug] = manifest;

            // 第一个加载的角色成为默认角色
            _default ??= manifest;

            // 每次加载新角色时切换到它
            _current = manifest;
        }

        return result;
    }

    public CharacterLoadResult LoadFromJson(string json)
    {
        var result = _loader.LoadFromJson(json);
        if (!result.Success || result.Manifest == null)
            return result;

        var manifest = result.Manifest;

        lock (_lock)
        {
            _loadedCharacters[manifest.Slug] = manifest;
            _default ??= manifest;
            _current ??= manifest;
        }

        return result;
    }

    public bool SwitchTo(string slug)
    {
        lock (_lock)
        {
            if (!_loadedCharacters.TryGetValue(slug, out var manifest))
                return false;

            _current = manifest;
            return true;
        }
    }

    public IReadOnlyList<CharacterSummary> ListLoaded()
    {
        lock (_lock)
        {
            return _loadedCharacters.Values.Select(m => new CharacterSummary
            {
                Slug = m.Slug,
                Name = m.Name,
                Version = m.Version,
                IsCurrent = _current?.Slug == m.Slug
            }).ToList();
        }
    }

    public CharacterResourceReport GetResourceReport(string characterDirectory)
    {
        var report = new CharacterResourceReport
        {
            CharacterDirectory = characterDirectory
        };

        var result = _loader.LoadFromDirectory(characterDirectory);
        if (!result.Success || result.Manifest == null)
        {
            report.IsComplete = false;
            report.MissingResources.AddRange(result.Errors);
            return report;
        }

        var manifest = result.Manifest;
        var imageBase = Path.Combine(characterDirectory, manifest.Appearance?.ImageFolder ?? "");

        // 检查头像
        var avatarPath = Path.Combine(imageBase, manifest.Appearance?.AvatarImage ?? "avatar.png");
        report.AvatarStatus = File.Exists(avatarPath) ? "存在" : $"缺失: {avatarPath}";

        // 检查各状态图片
        if (manifest.States != null)
        {
            foreach (var (stateName, state) in manifest.States)
            {
                if (string.IsNullOrEmpty(state.Image))
                {
                    report.StateImageStatus[stateName] = "未配置图片";
                    continue;
                }

                var imgPath = Path.Combine(imageBase, state.Image);
                if (File.Exists(imgPath))
                {
                    report.StateImageStatus[stateName] = "存在";
                }
                else
                {
                    report.StateImageStatus[stateName] = $"缺失: {state.Image}";
                    report.MissingResources.Add($"状态 [{stateName}] 图片缺失: {state.Image}");
                }
            }
        }

        // 检查 Petdex 兼容
        if (manifest.Animation?.PetdexCompatibility?.Enabled == true)
        {
            var spritePath = Path.Combine(imageBase, manifest.Animation.PetdexCompatibility.SpriteSheet);
            if (!File.Exists(spritePath))
            {
                report.MissingResources.Add($"Petdex 精灵图缺失: {manifest.Animation.PetdexCompatibility.SpriteSheet}");
            }
        }

        report.Warnings.AddRange(result.Warnings);
        report.IsComplete = report.MissingResources.Count == 0;

        return report;
    }

    public CharacterLoadResult ImportFromPetdex(string petdexDirectory, bool switchTo = true)
    {
        var result = _converter.ImportFromPetdexDirectory(petdexDirectory);
        if (!result.Success || result.Manifest == null)
            return result;

        lock (_lock)
        {
            _loadedCharacters[result.Manifest.Slug] = result.Manifest;

            if (switchTo)
                _current = result.Manifest;

            _default ??= result.Manifest;
        }

        return result;
    }

    public void ResetToDefault()
    {
        lock (_lock)
        {
            _current = _default;
        }
    }

    /// <summary>
    /// 缺图回退 — 检查当前角色指定状态的图片是否存在，不存在则回退到 Idle
    /// </summary>
    public (string resolvedState, string? imagePath) ResolveStateImage(string state, string baseDirectory)
    {
        if (_current == null)
            return (state, null);

        var imageBase = Path.Combine(baseDirectory, _current.Appearance?.ImageFolder ?? "");

        // 尝试当前状态
        if (_current.States.TryGetValue(state, out var stateMapping) && !string.IsNullOrEmpty(stateMapping.Image))
        {
            var imgPath = Path.Combine(imageBase, stateMapping.Image);
            if (File.Exists(imgPath))
                return (state, imgPath);
        }

        // 回退到 FallbackState
        if (_current.States.TryGetValue(state, out var fallback) && !string.IsNullOrEmpty(fallback.FallbackState))
        {
            return ResolveStateImage(fallback.FallbackState, baseDirectory);
        }

        // 回退到 Idle
        if (state != "Idle" && _current.States.TryGetValue("Idle", out var idleState) && !string.IsNullOrEmpty(idleState.Image))
        {
            var idlePath = Path.Combine(imageBase, idleState.Image);
            if (File.Exists(idlePath))
                return ("Idle", idlePath);
        }

        return (state, null);
    }
}
