using System.IO;
using System.Text.Json;
using System.Timers;
using ReachIT.Application.Contracts;
using ReachIT.Domain.Enums;
using ReachIT.Domain.Models;

namespace ReachIT.Application.Services;

public sealed class ActivityMonitorService : IActivityMonitorService
{
    private readonly IActivityRepository _activityRepository;
    private readonly IForegroundWindowService _foregroundWindowService;
    private readonly IFileActivityWatcherService _fileWatcherService;
    private readonly IGitActivityService _gitActivityService;
    private readonly IAppSettingsService _settingsService;
    private readonly IFocusModeService _focusModeService;
    private readonly IWorkUnitService _workUnitService;
    private readonly ITaskSuggestionService _taskSuggestionService;
    private readonly System.Timers.Timer _windowTimer;
    private readonly System.Timers.Timer _gitTimer;
    private readonly SemaphoreSlim _eventGate = new(1, 1);
    private AppSettings _settings = new();
    private ProjectMeta? _project;
    private ForegroundWindowSnapshot? _lastWindow;
    private DateTime _lastWindowSeenUtc = DateTime.UtcNow;
    private bool _disposed;

    public ActivityMonitorService(
        IActivityRepository activityRepository,
        IForegroundWindowService foregroundWindowService,
        IFileActivityWatcherService fileWatcherService,
        IGitActivityService gitActivityService,
        IAppSettingsService settingsService,
        IFocusModeService focusModeService,
        IWorkUnitService workUnitService,
        ITaskSuggestionService taskSuggestionService)
    {
        _activityRepository = activityRepository;
        _foregroundWindowService = foregroundWindowService;
        _fileWatcherService = fileWatcherService;
        _gitActivityService = gitActivityService;
        _settingsService = settingsService;
        _focusModeService = focusModeService;
        _workUnitService = workUnitService;
        _taskSuggestionService = taskSuggestionService;

        _windowTimer = new System.Timers.Timer(5000) { AutoReset = true };
        _windowTimer.Elapsed += OnWindowTimer;
        _gitTimer = new System.Timers.Timer(TimeSpan.FromMinutes(2).TotalMilliseconds) { AutoReset = true };
        _gitTimer.Elapsed += OnGitTimer;
        _fileWatcherService.ActivityDetected += OnFileActivityDetected;
    }

    public bool IsRunning => _project is not null;

    public async Task StartAsync(ProjectMeta project, CancellationToken cancellationToken = default)
    {
        await StopAsync(cancellationToken).ConfigureAwait(false);
        _settings = await _settingsService.GetAsync(cancellationToken).ConfigureAwait(false);
        _project = project;
        var trackingSettings = ResolveTrackingSettings(project, _settings);

        if (!trackingSettings.EnableActivityTracking || trackingSettings.PauseActivityTracking)
        {
            return;
        }

        if (trackingSettings.TrackFileChanges)
        {
            _fileWatcherService.Start(project, trackingSettings.IgnoredFolders, trackingSettings.TrackTextStatistics);
        }

        if (trackingSettings.TrackActiveWindow)
        {
            _lastWindow = _foregroundWindowService.GetCurrent();
            _lastWindowSeenUtc = DateTime.UtcNow;
            _windowTimer.Start();
        }

        if (trackingSettings.TrackGitChanges)
        {
            _gitTimer.Start();
        }
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        _windowTimer.Stop();
        _gitTimer.Stop();
        _fileWatcherService.Stop();
        _project = null;
        _lastWindow = null;
        return Task.CompletedTask;
    }

