// Provides safe in-memory task operations for initial app scaffold.
using System.IO;
using ReachIT.Application.Contracts;
using ReachIT.Domain.Models;

namespace ReachIT.Application.Services;

public sealed class TaskService : ITaskService
{
    private readonly Dictionary<string, HashSet<Guid>> _taskIdsByFilePath = new(StringComparer.OrdinalIgnoreCase);

    private readonly List<TaskItem> _tasks =
    [
        new TaskItem { Title = "Prepare workspace structure", Description = "Initial scaffold task.", IsCompleted = false },
        new TaskItem { Title = "Connect .rit loader", Description = "TODO parser integration.", IsCompleted = false }
    ];

    public Task<IReadOnlyList<TaskItem>> GetTasksAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<TaskItem>>(_tasks.ToList());
    }

    public Task<IReadOnlyList<TaskItem>> GetTasksByFilePathAsync(string filePath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return Task.FromResult<IReadOnlyList<TaskItem>>([]);
        }

        var normalizedPath = NormalizePath(filePath);
        if (!_taskIdsByFilePath.TryGetValue(normalizedPath, out var taskIds))
        {
            return Task.FromResult<IReadOnlyList<TaskItem>>([]);
        }

        var attached = _tasks.Where(x => taskIds.Contains(x.Id)).ToList();
        return Task.FromResult<IReadOnlyList<TaskItem>>(attached);
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

        foreach (var pair in _taskIdsByFilePath)
        {
            pair.Value.Remove(taskId);
        }

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

    public Task AttachTaskToFileAsync(Guid taskId, string filePath, CancellationToken cancellationToken = default)
    {
        var existing = _tasks.FirstOrDefault(x => x.Id == taskId);
        if (existing is null || string.IsNullOrWhiteSpace(filePath))
        {
            return Task.CompletedTask;
        }

        var normalizedPath = NormalizePath(filePath);
        if (!_taskIdsByFilePath.TryGetValue(normalizedPath, out var taskIds))
        {
            taskIds = [];
            _taskIdsByFilePath[normalizedPath] = taskIds;
        }

        taskIds.Add(taskId);
        return Task.CompletedTask;
    }

    public async Task<TaskItem> CreateAndAttachTaskToFileAsync(string title, string filePath, CancellationToken cancellationToken = default)
    {
        var task = new TaskItem
        {
            Title = string.IsNullOrWhiteSpace(title) ? "New Task" : title.Trim(),
            Description = "Task attached from file view",
            DueDateUtc = DateTime.UtcNow.AddDays(1),
            IsCompleted = false
        };

        await AddTaskAsync(task, cancellationToken).ConfigureAwait(false);
        await AttachTaskToFileAsync(task.Id, filePath, cancellationToken).ConfigureAwait(false);
        return task;
    }

    private static string NormalizePath(string path)
    {
        try
        {
            return Path.GetFullPath(path);
        }
        catch
        {
            return path;
        }
    }
}
