using DesktopMascot.Core.Enums;
using DesktopMascot.UI.Services;

namespace DesktopMascot.UI.Tests;

public sealed class MascotAnimationProfileTests
{
    [Fact]
    public void ForState_UsesIdleBreathingForIdleState()
    {
        var profile = MascotAnimationProfile.ForState(MascotState.Idle, isBusy: false);

        Assert.Equal(MascotAnimationMode.Idle, profile.Mode);
        Assert.InRange(profile.Speed, 1.8, 2.8);
        Assert.InRange(profile.ScaleAmplitude, 0.008, 0.02);
        Assert.Equal(0, profile.HorizontalShakeAmplitude);
    }

    [Fact]
    public void ForState_UsesWorkingPulseForBusyStates()
    {
        var profile = MascotAnimationProfile.ForState(MascotState.Working, isBusy: false);

        Assert.Equal(MascotAnimationMode.Working, profile.Mode);
        Assert.True(profile.VerticalLiftAmplitude > 3);
        Assert.True(profile.HaloPulseOpacity > 0.24);
    }

    [Fact]
    public void ForState_IsBusyOverridesIdleState()
    {
        var profile = MascotAnimationProfile.ForState(MascotState.Idle, isBusy: true);

        Assert.Equal(MascotAnimationMode.Working, profile.Mode);
    }

    [Fact]
    public void ForState_UsesWaitingReminderForApprovalStates()
    {
        var profile = MascotAnimationProfile.ForState(MascotState.WaitingApproval, isBusy: false);

        Assert.Equal(MascotAnimationMode.WaitingApproval, profile.Mode);
        Assert.True(profile.RotationAmplitude > 0);
        Assert.True(profile.ChipLiftAmplitude > 0);
    }

    [Fact]
    public void ForState_UsesErrorShakeForErrorState()
    {
        var profile = MascotAnimationProfile.ForState(MascotState.Error, isBusy: false);

        Assert.Equal(MascotAnimationMode.Error, profile.Mode);
        Assert.True(profile.HorizontalShakeAmplitude > 2);
        Assert.True(profile.RotationAmplitude > 0);
    }

    [Fact]
    public void Evaluate_ReturnsDeterministicFrameForProfile()
    {
        var profile = MascotAnimationProfile.ForState(MascotState.Completed, isBusy: false);

        var first = profile.Evaluate(0.25);
        var second = profile.Evaluate(0.25);

        Assert.Equal(first.ScaleX, second.ScaleX);
        Assert.Equal(first.OffsetY, second.OffsetY);
        Assert.Equal(first.HaloOpacity, second.HaloOpacity);
    }
}
