// Stores user and workspace settings.
using System.ComponentModel.DataAnnotations.Schema;
using ReachIT.Domain.Enums;

namespace ReachIT.Domain.Models;

public class AppSettings
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public bool ShowRelatedTasksInTree { get; set; }
    public FocusModeType DefaultFocusMode { get; set; } = FocusModeType.Light;
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
