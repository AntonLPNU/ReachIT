// Describes a task category used for planning and filtering.
namespace ReachIT.Domain.Models;

public class TaskCategory
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "General";
}
