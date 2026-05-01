// Builds project explorer tree directly from filesystem and external metadata.
using ReachIT.Domain.Models;

namespace ReachIT.Application.Contracts;

public interface IFileSystemProjectExplorerService
{
    Task<IReadOnlyList<ProjectTreeNode>> BuildTreeAsync(ProjectMeta project, CancellationToken cancellationToken = default);
    Task<ProjectTreeNode?> CreateFileAsync(ProjectMeta project, ProjectTreeNode? selectedNode, CancellationToken cancellationToken = default);
    Task<ProjectTreeNode?> CreateFolderAsync(ProjectMeta project, ProjectTreeNode? selectedNode, CancellationToken cancellationToken = default);
    Task RenameNodeAsync(ProjectMeta project, ProjectTreeNode node, string newName, CancellationToken cancellationToken = default);
    Task DeleteNodeAsync(ProjectMeta project, ProjectTreeNode node, CancellationToken cancellationToken = default);
    void RevealInExplorer(ProjectTreeNode node);
    void OpenWithDefaultApp(ProjectTreeNode node);
}
