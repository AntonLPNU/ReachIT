// Defines project lifecycle and .rit workspace operations.
using ReachIT.Domain.Models;

namespace ReachIT.Application.Contracts;

public interface IProjectService
{
    Task<ProjectMeta> CreateProjectAsync(CreateProjectRequest request, CancellationToken cancellationToken = default);
    Task<ProjectMeta?> OpenProjectFromDialogAsync(CancellationToken cancellationToken = default);
    Task<ProjectMeta?> OpenProjectAsync(string projectFolderPath, CancellationToken cancellationToken = default);
    Task SaveProjectAsync(CancellationToken cancellationToken = default);
    Task SaveAllAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProjectTreeNode>> GetCurrentTreeAsync(CancellationToken cancellationToken = default);
    Task<ProjectTreeNode?> CreateInternalFileAsync(ProjectTreeNode? selectedNode, CancellationToken cancellationToken = default);
    Task<ProjectTreeNode?> CreateFolderAsync(ProjectTreeNode? selectedNode, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProjectMeta>> GetRecentProjectsAsync(CancellationToken cancellationToken = default);
    ProjectMeta? CurrentProject { get; }
}
