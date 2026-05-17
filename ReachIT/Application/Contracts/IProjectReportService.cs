using ReachIT.Domain.Models;

namespace ReachIT.Application.Contracts;

public interface IProjectReportService
{
    Task<string?> ExportHtmlReportAsync(ProjectMeta project, string outputPath, CancellationToken cancellationToken = default);
}
