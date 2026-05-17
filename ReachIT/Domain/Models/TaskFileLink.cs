namespace ReachIT.Domain.Models;

public sealed class TaskFileLink
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProjectId { get; set; }
    public Guid TaskItemId { get; set; }
    public string FilePath { get; set; } = string.Empty;
    public bool IsDirectory { get; set; }
    public DateTime LinkedAtUtc { get; set; } = DateTime.UtcNow;
    public string LinkSource { get; set; } = "Manual";
    public TaskItem? TaskItem { get; set; }
}
