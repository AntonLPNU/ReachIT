using System.IO;
using ReachIT.Application.Contracts;
using ReachIT.Domain.Enums;
using ReachIT.Domain.Models;

namespace ReachIT.Application.Services;

public sealed class WorkContextDetectorService : IWorkContextDetectorService
{
    private readonly IActivityRepository _activityRepository;
    private readonly IWorkItemRepository _workItemRepository;
    private readonly IForegroundWindowService _foregroundWindowService;
    private readonly IAppSettingsService _settingsService;

    public WorkContextDetectorService(
        IActivityRepository activityRepository,
        IWorkItemRepository workItemRepository,
        IForegroundWindowService foregroundWindowService,
        IAppSettingsService settingsService)
    {
        _activityRepository = activityRepository;
        _workItemRepository = workItemRepository;
        _foregroundWindowService = foregroundWindowService;
        _settingsService = settingsService;
    }

    public async Task<CurrentWorkContext> DetectAsync(ProjectMeta project, CancellationToken cancellationToken = default)
    {
        var recent = await _activityRepository.GetRecentAsync(project.Id, 40, cancellationToken).ConfigureAwait(false);
        var recentFiles = recent
            .Where(x => !string.IsNullOrWhiteSpace(x.FilePath))
            .Select(x => x.FilePath!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(6)
            .ToList();

        var active = _foregroundWindowService.GetCurrent();
        var latestFile = recentFiles.FirstOrDefault();
        var workItems = await _workItemRepository.GetByProjectAsync(project.Id, cancellationToken).ConfigureAwait(false);
        var likelyItem = FindLinkedWorkItem(workItems, latestFile, active);
        var settings = await _settingsService.GetAsync(cancellationToken).ConfigureAwait(false);
        var distracting = IsDistracting(active.ProcessName, settings.AllowedApplications);

        return new CurrentWorkContext
        {
            ProjectId = project.Id,
            LikelyWorkItemId = likelyItem?.Id,
            Confidence = likelyItem is null ? 0.35 : 0.82,
            Reason = likelyItem is null
                ? latestFile is null ? "No recent project file activity yet." : $"Recent activity on {Path.GetFileName(latestFile)} has no linked task yet."
                : $"Matched recent activity to linked work item '{likelyItem.Title}'.",
            ActiveApp = active.AppName,
            ActiveFile = latestFile,
            RecentFiles = recentFiles,
            IsDistracted = distracting
        };
    }

    private static WorkItem? FindLinkedWorkItem(IEnumerable<WorkItem> workItems, string? latestFile, ForegroundWindowSnapshot active)
    {
        var latestFullPath = TryGetFullPath(latestFile);
        if (latestFullPath is not null)
        {
            var exact = workItems.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x.LinkedPath) &&
                string.Equals(TryGetFullPath(x.LinkedPath), latestFullPath, StringComparison.OrdinalIgnoreCase));
            if (exact is not null)
            {
                return exact;
            }

            var folder = workItems
                .Where(x => !string.IsNullOrWhiteSpace(x.LinkedPath) && IsExistingDirectory(x.LinkedPath))
                .OrderByDescending(x => x.LinkedPath!.Length)
                .FirstOrDefault(x =>
                {
                    var linkedFullPath = TryGetFullPath(x.LinkedPath);
                    return linkedFullPath is not null &&
                           latestFullPath.StartsWith(linkedFullPath, StringComparison.OrdinalIgnoreCase);
                });
            if (folder is not null)
            {
                return folder;
            }
        }

        return workItems.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x.LinkedApp) &&
            active.ProcessName.Contains(x.LinkedApp, StringComparison.OrdinalIgnoreCase));
    }

    private static string? TryGetFullPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        try
        {
            return Path.GetFullPath(path);
        }
        catch (ArgumentException)
        {
            return null;
        }
        catch (NotSupportedException)
        {
            return null;
        }
    }

    private static bool IsExistingDirectory(string? path)
    {
        try
        {
            return !string.IsNullOrWhiteSpace(path) && Directory.Exists(path);
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (NotSupportedException)
        {
            return false;
        }
    }

    private static bool IsDistracting(string processName, IReadOnlyCollection<string> allowedApps)
    {
        if (string.IsNullOrWhiteSpace(processName))
        {
            return false;
        }

        if (allowedApps.Any(app => processName.Contains(app, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        return processName.Contains("youtube", StringComparison.OrdinalIgnoreCase)
               || processName.Contains("telegram", StringComparison.OrdinalIgnoreCase)
               || processName.Contains("discord", StringComparison.OrdinalIgnoreCase)
               || processName.Contains("spotify", StringComparison.OrdinalIgnoreCase);
    }
}
