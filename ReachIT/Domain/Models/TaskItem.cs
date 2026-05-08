// Represents a single managed task in ReachIT.
using System.ComponentModel.DataAnnotations.Schema;

namespace ReachIT.Domain.Models;

public class TaskItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsCompleted { get; set; }
    public string Status { get; set; } = "To Do";
    public int Priority { get; set; } = 1; // Queue order: lower numbers are handled first.
    public DateTime? DueDateUtc { get; set; }
    public DateTime? StartedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public Guid? ParentTaskId { get; set; }
    public TaskItem? ParentTask { get; set; }
    public List<TaskItem> Subtasks { get; set; } = new();
    public Guid? RelatedProjectTreeNodeId { get; set; }
    public string? AttachedFilePath { get; set; }
    public Guid? CategoryId { get; set; }
    public TaskCategory? Category { get; set; }
    public List<TaskTag> Tags { get; set; } = new();

    [NotMapped]
    public bool HasUnsavedChanges { get; set; }

    [NotMapped]
    public string DisplayTitle { get; set; } = string.Empty;

    [NotMapped]
    public int ProgressPercent => IsCompleted ? 100 : 0;

    [NotMapped]
    public string QueuePositionText => $"#{Priority}";

    [NotMapped]
    public string CompletionAuditText
    {
        get
        {
            if (!IsCompleted)
            {
                return StartedAtUtc.HasValue
                    ? $"Started {StartedAtUtc.Value.ToLocalTime():yyyy-MM-dd HH:mm}"
                    : "Waiting in queue";
            }

            return CompletedAtUtc.HasValue
                ? $"Closed {CompletedAtUtc.Value.ToLocalTime():yyyy-MM-dd HH:mm}"
                : "Closed";
        }
    }

    [NotMapped]
    public string DeadlineStatus
    {
        get
        {
            if (IsCompleted)
            {
                return "Completed";
            }

            if (!DueDateUtc.HasValue)
            {
                return "No deadline";
            }

            var localDue = DueDateUtc.Value.ToLocalTime();
            if (localDue < DateTime.Now)
            {
                return "Overdue";
            }

            if (localDue.Date == DateTime.Now.Date)
            {
                return "Today";
            }

            return "Planned";
        }
    }
}
