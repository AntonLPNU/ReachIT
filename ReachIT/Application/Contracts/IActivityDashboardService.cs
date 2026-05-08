using ReachIT.Domain.Models;

namespace ReachIT.Application.Contracts;

public interface IActivityDashboardService
{
    Task<ActivityDashboardSnapshot> GetSnapshotAsync(ProjectMeta project, CancellationToken cancellationToken = default);
}
