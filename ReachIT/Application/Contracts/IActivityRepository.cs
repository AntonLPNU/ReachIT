using ReachIT.Domain.Enums;
using ReachIT.Domain.Models;

namespace ReachIT.Application.Contracts;

public interface IActivityRepository
{
    Task AddAsync(ActivityEvent activityEvent, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ActivityEvent>> GetRecentAsync(Guid projectId, int take = 100, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ActivityEvent>> GetSinceAsync(Guid projectId, DateTime sinceUtc, CancellationToken cancellationToken = default);
    Task<ActivityEvent?> GetLatestAsync(Guid projectId, ActivityEventType? eventType = null, CancellationToken cancellationToken = default);
    Task ClearProjectAsync(Guid projectId, CancellationToken cancellationToken = default);
}
