using DesktopMascot.Agent.Engines;
using DesktopMascot.UI.Services;

namespace DesktopMascot.App.Services;

public sealed class ComputerUseControlService : IComputerUseControlService
{
    private readonly object _sync = new();
    private ComputerUseOrchestrator? _current;

    public bool HasActiveSession => Current?.Session.IsActive == true;
    public bool HasPendingApproval => Current?.HasPendingApproval == true;

    public void Attach(ComputerUseOrchestrator orchestrator)
    {
        lock (_sync)
        {
            _current = orchestrator;
        }
    }

    public bool Pause()
    {
        var current = Current;
        if (current?.Session.IsActive != true)
            return false;

        current.Pause();
        return true;
    }

    public bool Resume()
    {
        var current = Current;
        if (current?.Session.IsActive != true)
            return false;

        current.Resume();
        return true;
    }

    public bool Takeover()
    {
        var current = Current;
        if (current is null || (!current.Session.IsActive && !current.HasPendingApproval))
            return false;

        current.Takeover();
        return true;
    }

    public bool ApproveCurrentAction() => Current?.ApproveCurrentAction() == true;

    public bool DenyCurrentAction(string? reason = null) =>
        Current?.DenyCurrentAction(reason) == true;

    private ComputerUseOrchestrator? Current
    {
        get
        {
            lock (_sync)
            {
                return _current;
            }
        }
    }
}
