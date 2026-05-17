// Provides database-backed task operations, including history tracking.
using System.IO;
using Microsoft.EntityFrameworkCore;
using ReachIT.Application.Contracts;
using ReachIT.Domain.Models;
using ReachIT.Infrastructure.Persistence;

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
            var normalizedPath = NormalizePath(filePath);
            existing.AttachedFilePath = normalizedPath;
            var projectId = await ResolveProjectIdForPathAsync(db, normalizedPath, cancellationToken).ConfigureAwait(false);
            if (projectId.HasValue)
            {
                await AddTaskFileLinkAsync(db, projectId.Value, taskId, normalizedPath, Directory.Exists(normalizedPath), "Attach", cancellationToken)
                    .ConfigureAwait(false);
            }
            
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

    public async Task LinkTaskFileAsync(Guid projectId, Guid taskId, string filePath, bool isDirectory, string source = "Manual", CancellationToken cancellationToken = default)
    {
        if (projectId == Guid.Empty || taskId == Guid.Empty || string.IsNullOrWhiteSpace(filePath))
        {
            return;
        }

        await using var db = _databaseService.CreateDbContext();
        var normalizedPath = NormalizePath(filePath);
        await AddTaskFileLinkAsync(db, projectId, taskId, normalizedPath, isDirectory, source, cancellationToken)
            .ConfigureAwait(false);

        var task = await db.Tasks.FirstOrDefaultAsync(x => x.Id == taskId, cancellationToken).ConfigureAwait(false);
        if (task is not null && string.IsNullOrWhiteSpace(task.AttachedFilePath))
        {
            task.AttachedFilePath = normalizedPath;
        }

        db.TaskHistoryEntries.Add(new TaskHistoryEntry
        {
            Id = Guid.NewGuid(),
            TaskItemId = taskId,
            ChangeType = "FileLinked",
            ChangedAtUtc = DateTime.UtcNow,
            Notes = $"Linked file: {filePath}"
        });

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<TaskFileLink>> GetTaskFileLinksAsync(Guid taskId, bool includeDescendants = false, CancellationToken cancellationToken = default)
    {
        if (taskId == Guid.Empty)
        {
            return [];
        }

        await using var db = _databaseService.CreateDbContext();
        var taskIds = new HashSet<Guid> { taskId };
        if (includeDescendants)
        {
            var allTasks = await db.Tasks.AsNoTracking().ToListAsync(cancellationToken).ConfigureAwait(false);
            AddDescendantTaskIds(taskId, allTasks, taskIds);
        }

        return await db.TaskFileLinks
            .AsNoTracking()
            .Where(x => taskIds.Contains(x.TaskItemId))
            .OrderBy(x => x.LinkedAtUtc)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task MoveLinkedPathAsync(Guid projectId, string oldPath, string newPath, bool isDirectory, CancellationToken cancellationToken = default)
    {
        if (projectId == Guid.Empty || string.IsNullOrWhiteSpace(oldPath) || string.IsNullOrWhiteSpace(newPath))
        {
            return;
        }

        await using var db = _databaseService.CreateDbContext();
        var normalizedOld = NormalizePath(oldPath);
        var normalizedNew = NormalizePath(newPath);
        var oldPrefix = normalizedOld.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var newPrefix = normalizedNew.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;

        var links = await db.TaskFileLinks
            .Where(x => x.ProjectId == projectId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var link in links)
        {
            if (string.Equals(link.FilePath, normalizedOld, StringComparison.OrdinalIgnoreCase))
            {
                link.FilePath = normalizedNew;
                link.IsDirectory = isDirectory;
            }
            else if (isDirectory && link.FilePath.StartsWith(oldPrefix, StringComparison.OrdinalIgnoreCase))
            {
                link.FilePath = newPrefix + link.FilePath[oldPrefix.Length..];
            }
        }

        var tasks = await db.Tasks
            .Where(x => x.AttachedFilePath != null && x.AttachedFilePath != string.Empty)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var task in tasks)
        {
            if (string.Equals(task.AttachedFilePath, normalizedOld, StringComparison.OrdinalIgnoreCase))
            {
                task.AttachedFilePath = normalizedNew;
            }
            else if (isDirectory && task.AttachedFilePath is not null && task.AttachedFilePath.StartsWith(oldPrefix, StringComparison.OrdinalIgnoreCase))
            {
                task.AttachedFilePath = newPrefix + task.AttachedFilePath[oldPrefix.Length..];
            }
        }

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<TaskItem> CreateAndAttachTaskToFileAsync(string title, string filePath, CancellationToken cancellationToken = default)
    {
        return await CreateAndAttachTaskToFileAsync(title, "Task attached from file view", DateTime.Now.AddDays(1), filePath, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<TaskItem> CreateAndAttachTaskToFileAsync(string title, string description, DateTime? dueDateLocal, string filePath, CancellationToken cancellationToken = default)
    {
        var task = new TaskItem
        {
            Id = Guid.NewGuid(),
            Title = string.IsNullOrWhiteSpace(title) ? "New Task" : title.Trim(),
            Description = string.IsNullOrWhiteSpace(description) ? "Task attached from file view" : description.Trim(),
            DueDateUtc = dueDateLocal?.Date.ToUniversalTime(),
            IsCompleted = false,
            AttachedFilePath = NormalizePath(filePath)
        };

        await AddTaskAsync(task, cancellationToken).ConfigureAwait(false);
        await using (var db = _databaseService.CreateDbContext())
        {
            var projectId = await ResolveProjectIdForPathAsync(db, task.AttachedFilePath, cancellationToken).ConfigureAwait(false);
            if (projectId.HasValue)
            {
                await AddTaskFileLinkAsync(db, projectId.Value, task.Id, task.AttachedFilePath, Directory.Exists(task.AttachedFilePath), "CreateAndAttach", cancellationToken)
                    .ConfigureAwait(false);
                await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        return task;
    }

    private static async Task<Guid?> ResolveProjectIdForPathAsync(ReachItDbContext db, string filePath, CancellationToken cancellationToken)
    {
        var normalizedPath = NormalizePath(filePath);
        var projects = await db.Projects.AsNoTracking().ToListAsync(cancellationToken).ConfigureAwait(false);
        var project = projects
            .Where(p => !string.IsNullOrWhiteSpace(p.ProjectDirectoryPath))
            .Where(p => IsInside(p.ProjectDirectoryPath, normalizedPath))
            .OrderByDescending(p => p.ProjectDirectoryPath.Length)
            .FirstOrDefault();

        return project?.Id;
    }

    private static async Task AddTaskFileLinkAsync(ReachItDbContext db, Guid projectId, Guid taskId, string filePath, bool isDirectory, string source, CancellationToken cancellationToken)
    {
        var normalizedPath = NormalizePath(filePath);
        var exists = await db.TaskFileLinks.AnyAsync(
            x => x.ProjectId == projectId && x.TaskItemId == taskId && x.FilePath == normalizedPath,
            cancellationToken).ConfigureAwait(false);

        if (exists)
        {
            return;
        }

        db.TaskFileLinks.Add(new TaskFileLink
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            TaskItemId = taskId,
            FilePath = normalizedPath,
            IsDirectory = isDirectory,
            LinkedAtUtc = DateTime.UtcNow,
            LinkSource = source
        });
    }

    private static void AddDescendantTaskIds(Guid parentId, IReadOnlyList<TaskItem> allTasks, HashSet<Guid> ids)
    {
        foreach (var child in allTasks.Where(x => x.ParentTaskId == parentId))
        {
            if (!ids.Add(child.Id))
            {
                continue;
            }

            AddDescendantTaskIds(child.Id, allTasks, ids);
        }
    }

    private static bool IsInside(string rootPath, string path)
    {
        try
        {
            var root = Path.GetFullPath(rootPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var target = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return target.Equals(root, StringComparison.OrdinalIgnoreCase) ||
                   target.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
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
