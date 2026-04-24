// Provides baseline statistics data for placeholder views.
using ReachIT.Application.Contracts;
using ReachIT.Domain.Models;

namespace ReachIT.Application.Services;

public sealed class StatisticsService : IStatisticsService
{
    public Task<ProjectStats> GetProjectStatsAsync(CancellationToken cancellationToken = default)
    {
        var stats = new ProjectStats
        {
            TotalFiles = 0,
            TotalTasks = 2,
            CompletedTasks = 0,
            FocusHours = 0
        };

        return Task.FromResult(stats);
    }

    public Task<IReadOnlyList<ProductivityStat>> GetProductivityStatsAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<ProductivityStat> list =
        [
            new ProductivityStat { DateUtc = DateTime.UtcNow.Date, CompletedTasks = 0, FocusHours = 0 }
        ];

        return Task.FromResult(list);
    }
}
