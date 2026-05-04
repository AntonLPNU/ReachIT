using ReachIT.Domain.Models;

namespace ReachIT.Application.Contracts;

public interface IWorkItemService
{
    Task<IReadOnlyList<WorkItem>> GetProjectWorkItemsAsync(Guid projectId, CancellationToken cancellationToken = default);
    Task<WorkItem> CreateAsync(WorkItem item, CancellationToken cancellationToken = default);
    Task SyncLegacyTasksAsync(ProjectMeta project, CancellationToken cancellationToken = default);
}
