// Keeps in-memory recent projects and external files for start/explorer panels.
using Microsoft.EntityFrameworkCore;
using ReachIT.Application.Contracts;
using ReachIT.Domain.Models;

namespace ReachIT.Application.Services;

public sealed class RecentFilesService : IRecentFilesService
{
    private readonly IDatabaseService _databaseService;

    public RecentFilesService(IDatabaseService databaseService)
    {
        _databaseService = databaseService;
    }

    public async Task<IReadOnlyList<ProjectMeta>> GetRecentProjectsAsync(CancellationToken cancellationToken = default)
    {
        await using var dbContext = _databaseService.CreateDbContext();
        return await dbContext.Projects
            .AsNoTracking()
            .OrderByDescending(x => x.UpdatedAtUtc)
            .Take(20)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task AddRecentProjectAsync(ProjectMeta project, CancellationToken cancellationToken = default)
    {
        await using var dbContext = _databaseService.CreateDbContext();
        var ritFilePath = project.RitFilePath ?? string.Empty;
        var existing = await dbContext.Projects
            .FirstOrDefaultAsync(x => EF.Functions.Collate(x.RitFilePath, "NOCASE") == ritFilePath, cancellationToken)
            .ConfigureAwait(false);

        if (existing is null)
        {
            dbContext.Projects.Add(project);
        }
        else
        {
            existing.ProjectName = project.ProjectName;
            existing.Description = project.Description;
            existing.ProjectDirectoryPath = project.ProjectDirectoryPath;
            existing.TemplateType = project.TemplateType;
            existing.UpdatedAtUtc = DateTime.UtcNow;
        }

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task RemoveRecentProjectAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        await using var dbContext = _databaseService.CreateDbContext();
        var existing = await dbContext.Projects.FirstOrDefaultAsync(x => x.Id == projectId, cancellationToken).ConfigureAwait(false);
        if (existing is not null)
        {
            dbContext.Projects.Remove(existing);
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task<IReadOnlyList<RecentExternalFileItem>> GetRecentExternalFilesAsync(CancellationToken cancellationToken = default)
    {
        await using var dbContext = _databaseService.CreateDbContext();
        return await dbContext.RecentExternalFiles
            .AsNoTracking()
            .OrderByDescending(x => x.LastOpenedAtUtc)
            .Take(100)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task AddRecentExternalFileAsync(RecentExternalFileItem item, CancellationToken cancellationToken = default)
    {
        await using var dbContext = _databaseService.CreateDbContext();
        var sourcePathOrUrl = item.SourcePathOrUrl ?? string.Empty;
        var existing = await dbContext.RecentExternalFiles
            .FirstOrDefaultAsync(x => EF.Functions.Collate(x.SourcePathOrUrl, "NOCASE") == sourcePathOrUrl, cancellationToken)
            .ConfigureAwait(false);

        if (existing is null)
        {
            dbContext.RecentExternalFiles.Add(item);
        }
        else
        {
            existing.DisplayName = item.DisplayName;
            existing.ResourceType = item.ResourceType;
            existing.LastOpenedAtUtc = DateTime.UtcNow;
        }

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task RemoveRecentExternalFileAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var dbContext = _databaseService.CreateDbContext();
        var existing = await dbContext.RecentExternalFiles.FirstOrDefaultAsync(x => x.Id == id, cancellationToken).ConfigureAwait(false);
        if (existing is not null)
        {
            dbContext.RecentExternalFiles.Remove(existing);
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}
