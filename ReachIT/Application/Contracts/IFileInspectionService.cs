using ReachIT.Domain.Models;

namespace ReachIT.Application.Contracts;

public interface IFileInspectionService
{
    Task<FileInspectionResult> InspectAsync(string fullPath, CancellationToken cancellationToken = default);
}
