using Microsoft.EntityFrameworkCore;
using ReachIT.Application.Contracts;
using ReachIT.Domain.Models;

namespace ReachIT.Infrastructure.Persistence;

public sealed class WorkItemRepository : IWorkItemRepository
{
    private readonly IDatabaseService _databaseService;

    public WorkItemRepository(IDatabaseService databaseService)
    {
        _databaseService = databaseService;
    }

    public async Task<IReadOnlyList<WorkItem>> GetByProjectAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        await using var db = _databaseService.CreateDbContext();
        return await db.WorkItems
            .Where(x => x.ProjectId == projectId)
            .OrderBy(x => x.CreatedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<WorkItem?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var db = _databaseService.CreateDbContext();
        return await db.WorkItems.FirstOrDefaultAsync(x => x.Id == id, cancellationToken).ConfigureAwait(false);
    }

    public async Task<WorkItem?> GetByLinkedPathAsync(Guid projectId, string linkedPath, CancellationToken cancellationToken = default)
    {
        await using var db = _databaseService.CreateDbContext();
        return await db.WorkItems
            .FirstOrDefaultAsync(x => x.ProjectId == projectId && x.LinkedPath == linkedPath, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task AddAsync(WorkItem item, CancellationToken cancellationToken = default)
    {
        await using var db = _databaseService.CreateDbContext();
        db.WorkItems.Add(item);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task UpdateAsync(WorkItem item, CancellationToken cancellationToken = default)
    {
        await using var db = _databaseService.CreateDbContext();
        db.WorkItems.Update(item);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
