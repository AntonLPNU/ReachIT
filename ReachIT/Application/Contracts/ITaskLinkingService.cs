using ReachIT.Domain.Models;

namespace ReachIT.Application.Contracts;

public interface ITaskLinkingService
{
    Task LinkToPathAsync(Guid workItemId, string path, CancellationToken cancellationToken = default);
    Task LinkToAppAsync(Guid workItemId, string appName, CancellationToken cancellationToken = default);
    Task LinkToMilestoneAsync(Guid workItemId, Guid milestoneId, CancellationToken cancellationToken = default);
}
