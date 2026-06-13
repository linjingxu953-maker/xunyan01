namespace DesktopMascot.UI.Services;

public interface IOnboardingWindowService
{
    Task ShowOnboardingWindowAsync(CancellationToken ct = default);
}
