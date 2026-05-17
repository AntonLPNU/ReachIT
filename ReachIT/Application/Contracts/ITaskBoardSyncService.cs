using ReachIT.Domain.Models;

namespace ReachIT.Application.Contracts;

public interface ITaskBoardSyncService
{
    Task<string?> ExportCurrentProjectAsync(CancellationToken cancellationToken = default);
    Task<string?> ExportAsync(ProjectMeta project, CancellationToken cancellationToken = default);
}
