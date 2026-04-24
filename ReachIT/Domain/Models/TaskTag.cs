// Describes tags that can be attached to tasks.
namespace ReachIT.Domain.Models;

public class TaskTag
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TaskItemId { get; set; }
    public string Value { get; set; } = string.Empty;
}
