namespace DesktopMascot.UI.Services;

public interface ITaskResultActionService
{
    Task<bool> CopyToClipboardAsync(string text);
    Task<string> SaveResultAsync(string title, string content);
}
