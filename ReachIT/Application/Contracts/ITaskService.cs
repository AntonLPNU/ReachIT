// Defines task management operations.
using ReachIT.Domain.Models;

namespace ReachIT.Application.Contracts;

public interface ITaskService
{
    Task<IReadOnlyList<TaskItem>> GetTasksAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TaskItem>> GetTasksByFilePathAsync(string filePath, CancellationToken cancellationToken = default);
    Task AddTaskAsync(TaskItem task, CancellationToken cancellationToken = default);
    Task UpdateTaskAsync(TaskItem task, CancellationToken cancellationToken = default);
    Task DeleteTaskAsync(Guid taskId, CancellationToken cancellationToken = default);
    Task AttachTaskToFileAsync(Guid taskId, string filePath, CancellationToken cancellationToken = default);
    Task<TaskItem> CreateAndAttachTaskToFileAsync(string title, string filePath, CancellationToken cancellationToken = default);
}
