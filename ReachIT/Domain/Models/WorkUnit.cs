using ReachIT.Domain.Enums;

namespace ReachIT.Domain.Models;

public class WorkUnit
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProjectId { get; set; }
    public Guid? WorkItemId { get; set; }
    public WorkUnitType Type { get; set; } = WorkUnitType.ManualProgress;
    public double Value { get; set; } = 1;
    public string Source { get; set; } = "manual";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string MetadataJson { get; set; } = "{}";
}
