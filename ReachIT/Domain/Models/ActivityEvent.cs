using ReachIT.Domain.Enums;

namespace ReachIT.Domain.Models;

public class ActivityEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProjectId { get; set; }
    public Guid? WorkItemId { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public ActivityEventType EventType { get; set; }
    public string AppName { get; set; } = string.Empty;
    public string ProcessName { get; set; } = string.Empty;
    public string WindowTitle { get; set; } = string.Empty;
    public string? FilePath { get; set; }
    public string? FolderPath { get; set; }
    public int? DurationSeconds { get; set; }
    public double? Value { get; set; }
    public string MetadataJson { get; set; } = "{}";
}
