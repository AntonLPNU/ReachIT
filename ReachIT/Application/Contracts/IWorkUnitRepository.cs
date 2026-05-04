using ReachIT.Domain.Models;

namespace ReachIT.Application.Contracts;

public interface IWorkUnitRepository
{
    Task<IReadOnlyList<WorkUnit>> GetByProjectAsync(Guid projectId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<WorkUnit>> GetByWorkItemAsync(Guid workItemId, CancellationToken cancellationToken = default);
    Task AddAsync(WorkUnit unit, CancellationToken cancellationToken = default);
}
