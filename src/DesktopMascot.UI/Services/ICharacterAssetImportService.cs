namespace DesktopMascot.UI.Services;

public interface ICharacterAssetImportService
{
    CharacterAssetImportResult ImportToAppData(MascotCharacterProfile profile);
}
