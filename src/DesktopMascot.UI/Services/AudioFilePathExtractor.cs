using System.Text.RegularExpressions;

namespace DesktopMascot.UI.Services;

public static class AudioFilePathExtractor
{
    private static readonly Regex LocalAudioPathPattern = new(
        @"(?<path>(?:[A-Za-z]:\\|\\\\)[^\r\n""<>|]+?\.(?:mp3|wav|m4a|aac|wma))",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static string? ExtractExistingPath(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        foreach (Match match in LocalAudioPathPattern.Matches(text))
        {
            var path = TrimTrailingPunctuation(match.Groups["path"].Value);
            if (File.Exists(path))
                return path;
        }

        return null;
    }

    private static string TrimTrailingPunctuation(string path) =>
        path.Trim().TrimEnd('.', ',', ';', ':', '，', '。', '；', '：', ')', '）');
}
