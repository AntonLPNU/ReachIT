// Stores user and workspace settings.
using System.ComponentModel.DataAnnotations.Schema;
using ReachIT.Domain.Enums;

namespace ReachIT.Domain.Models;

public class AppSettings
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public bool ShowRelatedTasksInTree { get; set; }
    public bool HideSidePanelAfterExternalFileOpen { get; set; } = true;
    public FocusModeType DefaultFocusMode { get; set; } = FocusModeType.Light;
    public string Theme { get; set; } = "Light";
    public string Language { get; set; } = "en";
    public bool EnableNotifications { get; set; } = true;
    public string SidePanelHotkey { get; set; } = "Ctrl+Shift+R";
    public string FloatingLogoHotkey { get; set; } = "Ctrl+Alt+R";
    public string QuickAddTaskHotkey { get; set; } = "Ctrl+Alt+T";
    public string FocusModeHotkey { get; set; } = "Ctrl+Alt+F";
    public string MainWindowHotkey { get; set; } = "Ctrl+Alt+M";
    public bool ShowFloatingLogoOnStartup { get; set; } = true;
    public double FloatingLogoLeft { get; set; } = 24;
    public double FloatingLogoTop { get; set; } = 160;
    public string LastOpenedProjectPath { get; set; } = string.Empty;
    public string BackupLocationPath { get; set; } = string.Empty;
    public string AllowedApplicationsSerialized { get; set; } = string.Empty;

    [NotMapped]
    public List<string> AllowedApplications
    {
        get => AllowedApplicationsSerialized
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
        set => AllowedApplicationsSerialized = string.Join(';', value);
    }
}
