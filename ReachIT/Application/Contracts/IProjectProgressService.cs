using ReachIT.Domain.Models;

namespace ReachIT.Application.Contracts;

public interface IProjectProgressService
{
    Task<ProjectProgressSnapshot> GetSnapshotAsync(ProjectMeta project, CancellationToken cancellationToken = default);
}
