namespace ReachIT.Domain.Models;

public sealed record ForegroundWindowSnapshot(
    string AppName,
    string ProcessName,
    string WindowTitle,
    int ProcessId,
    string ExecutablePath = "");
