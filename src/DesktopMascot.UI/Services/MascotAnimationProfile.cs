using DesktopMascot.Core.Enums;

namespace DesktopMascot.UI.Services;

public enum MascotAnimationMode
{
    Idle,
    Listening,
    Working,
    WaitingApproval,
    Completed,
    Error
}

public sealed record MascotAnimationFrame(
    double ScaleX,
    double ScaleY,
    double OffsetX,
    double OffsetY,
    double RotationDegrees,
    double HaloOpacity,
    double ChipOffsetY);

public sealed record MascotAnimationProfile(
    MascotAnimationMode Mode,
    double Speed,
    double ScaleAmplitude,
    double StretchAmplitude,
    double VerticalLiftAmplitude,
    double HorizontalShakeAmplitude,
    double RotationAmplitude,
    double HaloBaseOpacity,
    double HaloPulseOpacity,
    double ChipLiftAmplitude)
{
    public static MascotAnimationProfile ForState(MascotState state, bool isBusy)
    {
        if (state == MascotState.Error)
        {
            return new MascotAnimationProfile(
                MascotAnimationMode.Error,
                Speed: 18.0,
                ScaleAmplitude: 0.006,
                StretchAmplitude: 0.006,
                VerticalLiftAmplitude: 0.8,
                HorizontalShakeAmplitude: 3.2,
                RotationAmplitude: 1.6,
                HaloBaseOpacity: 0.34,
                HaloPulseOpacity: 0.18,
                ChipLiftAmplitude: 0.8);
        }

        if (state is MascotState.WaitingApproval or MascotState.MemoryConfirm)
        {
            return new MascotAnimationProfile(
                MascotAnimationMode.WaitingApproval,
                Speed: 4.0,
                ScaleAmplitude: 0.018,
                StretchAmplitude: 0.012,
                VerticalLiftAmplitude: 2.6,
                HorizontalShakeAmplitude: 0,
                RotationAmplitude: 1.2,
                HaloBaseOpacity: 0.32,
                HaloPulseOpacity: 0.24,
                ChipLiftAmplitude: 2.0);
        }

        if (state == MascotState.Completed)
        {
            return new MascotAnimationProfile(
                MascotAnimationMode.Completed,
                Speed: 4.8,
                ScaleAmplitude: 0.026,
                StretchAmplitude: 0.014,
                VerticalLiftAmplitude: 3.4,
                HorizontalShakeAmplitude: 0,
                RotationAmplitude: 1.4,
                HaloBaseOpacity: 0.28,
                HaloPulseOpacity: 0.30,
                ChipLiftAmplitude: 2.4);
        }

        if (isBusy || state is MascotState.Understanding
                or MascotState.ReadingContext
                or MascotState.Planning
                or MascotState.Working
                or MascotState.Reporting)
        {
            return new MascotAnimationProfile(
                MascotAnimationMode.Working,
                Speed: 5.2,
                ScaleAmplitude: 0.026,
                StretchAmplitude: 0.014,
                VerticalLiftAmplitude: 4.2,
                HorizontalShakeAmplitude: 0,
                RotationAmplitude: 0.5,
                HaloBaseOpacity: 0.36,
                HaloPulseOpacity: 0.30,
                ChipLiftAmplitude: 1.4);
        }

        if (state == MascotState.Listening)
        {
            return new MascotAnimationProfile(
                MascotAnimationMode.Listening,
                Speed: 3.2,
                ScaleAmplitude: 0.016,
                StretchAmplitude: 0.01,
                VerticalLiftAmplitude: 2.2,
                HorizontalShakeAmplitude: 0,
                RotationAmplitude: 0.6,
                HaloBaseOpacity: 0.25,
                HaloPulseOpacity: 0.18,
                ChipLiftAmplitude: 1.0);
        }

        return new MascotAnimationProfile(
            MascotAnimationMode.Idle,
            Speed: 2.4,
            ScaleAmplitude: 0.012,
            StretchAmplitude: 0.008,
            VerticalLiftAmplitude: 1.8,
            HorizontalShakeAmplitude: 0,
            RotationAmplitude: 0.25,
            HaloBaseOpacity: 0.20,
            HaloPulseOpacity: 0.12,
            ChipLiftAmplitude: 0.6);
    }

    public MascotAnimationFrame Evaluate(double seconds)
    {
        var wave = Math.Sin(seconds * Speed);
        var slowWave = Math.Sin(seconds * Speed * 0.5);
        var pulse = Math.Abs(wave);
        var offsetX = HorizontalShakeAmplitude == 0 ? 0 : wave * HorizontalShakeAmplitude;
        var rotation = Mode == MascotAnimationMode.Error
            ? wave * RotationAmplitude
            : slowWave * RotationAmplitude;

        return new MascotAnimationFrame(
            ScaleX: 1 + wave * ScaleAmplitude,
            ScaleY: 1 + slowWave * StretchAmplitude,
            OffsetX: offsetX,
            OffsetY: -pulse * VerticalLiftAmplitude,
            RotationDegrees: rotation,
            HaloOpacity: Math.Clamp(HaloBaseOpacity + pulse * HaloPulseOpacity, 0, 1),
            ChipOffsetY: -pulse * ChipLiftAmplitude);
    }
}
