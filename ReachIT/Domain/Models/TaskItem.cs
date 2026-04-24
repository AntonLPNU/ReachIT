// Represents a single managed task in ReachIT.
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
}
