using ReachIT.Application.Contracts;
using ReachIT.Domain.Enums;
using ReachIT.Domain.Models;

namespace ReachIT.Application.Services;

public sealed class ProductivityScoringService : IProductivityScoringService
{
    private readonly IActivityRepository _activityRepository;
    private readonly IWorkUnitRepository _workUnitRepository;

    public ProductivityScoringService(IActivityRepository activityRepository, IWorkUnitRepository workUnitRepository)
    {
        _activityRepository = activityRepository;
        _workUnitRepository = workUnitRepository;
    }

    public async Task<ProductivityScoreSnapshot> ScoreAsync(ProjectMeta project, CancellationToken cancellationToken = default)
    {
        var since = DateTime.UtcNow.Date;
        var events = await _activityRepository.GetSinceAsync(project.Id, since, cancellationToken).ConfigureAwait(false);
        var units = await _workUnitRepository.GetByProjectAsync(project.Id, cancellationToken).ConfigureAwait(false);
        var todayUnits = units.Where(x => x.CreatedAt >= since).ToList();

        var focusMinutes = events
            .Where(x => x.EventType == ActivityEventType.AllowedAppUsed || x.EventType == ActivityEventType.FocusStarted)
            .Sum(x => (x.DurationSeconds ?? 0) / 60d);
        var distractingMinutes = events
            .Where(x => x.EventType == ActivityEventType.DistractingAppUsed)
            .Sum(x => (x.DurationSeconds ?? 0) / 60d);
        var interruptions = events.Count(x => x.EventType == ActivityEventType.DistractingAppUsed);
        var changedFiles = events.Where(x => x.FilePath is not null).Select(x => x.FilePath).Distinct().Count();

        var focusScore = Math.Clamp((int)Math.Round(focusMinutes * 2), 0, 100);
        var distractionScore = Math.Clamp(100 - (int)Math.Round(distractingMinutes * 5) - interruptions * 8, 0, 100);
        var progressScore = Math.Clamp(todayUnits.Count * 8 + changedFiles * 6, 0, 100);
        var total = Math.Clamp((int)Math.Round((focusScore * 0.4) + (distractionScore * 0.3) + (progressScore * 0.3)), 0, 100);

        return new ProductivityScoreSnapshot
        {
            ProductivityScore = total,
            FocusScore = focusScore,
            DistractionScore = distractionScore,
            ProgressScore = progressScore,
            Interruptions = interruptions,
            FocusMinutes = focusMinutes,
            DistractingMinutes = distractingMinutes,
            Explanation = $"Focus {focusMinutes:F0}m, distractions {interruptions}, files changed {changedFiles}, work units {todayUnits.Count}."
        };
    }
}
