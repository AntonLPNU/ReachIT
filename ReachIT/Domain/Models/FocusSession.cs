// Captures a focus mode session lifecycle.
using System.ComponentModel.DataAnnotations.Schema;
using ReachIT.Domain.Enums;

namespace ReachIT.Domain.Models;

public class FocusSession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public FocusModeType ModeType { get; set; } = FocusModeType.Light;
    public DateTime StartedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? EndedAtUtc { get; set; }
    public bool IsActive { get; set; }
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
