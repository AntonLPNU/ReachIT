using ReachIT.Application.Contracts;

namespace ReachIT.Application.Services;

public sealed class TaskLinkingService : ITaskLinkingService
{
    private readonly IWorkItemRepository _workItemRepository;

    public TaskLinkingService(IWorkItemRepository workItemRepository)
    {
        _workItemRepository = workItemRepository;
    }

    public async Task LinkToPathAsync(Guid workItemId, string path, CancellationToken cancellationToken = default)
    {
        var item = await _workItemRepository.GetByIdAsync(workItemId, cancellationToken).ConfigureAwait(false);
        if (item is null)
        {
            return;
        }

        item.LinkedPath = path;
        item.UpdatedAt = DateTime.UtcNow;
        await _workItemRepository.UpdateAsync(item, cancellationToken).ConfigureAwait(false);
    }

    public async Task LinkToAppAsync(Guid workItemId, string appName, CancellationToken cancellationToken = default)
    {
        var item = await _workItemRepository.GetByIdAsync(workItemId, cancellationToken).ConfigureAwait(false);
        if (item is null)
        {
            return;
        }

        item.LinkedApp = appName;
        item.UpdatedAt = DateTime.UtcNow;
        await _workItemRepository.UpdateAsync(item, cancellationToken).ConfigureAwait(false);
    }

    public async Task LinkToMilestoneAsync(Guid workItemId, Guid milestoneId, CancellationToken cancellationToken = default)
    {
        var item = await _workItemRepository.GetByIdAsync(workItemId, cancellationToken).ConfigureAwait(false);
        if (item is null)
        {
            return;
        }

        item.MilestoneId = milestoneId;
        item.UpdatedAt = DateTime.UtcNow;
        await _workItemRepository.UpdateAsync(item, cancellationToken).ConfigureAwait(false);
    }
}