    public async Task ReloadSettingsAsync(CancellationToken cancellationToken = default)
    {
        var project = _project;
        _settings = await _settingsService.GetAsync(cancellationToken).ConfigureAwait(false);
        if (project is not null)
        {
            await StartAsync(project, cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task RecordManualEventAsync(ProjectMeta project, ActivityEventType eventType, string metadataJson = "{}", CancellationToken cancellationToken = default)
    {
        await RecordAsync(new ActivityEvent
        {
            ProjectId = project.Id,
            Timestamp = DateTime.UtcNow,
            EventType = eventType,
            MetadataJson = string.IsNullOrWhiteSpace(metadataJson) ? "{}" : metadataJson
        }, cancellationToken).ConfigureAwait(false);
    }

    private void OnFileActivityDetected(object? sender, ActivityEvent e)
    {
        _ = RecordAsync(e, CancellationToken.None);
    }

    private void OnWindowTimer(object? sender, ElapsedEventArgs e)
    {
        _ = CaptureWindowAsync();
    }

    private void OnGitTimer(object? sender, ElapsedEventArgs e)
    {
        _ = CaptureGitAsync();
    }

    private async Task CaptureWindowAsync()
    {
        var project = _project;
        if (project is null || !ResolveTrackingSettings(project, _settings).TrackActiveWindow)
        {
            return;
        }

        var current = _foregroundWindowService.GetCurrent();
        if (IsPrivateApp(current))
        {
            current = current with { WindowTitle = string.Empty };
        }

        var now = DateTime.UtcNow;
        var duration = Math.Max(0, (int)(now - _lastWindowSeenUtc).TotalSeconds);
        var changed = _lastWindow is null ||
            !string.Equals(_lastWindow.ProcessName, current.ProcessName, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(_lastWindow.WindowTitle, current.WindowTitle, StringComparison.Ordinal);

        if (changed)
        {
            await RecordAsync(CreateWindowEvent(project, current, ActivityEventType.WindowChanged, null), CancellationToken.None).ConfigureAwait(false);
        }
        else if (duration >= 5)
        {
            var eventType = _focusModeService.IsActive
                ? IsAllowedApp(current.ProcessName)
                    ? ActivityEventType.AllowedAppUsed
                    : ActivityEventType.DistractingAppUsed
                : ActivityEventType.AppActivated;

            await RecordAsync(CreateWindowEvent(project, current, eventType, duration), CancellationToken.None).ConfigureAwait(false);
        }

        _lastWindow = current;
        _lastWindowSeenUtc = now;
    }

    private async Task CaptureGitAsync()
    {
        var project = _project;
        if (project is null || !ResolveTrackingSettings(project, _settings).TrackGitChanges)
        {
            return;
        }

        var events = await _gitActivityService.ScanAsync(project, CancellationToken.None).ConfigureAwait(false);
        foreach (var activityEvent in events)
        {
            await RecordAsync(activityEvent, CancellationToken.None).ConfigureAwait(false);
        }
    }

    private ActivityEvent CreateWindowEvent(ProjectMeta project, ForegroundWindowSnapshot snapshot, ActivityEventType eventType, int? durationSeconds)
    {
        return new ActivityEvent
        {
            ProjectId = project.Id,
            Timestamp = DateTime.UtcNow,
            EventType = eventType,
            AppName = snapshot.AppName,
            ProcessName = snapshot.ProcessName,
            WindowTitle = snapshot.WindowTitle,
            DurationSeconds = durationSeconds,
            MetadataJson = JsonSerializer.Serialize(new { snapshot.ProcessId })
        };
    }

    private async Task RecordAsync(ActivityEvent activityEvent, CancellationToken cancellationToken)
    {
        if (_project is null)
        {
            return;
        }

        var trackingSettings = ResolveTrackingSettings(_project, _settings);
        if (!trackingSettings.EnableActivityTracking || trackingSettings.PauseActivityTracking)
        {
            return;
        }

        await _eventGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            activityEvent.Timestamp = activityEvent.Timestamp == default ? DateTime.UtcNow : activityEvent.Timestamp;
            await _activityRepository.AddAsync(activityEvent, cancellationToken).ConfigureAwait(false);

            if (activityEvent.EventType is ActivityEventType.FileChanged or ActivityEventType.FileCreated or ActivityEventType.TextChanged)
            {
                var path = activityEvent.FilePath ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(path))
                {
                    if (_focusModeService.IsActive)
                    {
                        await _workUnitService.RecordFileActivityAsync(_project, path, cancellationToken).ConfigureAwait(false);
                    }

                    await _taskSuggestionService.GenerateFromActivityAsync(_project, cancellationToken).ConfigureAwait(false);
                }
            }

            var durationSeconds = activityEvent.DurationSeconds.GetValueOrDefault();
            if (_focusModeService.IsActive && activityEvent.EventType == ActivityEventType.AllowedAppUsed && durationSeconds > 0)
            {
                await _workUnitService.AddAsync(
                    _project.Id,
                    activityEvent.WorkItemId,
                    WorkUnitType.FocusMinutes,
                    durationSeconds / 60d,
                    "activity_monitor",
                    activityEvent.MetadataJson,
                    cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            _eventGate.Release();
        }
    }

    private bool IsAllowedApp(string processName)
    {
        return _settings.AllowedApplications.Count == 0 ||
               _settings.AllowedApplications.Any(app => processName.Contains(app, StringComparison.OrdinalIgnoreCase));
    }

    private bool IsPrivateApp(ForegroundWindowSnapshot snapshot)
    {
        return _settings.IgnorePrivateApps &&
               _settings.PrivateApps.Any(app => snapshot.ProcessName.Contains(app, StringComparison.OrdinalIgnoreCase));
    }

    private static EffectiveTrackingSettings ResolveTrackingSettings(ProjectMeta project, AppSettings settings)
    {
        if (!project.UseProjectActivitySettings)
        {
            return new EffectiveTrackingSettings(
                settings.EnableActivityTracking,
                settings.TrackActiveWindow,
                settings.TrackFileChanges,
                settings.TrackGitChanges,
                settings.TrackTextStatistics,
                settings.PauseActivityTracking,
                settings.IgnoredFolders);
        }

        return new EffectiveTrackingSettings(
            project.ProjectEnableActivityTracking,
            project.ProjectTrackActiveWindow,
            project.ProjectTrackFileChanges,
            project.ProjectTrackGitChanges,
            project.ProjectTrackTextStatistics,
            project.ProjectPauseActivityTracking,
            project.ProjectIgnoredFolders);
    }

    private sealed record EffectiveTrackingSettings(
        bool EnableActivityTracking,
        bool TrackActiveWindow,
        bool TrackFileChanges,
        bool TrackGitChanges,
        bool TrackTextStatistics,
        bool PauseActivityTracking,
        IReadOnlyCollection<string> IgnoredFolders);

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _fileWatcherService.ActivityDetected -= OnFileActivityDetected;
        _windowTimer.Dispose();
        _gitTimer.Dispose();
        _fileWatcherService.Dispose();
        _eventGate.Dispose();
    }
}
