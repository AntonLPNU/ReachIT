// Defines access to project statistics and productivity metrics.
using ReachIT.Domain.Models;

namespace ReachIT.Application.Contracts;

public interface IStatisticsService
{
    Task<ProjectStats> GetProjectStatsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProductivityStat>> GetProductivityStatsAsync(CancellationToken cancellationToken = default);
}
