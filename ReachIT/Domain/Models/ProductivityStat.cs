// Represents productivity metrics for dashboard/statistics sections.
namespace ReachIT.Domain.Models;

public class ProductivityStat
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime DateUtc { get; set; } = DateTime.UtcNow.Date;
    public int CompletedTasks { get; set; }
    public double FocusHours { get; set; }
}
