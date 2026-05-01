// Represents a single managed task in ReachIT.
using System.ComponentModel.DataAnnotations.Schema;

namespace ReachIT.Domain.Models;

public class TaskItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsCompleted { get; set; }
    public DateTime? DueDateUtc { get; set; }
    public Guid? RelatedProjectTreeNodeId { get; set; }
    public Guid? CategoryId { get; set; }
    public TaskCategory? Category { get; set; }
    public List<TaskTag> Tags { get; set; } = new();

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
