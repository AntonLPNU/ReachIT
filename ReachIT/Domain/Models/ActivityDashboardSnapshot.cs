namespace ReachIT.Domain.Models;

public sealed class ActivityDashboardSnapshot
{
    public CurrentWorkContext CurrentContext { get; set; } = new();
    public ProductivityScoreSnapshot Productivity { get; set; } = new();
    public IReadOnlyList<ActivityEvent> RecentEvents { get; set; } = [];
    public IReadOnlyList<TaskSuggestion> SuggestedTaskLinks { get; set; } = [];
    public IReadOnlyList<TaskSuggestion> SuggestedCompletedTasks { get; set; } = [];
    public int FilesChangedToday { get; set; }
    public int InterruptionsToday { get; set; }
    public double FocusMinutesToday { get; set; }
}
