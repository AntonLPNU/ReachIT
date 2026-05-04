using ReachIT.Domain.Enums;
using ReachIT.Domain.Models;

namespace ReachIT.Application.Contracts;

public interface IWorkUnitService
{
    Task<WorkUnit> AddAsync(Guid projectId, Guid? workItemId, WorkUnitType type, double value, string source, string metadataJson = "{}", CancellationToken cancellationToken = default);
    Task RecordFileActivityAsync(ProjectMeta project, string path, CancellationToken cancellationToken = default);
}
