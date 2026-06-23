using DesktopMascot.Core.Enums;
using DesktopMascot.UI.Services;

namespace DesktopMascot.UI.Tests;

public sealed class CharacterImageServiceTests
{
    [Fact]
    public void Resolve_DefaultFengLinYuRenIdleState_LoadsStateImage()
    {
        var service = new CharacterImageService();
        var profile = new MascotCharacterProfile();

        var result = service.Resolve(profile, MascotState.Idle);

        Assert.NotNull(result.FilePath);
        Assert.EndsWith(
            Path.Combine("assets", "characters", "feng lin yu ren", "idle.png"),
            result.FilePath!.Replace('/', Path.DirectorySeparatorChar),
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Resolve_UsesKnownStateAliasWhenConfiguredFileIsMissing()
    {
        var service = new CharacterImageService();
        var profile = new MascotCharacterProfile
        {
            ImageFolder = "assets/characters/feng lin yu ren",
            AvatarImage = "avatar.png",
            StateImages = new Dictionary<string, string>
            {
                ["Working"] = "missing-working.png"
            }
        };

        var result = service.Resolve(profile, MascotState.Working);

        Assert.NotNull(result.FilePath);
        Assert.EndsWith(
            Path.Combine("assets", "characters", "feng lin yu ren", "working.png"),
            result.FilePath!.Replace('/', Path.DirectorySeparatorChar),
            StringComparison.OrdinalIgnoreCase);
    }
}
