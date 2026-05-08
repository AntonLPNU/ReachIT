using ReachIT.Application.Contracts;
using ReachIT.Domain.Enums;
using ReachIT.Domain.Models;

namespace ReachIT.Application.Services;

public sealed class ActivityDashboardService : IActivityDashboardService
{
    private readonly IActivityRepository _activityRepository;
    private readonly IWorkContextDetectorService _contextDetectorService;
    private readonly IProductivityScoringService _productivityScoringService;
    private readonly ITaskSuggestionService _taskSuggestionService;

    public ActivityDashboardService(
        IActivityRepository activityRepository,
        IWorkContextDetectorService contextDetectorService,
        IProductivityScoringService productivityScoringService,
        ITaskSuggestionService taskSuggestionService)
    {
        _activityRepository = activityRepository;
        _contextDetectorService = contextDetectorService;
        _productivityScoringService = productivityScoringService;
        _taskSuggestionService = taskSuggestionService;
    }

    public async Task<ActivityDashboardSnapshot> GetSnapshotAsync(ProjectMeta project, CancellationToken cancellationToken = default)
    {
        var since = DateTime.UtcNow.Date;
        var today = await _activityRepository.GetSinceAsync(project.Id, since, cancellationToken).ConfigureAwait(false);
        var recent = await _activityRepository.GetRecentAsync(project.Id, 80, cancellationToken).ConfigureAwait(false);
        var suggestions = await _taskSuggestionService.GetNewSuggestionsAsync(project.Id, cancellationToken).ConfigureAwait(false);

        return new ActivityDashboardSnapshot
        {
            CurrentContext = await _contextDetectorService.DetectAsync(project, cancellationToken).ConfigureAwait(false),
            Productivity = await _productivityScoringService.ScoreAsync(project, cancellationToken).ConfigureAwait(false),
            RecentEvents = recent,
            SuggestedTaskLinks = suggestions.Where(x => x.Reason.Contains("Detected work", StringComparison.OrdinalIgnoreCase)).Take(8).ToList(),
            SuggestedCompletedTasks = suggestions.Where(x => x.Reason.Contains("may be completed", StringComparison.OrdinalIgnoreCase)).Take(8).ToList(),
            FilesChangedToday = today.Where(x => x.FilePath is not null).Select(x => x.FilePath).Distinct().Count(),
            InterruptionsToday = today.Count(x => x.EventType == ActivityEventType.DistractingAppUsed),
            FocusMinutesToday = today.Where(x => x.EventType == ActivityEventType.AllowedAppUsed).Sum(x => (x.DurationSeconds ?? 0) / 60d)
        };
    }
}
