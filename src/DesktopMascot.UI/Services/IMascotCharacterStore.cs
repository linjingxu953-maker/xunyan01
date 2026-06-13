namespace DesktopMascot.UI.Services;

public interface IMascotCharacterStore
{
    event EventHandler? ProfileChanged;

    MascotCharacterProfile Load();
    void Save(MascotCharacterProfile profile);
    IReadOnlyList<MascotCharacterProfileEntry> ListProfiles();
    MascotCharacterProfile? LoadProfile(string id);
    MascotCharacterProfileEntry SaveProfile(MascotCharacterProfile profile);
    MascotCharacterProfileEntry SaveProfileAs(MascotCharacterProfile profile, string name);
    bool DeleteProfile(string id);
}
