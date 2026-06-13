using System.Text.Json;

namespace DesktopMascot.UI.Services;

public sealed class JsonWindowPlacementStore : IWindowPlacementStore
{
    private readonly string _filePath;
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true
    };

    public JsonWindowPlacementStore()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        if (string.IsNullOrWhiteSpace(appData))
        {
            appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        }

        _filePath = Path.Combine(appData, "DesktopAIMascot", "config", "window-state.json");
    }

    public WindowPlacementState? Load()
    {
        try
        {
            if (!File.Exists(_filePath))
                return null;

            var json = File.ReadAllText(_filePath);
            return JsonSerializer.Deserialize<WindowPlacementState>(json, SerializerOptions);
        }
        catch
        {
            return null;
        }
    }

    public void Save(WindowPlacementState state)
    {
        try
        {
            var directory = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(state, SerializerOptions);
            File.WriteAllText(_filePath, json);
        }
        catch
        {
            // Position persistence must never prevent the desktop shell from closing.
        }
    }
}
