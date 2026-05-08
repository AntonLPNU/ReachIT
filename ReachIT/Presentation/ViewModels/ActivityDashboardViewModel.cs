using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Input;
using ReachIT.Application.Contracts;
using ReachIT.Domain.Enums;
using ReachIT.Domain.Models;
using ReachIT.Presentation.Commands;

namespace ReachIT.Presentation.ViewModels;

public sealed class ActivityDashboardViewModel : ViewModelBase
{
    private readonly IProjectService _projectService;
    private readonly IActivityDashboardService _activityDashboardService;
    private readonly IActivityMonitorService _activityMonitorService;
    private readonly IActivityRepository _activityRepository;
    private string _nowWorkingOn = "-";
    private string _activeApp = "-";
    private string _currentTaskGuess = "-";
    private string _contextReason = string.Empty;
    private int _productivityScore;
    private int _focusScore;
    private int _distractionScore;
    private int _progressScore;
    private int _filesChangedToday;
    private double _focusMinutesToday;
    private int _interruptionsToday;
    private bool _isDistracted;
    private string _productivityExplanation = string.Empty;

    public ActivityDashboardViewModel(
        IProjectService projectService,
        IActivityDashboardService activityDashboardService,
        IActivityMonitorService activityMonitorService,
        IActivityRepository activityRepository)
    {
        _projectService = projectService;
        _activityDashboardService = activityDashboardService;
        _activityMonitorService = activityMonitorService;
        _activityRepository = activityRepository;
        RefreshCommand = new AsyncCommand(_ => LoadAsync());
        PauseTrackingCommand = new AsyncCommand(_ => _activityMonitorService.StopAsync());
        ResumeTrackingCommand = new AsyncCommand(_ => ResumeAsync());
        ClearHistoryCommand = new AsyncCommand(_ => ClearHistoryAsync());
    }

    public ObservableCollection<ActivityTimelineRow> Timeline { get; } = new();
    public ObservableCollection<TaskSuggestion> SuggestedTaskLinks { get; } = new();
    public ObservableCollection<TaskSuggestion> SuggestedCompletedTasks { get; } = new();
    public ObservableCollection<string> RecentFiles { get; } = new();

    public string NowWorkingOn
    {
        get => _nowWorkingOn;
        private set => SetProperty(ref _nowWorkingOn, value);
    }

    public string ActiveApp
    {
        get => _activeApp;
        private set => SetProperty(ref _activeApp, value);
    }

    public string CurrentTaskGuess
    {
        get => _currentTaskGuess;
        private set => SetProperty(ref _currentTaskGuess, value);
    }

    public string ContextReason
    {
        get => _contextReason;
        private set => SetProperty(ref _contextReason, value);
    }

    public int ProductivityScore
    {
        get => _productivityScore;
        private set => SetProperty(ref _productivityScore, value);
    }

    public int FocusScore
    {
        get => _focusScore;
        private set => SetProperty(ref _focusScore, value);
    }

    public int DistractionScore
    {
        get => _distractionScore;
        private set => SetProperty(ref _distractionScore, value);
    }

    public int ProgressScore
    {
        get => _progressScore;
        private set => SetProperty(ref _progressScore, value);
    }

    public int FilesChangedToday
    {
        get => _filesChangedToday;
        private set => SetProperty(ref _filesChangedToday, value);
    }

    public double FocusMinutesToday
    {
        get => _focusMinutesToday;
        private set => SetProperty(ref _focusMinutesToday, value);
    }

    public int InterruptionsToday
    {
        get => _interruptionsToday;
        private set => SetProperty(ref _interruptionsToday, value);
    }

    public bool IsDistracted
    {
        get => _isDistracted;
        private set => SetProperty(ref _isDistracted, value);
    }

    public string ProductivityExplanation
    {
        get => _productivityExplanation;
        private set => SetProperty(ref _productivityExplanation, value);
    }

    public ICommand RefreshCommand { get; }
    public ICommand PauseTrackingCommand { get; }
    public ICommand ResumeTrackingCommand { get; }
    public ICommand ClearHistoryCommand { get; }

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        var project = _projectService.CurrentProject;
        if (project is null)
        {
            ApplyNoProjectState();
            return;
        }

