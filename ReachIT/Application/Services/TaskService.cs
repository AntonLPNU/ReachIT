// Provides safe in-memory task operations for initial app scaffold.
using ReachIT.Application.Contracts;
using ReachIT.Domain.Models;

namespace ReachIT.Application.Services;

public sealed class TaskService : ITaskService
{
    private readonly List<TaskItem> _tasks =
    [
        new TaskItem { Title = "Prepare workspace structure", Description = "Initial scaffold task.", IsCompleted = false },
        new TaskItem { Title = "Connect .rit loader", Description = "TODO parser integration.", IsCompleted = false }
    ];

    public Task<IReadOnlyList<TaskItem>> GetTasksAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<TaskItem>>(_tasks.ToList());
    }

    public Task AddTaskAsync(TaskItem task, CancellationToken cancellationToken = default)
    {
        if (task is null)
        {
            throw new ArgumentNullException(nameof(task));
        }

        _tasks.Add(task);
        return Task.CompletedTask;
    }

    public Task UpdateTaskAsync(TaskItem task, CancellationToken cancellationToken = default)
    {
        var existing = _tasks.FirstOrDefault(x => x.Id == task.Id);
        if (existing is null)
        {
            return Task.CompletedTask;
        }

        existing.Title = task.Title;
        existing.Description = task.Description;
        existing.IsCompleted = task.IsCompleted;
        existing.DueDateUtc = task.DueDateUtc;
        existing.CategoryId = task.CategoryId;
        return Task.CompletedTask;
    }

    public Task DeleteTaskAsync(Guid taskId, CancellationToken cancellationToken = default)
    {
        _tasks.RemoveAll(x => x.Id == taskId);
        return Task.CompletedTask;
    }

    public Task LinkTaskToNodeAsync(Guid taskId, Guid projectTreeNodeId, CancellationToken cancellationToken = default)
    {
        var existing = _tasks.FirstOrDefault(x => x.Id == taskId);
        if (existing is not null)
        {
            existing.RelatedProjectTreeNodeId = projectTreeNodeId;
        }

        // TODO: Persist task-tree relation in SQLite to support "Show related tasks in tree" option.
        return Task.CompletedTask;
    }
}
