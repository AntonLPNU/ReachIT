using ReachIT.Domain.Models;

namespace ReachIT.Application.Contracts;

public interface IProgressCalculationService
{
    double CalculateWorkItemProgress(WorkItem item, IReadOnlyList<WorkItem> allItems);
    Task RecalculateProjectAsync(Guid projectId, CancellationToken cancellationToken = default);
}
