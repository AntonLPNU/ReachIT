using ReachIT.Domain.Models;

namespace ReachIT.Application.Contracts;

public interface IWorkItemRepository
{
    Task<IReadOnlyList<WorkItem>> GetByProjectAsync(Guid projectId, CancellationToken cancellationToken = default);
    Task<WorkItem?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<WorkItem?> GetByLinkedPathAsync(Guid projectId, string linkedPath, CancellationToken cancellationToken = default);
    Task AddAsync(WorkItem item, CancellationToken cancellationToken = default);
    Task UpdateAsync(WorkItem item, CancellationToken cancellationToken = default);
}
