using ReachIT.Application.Contracts;
using ReachIT.Domain.Enums;
using ReachIT.Domain.Models;

namespace ReachIT.Application.Services;

public sealed class WorkItemService : IWorkItemService
{
    private readonly IWorkItemRepository _workItemRepository;
    private readonly ITaskService _taskService;

    public WorkItemService(IWorkItemRepository workItemRepository, ITaskService taskService)
    {
        _workItemRepository = workItemRepository;
        _taskService = taskService;
    }

    public Task<IReadOnlyList<WorkItem>> GetProjectWorkItemsAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        return _workItemRepository.GetByProjectAsync(projectId, cancellationToken);
    }

    public async Task<WorkItem> CreateAsync(WorkItem item, CancellationToken cancellationToken = default)
    {
        item.CreatedAt = DateTime.UtcNow;
        item.UpdatedAt = item.CreatedAt;
        await _workItemRepository.AddAsync(item, cancellationToken).ConfigureAwait(false);
        return item;
    }

    public async Task SyncLegacyTasksAsync(ProjectMeta project, CancellationToken cancellationToken = default)
    {
        var existing = await _workItemRepository.GetByProjectAsync(project.Id, cancellationToken).ConfigureAwait(false);
        var existingLegacyIds = existing
            .Where(x => x.LegacyTaskItemId.HasValue)
            .Select(x => x.LegacyTaskItemId!.Value)
            .ToHashSet();

        var tasks = await _taskService.GetTasksAsync(cancellationToken).ConfigureAwait(false);
        foreach (var task in tasks.Where(x => !existingLegacyIds.Contains(x.Id)))
        {
            await _workItemRepository.AddAsync(new WorkItem
            {
                ProjectId = project.Id,
                ParentId = task.ParentTaskId,
                Title = task.Title,
                Description = task.Description,
                Type = task.ParentTaskId.HasValue ? WorkItemType.Subtask : WorkItemType.Task,
                Status = task.IsCompleted ? WorkItemStatus.Done : MapStatus(task.Status),
                Priority = task.Priority,
                ProgressPercent = task.IsCompleted ? 100 : 0,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                CompletedAt = task.IsCompleted ? DateTime.UtcNow : null,
                Deadline = task.DueDateUtc,
                EstimatedWorkUnits = 1,
                CompletedWorkUnits = task.IsCompleted ? 1 : 0,
                LinkedPath = task.AttachedFilePath ?? string.Empty,
                Tags = string.Join(';', task.Tags.Select(x => x.Value)),
                LegacyTaskItemId = task.Id
            }, cancellationToken).ConfigureAwait(false);
        }
    }

    private static WorkItemStatus MapStatus(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return WorkItemStatus.Backlog;
        }

        return status.Trim().ToLowerInvariant() switch
        {
            "done" or "completed" => WorkItemStatus.Done,
            "active" or "in progress" or "todo" or "to do" => WorkItemStatus.InProgress,
            "review" => WorkItemStatus.Review,
            "waiting" => WorkItemStatus.Waiting,
            "planned" => WorkItemStatus.Planned,
            _ => WorkItemStatus.Backlog
        };
    }
}
