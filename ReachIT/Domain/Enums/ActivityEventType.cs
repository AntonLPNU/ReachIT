namespace ReachIT.Domain.Enums;

public enum ActivityEventType
{
    AppActivated,
    WindowChanged,
    FileCreated,
    FileChanged,
    FileDeleted,
    FileRenamed,
    FolderChanged,
    TextChanged,
    GitChanged,
    FocusStarted,
    FocusStopped,
    AllowedAppUsed,
    DistractingAppUsed,
    IdleStarted,
    IdleEnded,
    ManualAction
}