        try
        {
            var snapshot = await _activityDashboardService.GetSnapshotAsync(project, cancellationToken).ConfigureAwait(true);
            NowWorkingOn = snapshot.CurrentContext.ActiveFile is null ? project.ProjectName : Path.GetFileName(snapshot.CurrentContext.ActiveFile);
            ActiveApp = string.IsNullOrWhiteSpace(snapshot.CurrentContext.ActiveApp) ? "-" : snapshot.CurrentContext.ActiveApp;
            CurrentTaskGuess = snapshot.CurrentContext.LikelyWorkItemId.HasValue
                ? snapshot.CurrentContext.LikelyWorkItemId.Value.ToString("N")[..8]
                : "-";
            ContextReason = snapshot.CurrentContext.Reason;
            IsDistracted = snapshot.CurrentContext.IsDistracted;
            ProductivityScore = snapshot.Productivity.ProductivityScore;
            FocusScore = snapshot.Productivity.FocusScore;
            DistractionScore = snapshot.Productivity.DistractionScore;
            ProgressScore = snapshot.Productivity.ProgressScore;
            FilesChangedToday = snapshot.FilesChangedToday;
            FocusMinutesToday = snapshot.FocusMinutesToday;
            InterruptionsToday = snapshot.InterruptionsToday;
            ProductivityExplanation = snapshot.Productivity.Explanation;

            Sync(RecentFiles, snapshot.CurrentContext.RecentFiles
                .Select(GetSafeFileName)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x!));
            Sync(SuggestedTaskLinks, snapshot.SuggestedTaskLinks);
            Sync(SuggestedCompletedTasks, snapshot.SuggestedCompletedTasks);
            Sync(Timeline, snapshot.RecentEvents.Select(MapTimeline));
        }
        catch (Exception ex)
        {
            ApplyActivityErrorState(ex);
        }
    }

    private async Task ResumeAsync()
    {
        if (_projectService.CurrentProject is not null)
        {
            await _activityMonitorService.StartAsync(_projectService.CurrentProject).ConfigureAwait(true);
            await LoadAsync().ConfigureAwait(true);
        }
    }

    private async Task ClearHistoryAsync()
    {
        if (_projectService.CurrentProject is not null)
        {
            await _activityRepository.ClearProjectAsync(_projectService.CurrentProject.Id).ConfigureAwait(true);
            await LoadAsync().ConfigureAwait(true);
        }
    }

    private void ApplyNoProjectState()
    {
        NowWorkingOn = "-";
        ActiveApp = "-";
        CurrentTaskGuess = "-";
        ContextReason = "Open a project to start tracking activity.";
        ProductivityScore = 0;
        FocusScore = 0;
        DistractionScore = 100;
        ProgressScore = 0;
        FilesChangedToday = 0;
        FocusMinutesToday = 0;
        InterruptionsToday = 0;
        ProductivityExplanation = string.Empty;
        Timeline.Clear();
        SuggestedTaskLinks.Clear();
        SuggestedCompletedTasks.Clear();
        RecentFiles.Clear();
    }

    private void ApplyActivityErrorState(Exception ex)
    {
        NowWorkingOn = _projectService.CurrentProject?.ProjectName ?? "-";
        ActiveApp = "-";
        CurrentTaskGuess = "-";
        ContextReason = $"Activity dashboard could not load: {ex.Message}";
        ProductivityScore = 0;
        FocusScore = 0;
        DistractionScore = 0;
        ProgressScore = 0;
        FilesChangedToday = 0;
        FocusMinutesToday = 0;
        InterruptionsToday = 0;
        ProductivityExplanation = string.Empty;
        Timeline.Clear();
        SuggestedTaskLinks.Clear();
        SuggestedCompletedTasks.Clear();
        RecentFiles.Clear();
    }

    private static ActivityTimelineRow MapTimeline(ActivityEvent e)
    {
        var subject = e.EventType switch
        {
            ActivityEventType.AppActivated or ActivityEventType.WindowChanged or ActivityEventType.AllowedAppUsed or ActivityEventType.DistractingAppUsed => e.AppName,
            ActivityEventType.FileChanged or ActivityEventType.FileCreated or ActivityEventType.FileDeleted or ActivityEventType.FileRenamed or ActivityEventType.TextChanged => GetSafeFileName(e.FilePath),
            ActivityEventType.GitChanged => "Git",
            _ => e.EventType.ToString()
        };

        return new ActivityTimelineRow
        {
            Time = e.Timestamp.ToLocalTime().ToString("HH:mm"),
            EventType = e.EventType.ToString(),
            Subject = string.IsNullOrWhiteSpace(subject) ? "-" : subject!,
            Detail = e.WindowTitle
        };
    }

    private static string? GetSafeFileName(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        try
        {
            return Path.GetFileName(path);
        }
        catch (ArgumentException)
        {
            return path;
        }
    }

    private static void Sync<T>(ObservableCollection<T> target, IEnumerable<T> source)
    {
        target.Clear();
        foreach (var item in source)
        {
            target.Add(item);
        }
    }
}

public sealed class ActivityTimelineRow
{
    public string Time { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Detail { get; set; } = string.Empty;
}
