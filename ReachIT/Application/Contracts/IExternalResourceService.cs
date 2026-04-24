// Defines external file/link attachment operations for a project.
using ReachIT.Domain.Models;

namespace ReachIT.Application.Contracts;

public interface IExternalResourceService
{
    Task<IReadOnlyList<ExternalResourceItem>> GetByProjectAsync(Guid projectId, CancellationToken cancellationToken = default);
    Task<ExternalResourceItem> AttachAsync(Guid projectId, string sourcePathOrUrl, CancellationToken cancellationToken = default);
    Task<ExternalResourceItem> CopyIntoProjectAsync(Guid projectId, string sourcePathOrUrl, CancellationToken cancellationToken = default);
    Task<ExternalResourceItem> SaveAsLinkAsync(Guid projectId, string sourcePathOrUrl, CancellationToken cancellationToken = default);
}
