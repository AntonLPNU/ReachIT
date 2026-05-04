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
    public int Priority { get; set; } = 1; // 1: Low, 2: Normal, 3: High
    public DateTime? DueDateUtc { get; set; }
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
