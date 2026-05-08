// Stores user and workspace settings.
using System.ComponentModel.DataAnnotations.Schema;
using ReachIT.Domain.Enums;

namespace ReachIT.Domain.Models;

public class AppSettings
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public bool ShowRelatedTasksInTree { get; set; }
    public bool HideSidePanelAfterExternalFileOpen { get; set; } = true;
    public FocusModeType DefaultFocusMode { get; set; } = FocusModeType.Strict;
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
    public string AllowedApplicationsSerialized { get; set; } = "ReachIT;explorer;SearchHost;ShellExperienceHost;StartMenuExperienceHost;ApplicationFrameHost;Code;Cursor;devenv;rider64;idea64;pycharm64;webstorm64;clion64;datagrip64;phpstorm64;eclipse;notepad;notepad++;Notepad;WINWORD;EXCEL;POWERPNT;OUTLOOK;OneNote;Acrobat;FoxitPDFEditor;chrome;msedge;firefox;brave;FreeCAD;blender;Blockbench;Aseprite;Resolve;fusion360;acad;SketchUp;3dsmax;Maya;Photoshop;Illustrator;figma;inkscape;gimp;paintdotnet;PaintStudio.View;WindowsTerminal;wt;powershell;cmd;git-bash;putty;winscp;postman;insomnia;docker desktop;Docker Desktop;slack;Teams;Zoom";
    public string FocusDistractingApplicationsSerialized { get; set; } = string.Empty;
    public bool EnableActivityTracking { get; set; } = true;
    public bool TrackActiveWindow { get; set; } = true;
    public bool TrackFileChanges { get; set; } = true;
    public bool TrackGitChanges { get; set; } = true;
    public bool TrackTextStatistics { get; set; } = true;
    public bool IgnorePrivateApps { get; set; } = true;
    public bool PauseActivityTracking { get; set; }
    public string PrivateAppsSerialized { get; set; } = "1password;bitwarden;keepass;authenticator";
    public string IgnoredFoldersSerialized { get; set; } = "bin;obj;.git;.vs;node_modules;packages;build;dist";

    [NotMapped]
    public List<string> AllowedApplications
    {
        get => AllowedApplicationsSerialized
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
        set => AllowedApplicationsSerialized = string.Join(';', value);
    }

    [NotMapped]
    public List<string> PrivateApps
    {
        get => PrivateAppsSerialized
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
        set => PrivateAppsSerialized = string.Join(';', value);
    }

    [NotMapped]
    public List<string> FocusDistractingApplications
    {
        get => FocusDistractingApplicationsSerialized
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
        set => FocusDistractingApplicationsSerialized = string.Join(';', value);
    }

    [NotMapped]
    public List<string> IgnoredFolders
    {
        get => IgnoredFoldersSerialized
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
        set => IgnoredFoldersSerialized = string.Join(';', value);
    }
}
