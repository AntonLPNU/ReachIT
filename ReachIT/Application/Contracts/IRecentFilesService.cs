// Defines recent projects and recent external files operations.
using ReachIT.Domain.Models;

namespace ReachIT.Application.Contracts;

public interface IRecentFilesService
{
    Task<IReadOnlyList<ProjectMeta>> GetRecentProjectsAsync(CancellationToken cancellationToken = default);
    Task AddRecentProjectAsync(ProjectMeta project, CancellationToken cancellationToken = default);
    Task RemoveRecentProjectAsync(Guid projectId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RecentExternalFileItem>> GetRecentExternalFilesAsync(CancellationToken cancellationToken = default);
    Task AddRecentExternalFileAsync(RecentExternalFileItem item, CancellationToken cancellationToken = default);
    Task RemoveRecentExternalFileAsync(Guid id, CancellationToken cancellationToken = default);
}
