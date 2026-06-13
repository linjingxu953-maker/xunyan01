namespace DesktopMascot.UI.Services;

public interface IWindowPlacementStore
{
    WindowPlacementState? Load();
    void Save(WindowPlacementState state);
}
