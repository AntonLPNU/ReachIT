using ReachIT.Domain.Models;

namespace ReachIT.Application.Contracts;

public interface IProductivityScoringService
{
    Task<ProductivityScoreSnapshot> ScoreAsync(ProjectMeta project, CancellationToken cancellationToken = default);
}
