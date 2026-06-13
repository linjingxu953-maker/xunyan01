using DesktopMascot.Core.Enums;

namespace DesktopMascot.UI.Services;

public interface ICharacterImageService
{
    CharacterImageResult Resolve(MascotCharacterProfile profile, MascotState state);
}
