namespace ReachIT.Application.Contracts;

public interface IGlobalHotkeyService : IDisposable
{
    event EventHandler? ToggleFloatingRequested;
    event EventHandler? QuickAddTaskRequested;
    event EventHandler? ToggleFocusRequested;
    event EventHandler? OpenMainWindowRequested;

    void Register();
    void Unregister();
}
