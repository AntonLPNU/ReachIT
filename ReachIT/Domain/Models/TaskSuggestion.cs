using ReachIT.Domain.Enums;

namespace ReachIT.Domain.Models;

public class TaskSuggestion
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProjectId { get; set; }
    public string SuggestedTitle { get; set; } = string.Empty;
    public string SuggestedDescription { get; set; } = string.Empty;
    public WorkItemType SuggestedType { get; set; } = WorkItemType.Task;
    public string SuggestedLinkedPath { get; set; } = string.Empty;
    public double Confidence { get; set; }
    public string Reason { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public TaskSuggestionStatus Status { get; set; } = TaskSuggestionStatus.New;
}
