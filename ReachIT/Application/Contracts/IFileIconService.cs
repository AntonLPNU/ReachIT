using ReachIT.Domain.Models;

namespace ReachIT.Application.Contracts;

public interface IFileIconService
{
    Task EnsureCacheAsync(CancellationToken cancellationToken = default);
    string? GetIconPath(ProjectTreeNode node);
}
