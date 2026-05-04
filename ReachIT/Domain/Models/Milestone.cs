using ReachIT.Domain.Enums;

namespace ReachIT.Domain.Models;

public class Milestone
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProjectId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? Deadline { get; set; }
    public WorkItemStatus Status { get; set; } = WorkItemStatus.Planned;
    public double ProgressPercent { get; set; }
}
