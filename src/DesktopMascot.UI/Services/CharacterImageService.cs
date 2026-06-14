using Avalonia.Media.Imaging;
using DesktopMascot.Core.Enums;

namespace DesktopMascot.UI.Services;

public sealed class CharacterImageService : ICharacterImageService
{
    private static readonly IReadOnlyDictionary<MascotState, string[]> StateImageAliases = new Dictionary<MascotState, string[]>
    {
        [MascotState.Idle] = ["idle.png", "avatar.png", "pose.png", "站立.png"],
        [MascotState.Listening] = ["listening.png", "Listening：看向用户.png", "Listening:看向用户.png"],
        [MascotState.Understanding] = ["thinking.png", "understanding.png", "Understanding：思考图.png", "Understanding:思考图.png"],
        [MascotState.ReadingContext] = ["reading.png", "understanding.png", "Understanding：思考图.png", "Understanding:思考图.png"],
        [MascotState.Planning] = ["planning.png", "thinking.png", "Understanding：思考图.png", "Understanding:思考图.png"],
        [MascotState.WaitingApproval] = ["waiting.png", "WaitingApproval：提醒、举手图.png", "WaitingApproval:提醒、举手图.png"],
        [MascotState.Working] = ["working.png", "Working：忙碌图.png", "Working:忙碌图.png"],
        [MascotState.MemoryConfirm] = ["memory.png", "WaitingApproval：提醒、举手图.png", "WaitingApproval:提醒、举手图.png"],
        [MascotState.Reporting] = ["reporting.png", "working.png", "Working：忙碌图.png", "Working:忙碌图.png"],
        [MascotState.Completed] = ["completed.png", "Completed：开心、完成图.png", "Completed:开心、完成图.png"],
        [MascotState.Error] = ["error.png", "Error：困惑、错误图.png", "Error:困惑、错误图.png"]
    };

    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png",
        ".jpg",
        ".jpeg",
        ".bmp",
        ".webp"
    };

    public CharacterImageResult Resolve(MascotCharacterProfile profile, MascotState state)
    {
        profile.EnsureImageDefaults();

        var folder = ResolveFolder(profile.ImageFolder);
        if (folder is null)
        {
            return new CharacterImageResult
            {
                Message = "未找到角色图片目录，已使用文字头像。"
            };
        }

        var stateFile = ResolveStateFile(profile, state);
        var imagePath = ResolveImagePath(folder, stateFile)
            ?? ResolveAliasImagePath(folder, state)
            ?? ResolveImagePath(folder, profile.AvatarImage)
            ?? ResolveAliasImagePath(folder, MascotState.Idle);

        if (imagePath is null)
        {
            return new CharacterImageResult
            {
                Message = "当前角色没有可用图片，已使用文字头像。"
            };
        }

        try
        {
            using var stream = File.OpenRead(imagePath);
            return new CharacterImageResult
            {
                Image = new Bitmap(stream),
                FilePath = imagePath,
                Message = $"正在显示 {Path.GetFileName(imagePath)}"
            };
        }
        catch (Exception ex)
        {
            return new CharacterImageResult
            {
                FilePath = imagePath,
                Message = $"图片加载失败：{ex.Message}"
            };
        }
    }

    private static string? ResolveStateFile(MascotCharacterProfile profile, MascotState state)
    {
        var key = state.ToString();
        return profile.StateImages.TryGetValue(key, out var imageFile)
            ? imageFile
            : null;
    }

    private static string? ResolveFolder(string folder)
    {
        if (string.IsNullOrWhiteSpace(folder))
            return null;

        if (Path.IsPathRooted(folder))
        {
            return Directory.Exists(folder) ? folder : null;
        }

        foreach (var root in EnumerateCandidateRoots())
        {
            var normalizedFolder = folder.Replace('/', Path.DirectorySeparatorChar);
            var direct = Path.GetFullPath(Path.Combine(root, normalizedFolder));
            if (Directory.Exists(direct))
                return direct;

            var underCharacters = Path.GetFullPath(Path.Combine(root, "assets", "characters", normalizedFolder));
            if (Directory.Exists(underCharacters))
                return underCharacters;
        }

        return null;
    }

    private static string? ResolveImagePath(string folder, string? imageFile)
    {
        if (string.IsNullOrWhiteSpace(imageFile))
            return null;

        var candidate = Path.IsPathRooted(imageFile)
            ? imageFile
            : Path.Combine(folder, imageFile.Replace('/', Path.DirectorySeparatorChar));

        if (!File.Exists(candidate))
            return null;

        return SupportedExtensions.Contains(Path.GetExtension(candidate))
            ? candidate
            : null;
    }

    private static string? ResolveAliasImagePath(string folder, MascotState state)
    {
        if (!StateImageAliases.TryGetValue(state, out var aliases))
            return null;

        foreach (var alias in aliases)
        {
            var resolved = ResolveImagePath(folder, alias);
            if (resolved is not null)
                return resolved;
        }

        var prefix = state.ToString();
        return Directory.EnumerateFiles(folder)
            .Where(file => SupportedExtensions.Contains(Path.GetExtension(file)))
            .FirstOrDefault(file =>
            {
                var name = Path.GetFileNameWithoutExtension(file);
                return name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
            });
    }

    private static IEnumerable<string> EnumerateCandidateRoots()
    {
        yield return Environment.CurrentDirectory;

        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            yield return directory.FullName;
            directory = directory.Parent;
        }
    }
}
