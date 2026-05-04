using System.ComponentModel.DataAnnotations.Schema;
using ReachIT.Domain.Enums;

namespace ReachIT.Domain.Models;

public class WorkItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProjectId { get; set; }
    public Guid? ParentId { get; set; }
    public WorkItem? Parent { get; set; }
    public List<WorkItem> Children { get; set; } = [];
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public WorkItemType Type { get; set; } = WorkItemType.Task;
    public WorkItemStatus Status { get; set; } = WorkItemStatus.Backlog;
    public int Priority { get; set; } = 2;
    public double ProgressPercent { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime? Deadline { get; set; }
    public double EstimatedWorkUnits { get; set; } = 1;
    public double CompletedWorkUnits { get; set; }
    public string LinkedPath { get; set; } = string.Empty;
    public string LinkedApp { get; set; } = string.Empty;
    public string Tags { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public Guid? MilestoneId { get; set; }
    public Guid? LegacyTaskItemId { get; set; }

    [NotMapped]
    public bool IsDone => Status is WorkItemStatus.Done or WorkItemStatus.Cancelled;
}
