using System.Text;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input.Platform;

namespace DesktopMascot.UI.Services;

public sealed class TaskResultActionService : ITaskResultActionService
{
    public async Task<bool> CopyToClipboardAsync(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return false;

        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop ||
            desktop.MainWindow?.Clipboard is null)
        {
            return false;
        }

        await desktop.MainWindow.Clipboard.SetTextAsync(text);
        return true;
    }

    public async Task<string> SaveResultAsync(string title, string content)
    {
        var baseDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DesktopMascot",
            "TaskResults");

        Directory.CreateDirectory(baseDirectory);

        var fileName = $"{DateTime.Now:yyyyMMdd-HHmmss}-{SanitizeFileName(title)}.md";
        var path = Path.Combine(baseDirectory, fileName);

        await File.WriteAllTextAsync(path, content, Encoding.UTF8);
        return path;
    }

    private static string SanitizeFileName(string value)
    {
        var text = string.IsNullOrWhiteSpace(value) ? "task-result" : value.Trim();

        foreach (var invalidChar in Path.GetInvalidFileNameChars())
        {
            text = text.Replace(invalidChar, '-');
        }

        return text.Length <= 48 ? text : text[..48];
    }
}
