namespace DesktopMascot.UI.Services;

public sealed class UnavailableComputerUseControlService : IComputerUseControlService
{
    public bool HasActiveSession => false;
    public bool HasPendingApproval => false;
    public bool Pause() => false;
    public bool Resume() => false;
    public bool Takeover() => false;
    public bool ApproveCurrentAction() => false;
    public bool DenyCurrentAction(string? reason = null) => false;
}
