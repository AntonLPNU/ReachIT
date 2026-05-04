namespace ReachIT.Domain.Models;

public sealed class ProjectProgressSnapshot
{
    public Guid ProjectId { get; init; }
    public double ProjectProgressPercent { get; init; }
    public Milestone? ActiveMilestone { get; init; }
    public int TasksDone { get; init; }
    public int TasksAll { get; init; }
    public int WorkItemsInProgress { get; init; }
    public int FilesChangedToday { get; init; }
    public double FocusMinutesToday { get; init; }
    public IReadOnlyList<TaskSuggestion> SuggestedTasks { get; init; } = [];
    public IReadOnlyList<WorkItem> StaleTasks { get; init; } = [];
    public IReadOnlyList<WorkItem> RecentlyCompletedTasks { get; init; } = [];
    public IReadOnlyList<WorkItem> FilesWithActivityWithoutTasks { get; init; } = [];
    public IReadOnlyList<WorkItem> PossiblyCompletedTasks { get; init; } = [];
    public string CurrentWorkContext { get; init; } = string.Empty;
}
