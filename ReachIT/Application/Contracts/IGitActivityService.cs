using ReachIT.Domain.Models;

namespace ReachIT.Application.Contracts;

public interface IGitActivityService
{
    Task<IReadOnlyList<ActivityEvent>> ScanAsync(ProjectMeta project, CancellationToken cancellationToken = default);
}
