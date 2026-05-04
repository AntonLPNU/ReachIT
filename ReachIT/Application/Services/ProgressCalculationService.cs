using ReachIT.Application.Contracts;
using ReachIT.Domain.Enums;
using ReachIT.Domain.Models;

namespace ReachIT.Application.Services;

public sealed class ProgressCalculationService : IProgressCalculationService
{
    private readonly IWorkItemRepository _workItemRepository;

    public ProgressCalculationService(IWorkItemRepository workItemRepository)
    {
        _workItemRepository = workItemRepository;
    }

    public double CalculateWorkItemProgress(WorkItem item, IReadOnlyList<WorkItem> allItems)
    {
        if (item.Status == WorkItemStatus.Done)
        {
            return 100;
        }

        if (item.Status == WorkItemStatus.Cancelled)
        {
            return item.ProgressPercent;
        }

        var children = allItems.Where(x => x.ParentId == item.Id).ToList();
        if (children.Count > 0)
        {
            var weightedTotal = children.Sum(x => Math.Max(1, x.EstimatedWorkUnits));
            if (weightedTotal <= 0)
            {
                return Math.Round(children.Average(x => x.ProgressPercent), 1);
            }

            var weightedProgress = children.Sum(x => CalculateWorkItemProgress(x, allItems) * Math.Max(1, x.EstimatedWorkUnits));
            return Math.Round(weightedProgress / weightedTotal, 1);
        }

        if (item.EstimatedWorkUnits > 0)
        {
            return Math.Clamp(Math.Round(item.CompletedWorkUnits / item.EstimatedWorkUnits * 100d, 1), 0, 100);
        }

        return Math.Clamp(item.ProgressPercent, 0, 100);
    }

    public async Task RecalculateProjectAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        var items = await _workItemRepository.GetByProjectAsync(projectId, cancellationToken).ConfigureAwait(false);
        foreach (var item in items)
        {
            var progress = CalculateWorkItemProgress(item, items);
            if (Math.Abs(item.ProgressPercent - progress) < 0.01)
            {
                continue;
            }

            item.ProgressPercent = progress;
            item.UpdatedAt = DateTime.UtcNow;
            await _workItemRepository.UpdateAsync(item, cancellationToken).ConfigureAwait(false);
        }
    }
}
