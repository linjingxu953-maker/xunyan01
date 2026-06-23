namespace DesktopMascot.UI.Services;

public interface IComputerUseControlService
{
    bool HasActiveSession { get; }
    bool HasPendingApproval { get; }
    bool Pause();
    bool Resume();
    bool Takeover();
    bool ApproveCurrentAction();
    bool DenyCurrentAction(string? reason = null);
}
