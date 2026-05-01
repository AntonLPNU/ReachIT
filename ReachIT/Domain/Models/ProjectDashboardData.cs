// Aggregates dashboard data for the currently opened project.
namespace ReachIT.Domain.Models;

public sealed class ProjectDashboardData
{
    public ProjectMeta? Project { get; set; }
    public ProjectStats Stats { get; set; } = new();
    public List<TaskItem> Tasks { get; set; } = [];
    public List<ProductivityStat> Productivity { get; set; } = [];
    public FocusSession? FocusSession { get; set; }
    public List<string> AllowedApplications { get; set; } = [];
    public List<ExternalResourceItem> ExternalResources { get; set; } = [];
    public List<string> ProjectItems { get; set; } = [];
    public List<ProjectActivityEntry> RecentActivity { get; set; } = [];
    public DateTime LoadedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class ProjectActivityEntry
{
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
    public string Category { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}
