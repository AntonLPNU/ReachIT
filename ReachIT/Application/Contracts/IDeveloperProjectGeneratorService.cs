using ReachIT.Domain.Models;

namespace ReachIT.Application.Contracts;

public interface IDeveloperProjectGeneratorService
{
    Task<ProjectMeta?> GenerateDemoProjectAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>> GenerateRandomFilesAsync(ProjectMeta project, int count, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>> GenerateFormatCatalogAsync(ProjectMeta project, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>> TouchRandomFilesAsync(ProjectMeta project, int count, CancellationToken cancellationToken = default);
    string? PickRandomProjectFile(ProjectMeta project);
}
