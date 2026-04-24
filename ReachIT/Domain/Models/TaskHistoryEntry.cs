// Tracks important task changes for audit-friendly history.
namespace ReachIT.Domain.Models;

public class TaskHistoryEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TaskItemId { get; set; }
    public DateTime ChangedAtUtc { get; set; } = DateTime.UtcNow;
    public string ChangeType { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
}
