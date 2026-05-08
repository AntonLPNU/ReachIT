using ReachIT.Domain.Models;

namespace ReachIT.Application.Contracts;

public interface IWorkContextDetectorService
{
    Task<CurrentWorkContext> DetectAsync(ProjectMeta project, CancellationToken cancellationToken = default);
}
