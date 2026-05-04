// Provides baseline statistics data for placeholder views.
using ReachIT.Application.Contracts;
using ReachIT.Domain.Models;
using ReachIT.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ReachIT.Application.Services;

public sealed class StatisticsService : IStatisticsService
{
    private readonly IDatabaseService _databaseService;

    public StatisticsService(IDatabaseService databaseService)
    {
        _databaseService = databaseService;
    }

    public async Task<ProjectStats> GetProjectStatsAsync(CancellationToken cancellationToken = default)
    {
        using var db = _databaseService.CreateDbContext();
        var filesCount = await db.ProjectItems.CountAsync(cancellationToken);
        var tasksCount = await db.Tasks.CountAsync(cancellationToken);
        var completedTasks = await db.Tasks.CountAsync(t => t.IsCompleted, cancellationToken);
        var focusSessions = await db.FocusSessions.Where(s => s.EndedAtUtc != null).ToListAsync(cancellationToken);

        double focusHours = focusSessions.Sum(s => (s.EndedAtUtc!.Value - s.StartedAtUtc).TotalHours);

        return new ProjectStats
        {
            TotalFiles = filesCount,
            TotalTasks = tasksCount,
            CompletedTasks = completedTasks,
            FocusHours = focusHours
        };
    }

    public async Task<IReadOnlyList<ProductivityStat>> GetProductivityStatsAsync(CancellationToken cancellationToken = default)
    {
        using var db = _databaseService.CreateDbContext();
        var stats = await db.ProductivityStats.OrderByDescending(p => p.DateUtc).Take(7).ToListAsync(cancellationToken);

        if (!stats.Any())
        {
            return new List<ProductivityStat> 
            {
                new ProductivityStat { DateUtc = DateTime.UtcNow.Date, CompletedTasks = 0, FocusHours = 0 }
            };
        }

        return stats;
    }
}
