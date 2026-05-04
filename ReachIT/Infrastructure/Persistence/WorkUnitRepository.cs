using Microsoft.EntityFrameworkCore;
using ReachIT.Application.Contracts;
using ReachIT.Domain.Models;

namespace ReachIT.Infrastructure.Persistence;

public sealed class WorkUnitRepository : IWorkUnitRepository
{
    private readonly IDatabaseService _databaseService;

    public WorkUnitRepository(IDatabaseService databaseService)
    {
        _databaseService = databaseService;
    }

    public async Task<IReadOnlyList<WorkUnit>> GetByProjectAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        await using var db = _databaseService.CreateDbContext();
        return await db.WorkUnits
            .Where(x => x.ProjectId == projectId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<WorkUnit>> GetByWorkItemAsync(Guid workItemId, CancellationToken cancellationToken = default)
    {
        await using var db = _databaseService.CreateDbContext();
        return await db.WorkUnits
            .Where(x => x.WorkItemId == workItemId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task AddAsync(WorkUnit unit, CancellationToken cancellationToken = default)
    {
        await using var db = _databaseService.CreateDbContext();
        db.WorkUnits.Add(unit);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
