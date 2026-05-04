namespace ReachIT.Application.Contracts;

public interface ITrayIconService : IDisposable
{
    event EventHandler? ShowHideFloatingRequested;
    event EventHandler? OpenMainWindowRequested;
    event EventHandler? QuickAddTaskRequested;
    event EventHandler? ExitRequested;

    void Initialize();
}
