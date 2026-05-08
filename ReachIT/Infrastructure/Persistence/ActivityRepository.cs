using Microsoft.EntityFrameworkCore;
using ReachIT.Application.Contracts;
using ReachIT.Domain.Enums;
using ReachIT.Domain.Models;

namespace ReachIT.Infrastructure.Persistence;

public sealed class ActivityRepository : IActivityRepository
{
    private readonly IDatabaseService _databaseService;
    private readonly SemaphoreSlim _schemaGate = new(1, 1);
    private bool _schemaEnsured;

    public ActivityRepository(IDatabaseService databaseService)
    {
        _databaseService = databaseService;
    }

    public async Task AddAsync(ActivityEvent activityEvent, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);
        await using var db = _databaseService.CreateDbContext();
        db.ActivityEvents.Add(activityEvent);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<ActivityEvent>> GetRecentAsync(Guid projectId, int take = 100, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);
        await using var db = _databaseService.CreateDbContext();
        return await db.ActivityEvents
            .AsNoTracking()
            .Where(x => x.ProjectId == projectId)
            .OrderByDescending(x => x.Timestamp)
            .Take(take)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<ActivityEvent>> GetSinceAsync(Guid projectId, DateTime sinceUtc, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);
        await using var db = _databaseService.CreateDbContext();
        return await db.ActivityEvents
            .AsNoTracking()
            .Where(x => x.ProjectId == projectId && x.Timestamp >= sinceUtc)
            .OrderByDescending(x => x.Timestamp)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<ActivityEvent?> GetLatestAsync(Guid projectId, ActivityEventType? eventType = null, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);
        await using var db = _databaseService.CreateDbContext();
        var query = db.ActivityEvents.AsNoTracking().Where(x => x.ProjectId == projectId);
        if (eventType.HasValue)
        {
            query = query.Where(x => x.EventType == eventType.Value);
        }

        return await query
            .OrderByDescending(x => x.Timestamp)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task ClearProjectAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);
        await using var db = _databaseService.CreateDbContext();
        await db.ActivityEvents
            .Where(x => x.ProjectId == projectId)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task EnsureSchemaAsync(CancellationToken cancellationToken)
    {
        if (_schemaEnsured)
        {
            return;
        }

        await _schemaGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_schemaEnsured)
            {
                return;
            }

            await _databaseService.InitializeAsync(cancellationToken).ConfigureAwait(false);
            _schemaEnsured = true;
        }
        finally
        {
            _schemaGate.Release();
        }
    }
}
