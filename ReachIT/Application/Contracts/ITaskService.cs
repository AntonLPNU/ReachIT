// Defines task management operations.
using ReachIT.Domain.Models;

namespace ReachIT.Application.Contracts;

public interface ITaskService
{
    Task<IReadOnlyList<TaskItem>> GetTasksAsync(CancellationToken cancellationToken = default);
    Task AddTaskAsync(TaskItem task, CancellationToken cancellationToken = default);
    Task UpdateTaskAsync(TaskItem task, CancellationToken cancellationToken = default);
    Task DeleteTaskAsync(Guid taskId, CancellationToken cancellationToken = default);
    Task LinkTaskToNodeAsync(Guid taskId, Guid projectTreeNodeId, CancellationToken cancellationToken = default);
}
