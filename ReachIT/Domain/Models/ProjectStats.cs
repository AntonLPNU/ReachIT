// Aggregates summary metrics for the current project.
namespace ReachIT.Domain.Models;

public class ProjectStats
{
    public int TotalFiles { get; set; }
    public int TotalTasks { get; set; }
    public int CompletedTasks { get; set; }
    public double FocusHours { get; set; }
}
