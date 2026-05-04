namespace ReachIT.Application.Contracts;

public interface IWindowManagerService
{
    Task StartAsync(string? projectFolderPath = null, CancellationToken cancellationToken = default);
    void ToggleFloatingLogo();
    void ShowFloatingLogo();
    void HideFloatingLogo();
    void ToggleQuickMenu();
    void OpenQuickAddTask();
    void OpenProjectExplorer();
    void OpenMainWindow();
    void OpenSettings();
    void OpenStatistics();
    void ToggleFocusMode();
    void ExitApplication();
}
