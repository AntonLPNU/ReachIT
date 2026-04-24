// Builds project explorer tree directly from filesystem and external metadata.
using ReachIT.Domain.Models;

namespace ReachIT.Application.Contracts;

public interface IFileSystemProjectExplorerService
{
    Task<IReadOnlyList<ProjectTreeNode>> BuildTreeAsync(ProjectMeta project, CancellationToken cancellationToken = default);
    Task<ProjectTreeNode?> CreateFileAsync(ProjectMeta project, ProjectTreeNode? selectedNode, CancellationToken cancellationToken = default);
    Task<ProjectTreeNode?> CreateFolderAsync(ProjectMeta project, ProjectTreeNode? selectedNode, CancellationToken cancellationToken = default);
}
