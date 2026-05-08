// Provides database-backed task operations, including history tracking.
using System.IO;
using Microsoft.EntityFrameworkCore;
using ReachIT.Application.Contracts;
using ReachIT.Domain.Models;

namespace ReachIT.Application.Services;

public sealed class TaskService : ITaskService
{
    private readonly IDatabaseService _databaseService;

    public TaskService(IDatabaseService databaseService)
    {
        _databaseService = databaseService;
    }

    public async Task<IReadOnlyList<TaskItem>> GetTasksAsync(CancellationToken cancellationToken = default)
    {
        await using var db = _databaseService.CreateDbContext();
        return await db.Tasks
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<TaskItem>> GetTasksByFilePathAsync(string filePath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            return [];

        // Simple approach MVP: find tasks linked by RelatedProjectTreeNodeId or an auxiliary mapping.
        // For MVP, we will assume RelatedProjectTreeNodeId handles tree files.
        // If we strictly need file paths mapped, we can add a FilePath property to TaskItem.
        await using var db = _databaseService.CreateDbContext();
        var normalizedPath = NormalizePath(filePath);
        return await db.Tasks
            .AsNoTracking()
            .Where(t => t.AttachedFilePath == normalizedPath)
            .ToListAsync(cancellationToken);
    }

    public async Task AddTaskAsync(TaskItem task, CancellationToken cancellationToken = default)
    {
        if (task is null) throw new ArgumentNullException(nameof(task));

        if (task.Id == Guid.Empty) task.Id = Guid.NewGuid();
        
        await using var db = _databaseService.CreateDbContext();
        if (task.Priority <= 0)
        {
            var maxPriority = await db.Tasks
                .Where(x => x.ParentTaskId == task.ParentTaskId)
                .Select(x => (int?)x.Priority)
                .MaxAsync(cancellationToken) ?? 0;

            task.Priority = maxPriority + 1;
        }

        ApplyCompletionState(task, task.IsCompleted);
        db.Tasks.Add(task);
        
        db.TaskHistoryEntries.Add(new TaskHistoryEntry
        {
            Id = Guid.NewGuid(),
            TaskItemId = task.Id,
            ChangeType = "Created",
            ChangedAtUtc = DateTime.UtcNow,
            Notes = $"Task created: {task.Title}"
        });
        
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateTaskAsync(TaskItem task, CancellationToken cancellationToken = default)
    {
        await using var db = _databaseService.CreateDbContext();
        var existing = await db.Tasks.FirstOrDefaultAsync(x => x.Id == task.Id, cancellationToken);
        if (existing is null) return;

        var wasCompleted = existing.IsCompleted;
        
        existing.Title = task.Title;
        existing.Description = task.Description;
        existing.IsCompleted = task.IsCompleted;
        existing.Status = task.Status;
        existing.Priority = task.Priority;
        existing.DueDateUtc = task.DueDateUtc;
        existing.StartedAtUtc = task.StartedAtUtc;
        existing.CompletedAtUtc = task.CompletedAtUtc;
        existing.ParentTaskId = task.ParentTaskId;
        existing.CategoryId = task.CategoryId;
        existing.RelatedProjectTreeNodeId = task.RelatedProjectTreeNodeId;
        existing.AttachedFilePath = task.AttachedFilePath;
        ApplyCompletionState(existing, wasCompleted);
        
        db.TaskHistoryEntries.Add(new TaskHistoryEntry
        {
            Id = Guid.NewGuid(),
            TaskItemId = task.Id,
            ChangeType = "Updated",
            ChangedAtUtc = DateTime.UtcNow,
            Notes = wasCompleted != existing.IsCompleted 
                ? $"Completion changed to: {existing.IsCompleted}" 
                : "Properties updated"
        });

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteTaskAsync(Guid taskId, CancellationToken cancellationToken = default)
    {
        await using var db = _databaseService.CreateDbContext();
        var task = await db.Tasks.FirstOrDefaultAsync(x => x.Id == taskId, cancellationToken);
        if (task is not null)
        {
            db.Tasks.Remove(task);
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task AttachTaskToFileAsync(Guid taskId, string filePath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(filePath)) return;
        
        await using var db = _databaseService.CreateDbContext();
        var existing = await db.Tasks.FirstOrDefaultAsync(x => x.Id == taskId, cancellationToken);
        if (existing is not null)
        {
            existing.AttachedFilePath = NormalizePath(filePath);
            
            db.TaskHistoryEntries.Add(new TaskHistoryEntry
            {
                Id = Guid.NewGuid(),
                TaskItemId = taskId,
                ChangeType = "Attached",
                ChangedAtUtc = DateTime.UtcNow,
                Notes = $"Attached to file: {filePath}"
            });
            
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<TaskItem> CreateAndAttachTaskToFileAsync(string title, string filePath, CancellationToken cancellationToken = default)
    {
        var task = new TaskItem
        {
            Id = Guid.NewGuid(),
            Title = string.IsNullOrWhiteSpace(title) ? "New Task" : title.Trim(),
            Description = "Task attached from file view",
            DueDateUtc = DateTime.UtcNow.AddDays(1),
            IsCompleted = false,
            AttachedFilePath = NormalizePath(filePath)
        };

        await AddTaskAsync(task, cancellationToken).ConfigureAwait(false);
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

    private static void ApplyCompletionState(TaskItem task, bool wasCompleted)
    {
        if (task.IsCompleted)
        {
            task.Status = "Done";
            task.CompletedAtUtc ??= DateTime.UtcNow;
            task.StartedAtUtc ??= task.CompletedAtUtc;
            return;
        }

        if (wasCompleted)
        {
            task.CompletedAtUtc = null;
        }
    }
}
