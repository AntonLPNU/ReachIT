// Handles external resources attachment/copy/link behavior for projects.
using System.IO;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ReachIT.Application.Contracts;
using ReachIT.Application.Security;
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
        sourcePathOrUrl = sourcePathOrUrl.Trim();
        var isWebUrl = sourcePathOrUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                       || sourcePathOrUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
        if (isWebUrl)
        {
            sourcePathOrUrl = WebResourceSecurity.NormalizeAndValidateUrl(sourcePathOrUrl);
        }
        else if (LooksLikeNonFileUri(sourcePathOrUrl))
        {
            throw new InvalidOperationException("Only http and https links can be saved as web resources.");
        }

        var resourceType = isWebUrl
            ? ExternalResourceType.WebLink
            : ExternalResourceType.ExternalFile;

        await using var dbContext = _databaseService.CreateDbContext();
        var currentProject = await dbContext.Projects
            .FirstOrDefaultAsync(x => x.Id == projectId, cancellationToken)
            .ConfigureAwait(false);
        if (currentProject is null)
        {
            throw new InvalidOperationException("Project metadata was not found.");
        }

        var displayName = BuildDisplayName(sourcePathOrUrl, resourceType);
        var storedPath = resourceType is ExternalResourceType.WebLink or ExternalResourceType.OfflinePage
            ? await CreateWebResourceFileAsync(currentProject.ProjectDirectoryPath, displayName, sourcePathOrUrl, cancellationToken).ConfigureAwait(false)
            : null;

        var item = new ExternalResourceItem
        {
            ProjectMetaId = projectId,
            DisplayName = displayName,
            SourcePathOrUrl = sourcePathOrUrl,
            StoredPath = storedPath,
            ResourceType = resourceType,
            AttachMode = ExternalResourceAttachMode.LinkOnly
        };

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

    private static async Task<string> CreateWebResourceFileAsync(
        string projectDirectoryPath,
        string displayName,
        string sourceUrl,
        CancellationToken cancellationToken)
    {
        var webResourcesDirectory = Path.Combine(projectDirectoryPath, "Web Resources");
        Directory.CreateDirectory(webResourcesDirectory);

        var metadata = new WebResourceLinkMetadata
        {
            Title = displayName,
            PrimaryUrl = WebResourceSecurity.NormalizeAndValidateUrl(sourceUrl),
            AlternateUrls = [],
            AllowedFocusHosts = GetUrlHost(sourceUrl) is { Length: > 0 } host ? [WebResourceSecurity.NormalizeAndValidateHost(host)] : [],
            ReadingProgress = "not-started",
            LastReadMarker = string.Empty,
            Highlights = [],
            Notes = "Add notes, highlights, alternate URLs, and reading progress here."
        };

        var fileName = GetUniqueFilePath(webResourcesDirectory, $"{SanitizeFileName(displayName)}.reachit-link.json");
        var payload = JsonSerializer.Serialize(metadata, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(fileName, payload, cancellationToken).ConfigureAwait(false);
        return fileName;
    }

    private static string BuildDisplayName(string sourcePathOrUrl, ExternalResourceType resourceType)
    {
        if (resourceType is ExternalResourceType.WebLink or ExternalResourceType.OfflinePage
            && Uri.TryCreate(sourcePathOrUrl, UriKind.Absolute, out var uri))
        {
            var path = uri.AbsolutePath.Trim('/');
            if (string.IsNullOrWhiteSpace(path))
            {
                return uri.Host;
            }

            var lastSegment = Uri.UnescapeDataString(path.Split('/', StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? uri.Host);
            return string.IsNullOrWhiteSpace(lastSegment)
                ? uri.Host
                : $"{uri.Host} - {lastSegment}";
        }

        var displayName = Path.GetFileName(sourcePathOrUrl);
        return string.IsNullOrWhiteSpace(displayName) ? sourcePathOrUrl : displayName;
    }

    private static string GetUrlHost(string sourceUrl)
    {
        return Uri.TryCreate(sourceUrl, UriKind.Absolute, out var uri)
            ? uri.Host
            : string.Empty;
    }

    private static bool LooksLikeNonFileUri(string value)
    {
        var colonIndex = value.IndexOf(':');
        if (colonIndex <= 1)
        {
            return false;
        }

        var scheme = value[..colonIndex];
        return scheme.All(ch => char.IsLetterOrDigit(ch) || ch is '+' or '-' or '.');
    }

    private static string SanitizeFileName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars().ToHashSet();
        var cleaned = new string(value.Select(ch => invalid.Contains(ch) ? '-' : ch).ToArray()).Trim(' ', '.', '-');
        return string.IsNullOrWhiteSpace(cleaned) ? $"web-resource-{Guid.NewGuid():N}" : cleaned;
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
