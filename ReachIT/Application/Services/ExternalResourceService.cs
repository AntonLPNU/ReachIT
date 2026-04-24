// Handles external resources attachment/copy/link behavior for projects.
using System.IO;
using Microsoft.EntityFrameworkCore;
using ReachIT.Application.Contracts;
using ReachIT.Domain.Enums;
using ReachIT.Domain.Models;

namespace ReachIT.Application.Services;

public sealed class ExternalResourceService : IExternalResourceService
{
    private readonly IDatabaseService _databaseService;
    private readonly IRecentFilesService _recentFilesService;

    public ExternalResourceService(
        IDatabaseService databaseService,
        IRecentFilesService recentFilesService)
    {
        _databaseService = databaseService;
        _recentFilesService = recentFilesService;
    }

    public async Task<IReadOnlyList<ExternalResourceItem>> GetByProjectAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        await using var dbContext = _databaseService.CreateDbContext();
        return await dbContext.ExternalResources
            .AsNoTracking()
            .Where(x => x.ProjectMetaId == projectId)
            .OrderByDescending(x => x.AddedAtUtc)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<ExternalResourceItem> AttachAsync(Guid projectId, string sourcePathOrUrl, CancellationToken cancellationToken = default)
    {
        if (File.Exists(sourcePathOrUrl))
        {
            return await CopyIntoProjectAsync(projectId, sourcePathOrUrl, cancellationToken).ConfigureAwait(false);
        }

        return await SaveAsLinkAsync(projectId, sourcePathOrUrl, cancellationToken).ConfigureAwait(false);
    }

    public async Task<ExternalResourceItem> CopyIntoProjectAsync(Guid projectId, string sourcePathOrUrl, CancellationToken cancellationToken = default)
    {
        await using var dbContext = _databaseService.CreateDbContext();
        var currentProject = await dbContext.Projects
            .FirstOrDefaultAsync(x => x.Id == projectId, cancellationToken)
            .ConfigureAwait(false);
        if (currentProject is null)
        {
            throw new InvalidOperationException("Project metadata was not found.");
        }

        var importsDirectory = Path.Combine(currentProject.ProjectDirectoryPath, "Imports");
        Directory.CreateDirectory(importsDirectory);

        var fileName = Path.GetFileName(sourcePathOrUrl);
        if (string.IsNullOrWhiteSpace(fileName))
        {
            fileName = $"External_{Guid.NewGuid():N}.bin";
        }

        var destinationPath = GetUniqueFilePath(importsDirectory, fileName);
        if (File.Exists(sourcePathOrUrl))
        {
            File.Copy(sourcePathOrUrl, destinationPath, overwrite: false);
        }
        else
        {
            // TODO: Add downloader for non-local resources when user explicitly requests import.
        }

        var item = new ExternalResourceItem
        {
            ProjectMetaId = projectId,
            DisplayName = fileName,
            SourcePathOrUrl = sourcePathOrUrl,
            StoredPath = destinationPath,
            ResourceType = ExternalResourceType.ExternalFile,
            AttachMode = ExternalResourceAttachMode.CopyIntoProject
        };

        dbContext.ExternalResources.Add(item);

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await _recentFilesService.AddRecentExternalFileAsync(new RecentExternalFileItem
        {
            DisplayName = fileName,
            SourcePathOrUrl = sourcePathOrUrl,
            ResourceType = ExternalResourceType.ExternalFile,
            LastOpenedAtUtc = DateTime.UtcNow
        }, cancellationToken).ConfigureAwait(false);

        return item;
    }

    public async Task<ExternalResourceItem> SaveAsLinkAsync(Guid projectId, string sourcePathOrUrl, CancellationToken cancellationToken = default)
    {
        var resourceType = sourcePathOrUrl.EndsWith(".html", StringComparison.OrdinalIgnoreCase)
            ? ExternalResourceType.OfflinePage
            : sourcePathOrUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                ? ExternalResourceType.WebLink
                : ExternalResourceType.ExternalFile;

        var displayName = Path.GetFileName(sourcePathOrUrl);
        if (string.IsNullOrWhiteSpace(displayName))
        {
            displayName = sourcePathOrUrl;
        }

        var item = new ExternalResourceItem
        {
            ProjectMetaId = projectId,
            DisplayName = displayName,
            SourcePathOrUrl = sourcePathOrUrl,
            ResourceType = resourceType,
            AttachMode = ExternalResourceAttachMode.LinkOnly
        };

        await using var dbContext = _databaseService.CreateDbContext();
        dbContext.ExternalResources.Add(item);

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await _recentFilesService.AddRecentExternalFileAsync(new RecentExternalFileItem
        {
            DisplayName = displayName,
            SourcePathOrUrl = sourcePathOrUrl,
            ResourceType = resourceType,
            LastOpenedAtUtc = DateTime.UtcNow
        }, cancellationToken).ConfigureAwait(false);

        return item;
    }

    private static string GetUniqueFilePath(string directoryPath, string fileName)
    {
        var candidatePath = Path.Combine(directoryPath, fileName);
        if (!File.Exists(candidatePath))
        {
            return candidatePath;
        }

        var baseName = Path.GetFileNameWithoutExtension(fileName);
        var extension = Path.GetExtension(fileName);
        var index = 1;

        while (true)
        {
            candidatePath = Path.Combine(directoryPath, $"{baseName}_{index}{extension}");
            if (!File.Exists(candidatePath))
            {
                return candidatePath;
            }

            index++;
        }
    }
}
