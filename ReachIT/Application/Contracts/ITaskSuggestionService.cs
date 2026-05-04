using ReachIT.Domain.Models;

namespace ReachIT.Application.Contracts;

public interface ITaskSuggestionService
{
    Task<IReadOnlyList<TaskSuggestion>> GetNewSuggestionsAsync(Guid projectId, CancellationToken cancellationToken = default);
    Task GenerateFromActivityAsync(ProjectMeta project, CancellationToken cancellationToken = default);
    Task<WorkItem?> AcceptAsync(Guid suggestionId, CancellationToken cancellationToken = default);
    Task IgnoreAsync(Guid suggestionId, CancellationToken cancellationToken = default);
}
