// Provides dashboard state for the main workspace landing view.
using System.Collections.ObjectModel;
using System.Windows.Input;
using ReachIT.Application.Contracts;
using ReachIT.Domain.Models;
using ReachIT.Presentation.Commands;
using ReachIT.Presentation.Services;

namespace ReachIT.Presentation.ViewModels;

public sealed class MainDashboardViewModel : ViewModelBase
{
    private readonly IProjectService _projectService;
    private readonly ITaskService _taskService;
    private readonly IStatisticsService _statisticsService;
    private readonly IExternalResourceService _externalResourceService;
    private readonly IFocusModeService _focusModeService;
    private readonly IProjectProgressService _projectProgressService;

    private ProjectDashboardData _dashboardData = new();
    private string _statusText = "No project opened";
    private string _projectName = "No project";
    private string _projectDescription = "Open a .rit project to view dashboard details.";
    private string _projectPath = "-";
    private string _createdAtText = "-";
    private string _lastModifiedText = "-";
    private string _projectState = "Idle";
    private double _overallProgress;
    private int _totalTasks;
    private int _completedTasks;
    private int _activeTasks;
    private int _overdueTasks;
    private bool _isFocusActive;
    private string _focusStatus = "Not started";
    private string _selectedFocusTask = "No focus task selected";
    private string _workedTodayText = "0h 00m";
    private string _workedWeekText = "0h 00m";
    private int _completedTasksToday;
    private int _productivityScore;
    private int _interruptionsCount;
    private int _totalFiles;
    private double _workItemProgressPercent;
    private string _activeMilestoneTitle = "-";
    private int _workItemsInProgress;
    private int _filesChangedToday;
    private double _focusMinutesToday;
    private string _currentWorkContext = string.Empty;
    private string _emptyTaskMessage = "No tasks yet. Create your first task to start planning.";

    public MainDashboardViewModel(
        IProjectService projectService,
        ITaskService taskService,
        IStatisticsService statisticsService,
        IExternalResourceService externalResourceService,
        IFocusModeService focusModeService,
        IProjectProgressService projectProgressService)
    {
        _projectService = projectService;
        _taskService = taskService;
        _statisticsService = statisticsService;
        _externalResourceService = externalResourceService;
        _focusModeService = focusModeService;
        _projectProgressService = projectProgressService;

        RefreshCommand = new AsyncCommand(_ => LoadAsync());
        CreateFirstTaskCommand = new AsyncCommand(_ => CreateTaskAsync());
        NewTaskCommand = new AsyncCommand(_ => CreateTaskAsync());
        NewFolderCommand = new AsyncCommand(_ => CreateFolderAsync());
        AddFileCommand = new AsyncCommand(_ => AddFileAsync());
        AddAllowedAppCommand = new RelayCommand(_ => AddAllowedApplication());
        ToggleFocusModeCommand = new AsyncCommand(_ => ToggleFocusModeAsync());
        OpenSidePanelCommand = new RelayCommand(_ => RequestOpenSidePanel?.Invoke(this, EventArgs.Empty));
        OpenProjectSettingsCommand = new RelayCommand(_ => RequestOpenProjectSettings?.Invoke(this, EventArgs.Empty));

        OpenTaskCommand = new RelayCommand(_ => RequestOpenStatistics?.Invoke(this, EventArgs.Empty));
        OpenActiveTasksCommand = new RelayCommand(_ => RequestOpenActiveTasks?.Invoke(this, EventArgs.Empty));
        EditTaskCommand = new RelayCommand(_ => RequestOpenTaskManager?.Invoke(this, EventArgs.Empty));
        CompleteTaskCommand = new AsyncCommand(p => CompleteTaskAsync(p as DashboardTaskRow));
        DeleteTaskCommand = new AsyncCommand(p => DeleteTaskAsync(p as DashboardTaskRow));
    }

    public event EventHandler? RequestOpenSidePanel;
    public event EventHandler? RequestOpenProjectSettings;
    public event EventHandler? RequestOpenTaskManager;
    public event EventHandler? RequestOpenActiveTasks;
    public event EventHandler? RequestOpenStatistics;
    public event EventHandler? RequestRefreshTree;

    public ProjectDashboardData DashboardData
    {
        get => _dashboardData;
        private set => SetProperty(ref _dashboardData, value);
    }

    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    public string ProjectName
    {
        get => _projectName;
        private set => SetProperty(ref _projectName, value);
    }

    public string ProjectDescription
    {
        get => _projectDescription;
        private set => SetProperty(ref _projectDescription, value);
    }

    public string ProjectPath
    {
        get => _projectPath;
        private set => SetProperty(ref _projectPath, value);
    }

    public string CreatedAtText
    {
        get => _createdAtText;
        private set => SetProperty(ref _createdAtText, value);
    }

    public string LastModifiedText
    {
        get => _lastModifiedText;
        private set => SetProperty(ref _lastModifiedText, value);
    }

    public string ProjectState
    {
        get => _projectState;
        private set => SetProperty(ref _projectState, value);
    }

    public double OverallProgress
    {
        get => _overallProgress;
        private set => SetProperty(ref _overallProgress, value);
    }

    public int TotalTasks
    {
        get => _totalTasks;
        private set => SetProperty(ref _totalTasks, value);
    }

    public int CompletedTasks
    {
        get => _completedTasks;
        private set => SetProperty(ref _completedTasks, value);
    }

    public int ActiveTasks
    {
        get => _activeTasks;
        private set => SetProperty(ref _activeTasks, value);
    }

    public int OverdueTasks
    {
        get => _overdueTasks;
        private set => SetProperty(ref _overdueTasks, value);
    }

    public bool IsFocusActive
    {
        get => _isFocusActive;
        private set => SetProperty(ref _isFocusActive, value);
    }

    public string FocusStatus
    {
        get => _focusStatus;
        private set => SetProperty(ref _focusStatus, value);
    }

    public string SelectedFocusTask
    {
        get => _selectedFocusTask;
        private set => SetProperty(ref _selectedFocusTask, value);
    }

    public string WorkedTodayText
    {
        get => _workedTodayText;
        private set => SetProperty(ref _workedTodayText, value);
    }

    public string WorkedWeekText
    {
        get => _workedWeekText;
        private set => SetProperty(ref _workedWeekText, value);
    }

    public int CompletedTasksToday
    {
        get => _completedTasksToday;
        private set => SetProperty(ref _completedTasksToday, value);
    }

    public int ProductivityScore
    {
        get => _productivityScore;
        private set => SetProperty(ref _productivityScore, value);
    }

    public int InterruptionsCount
    {
        get => _interruptionsCount;
        private set => SetProperty(ref _interruptionsCount, value);
    }

    public int TotalFiles
    {
        get => _totalFiles;
        private set => SetProperty(ref _totalFiles, value);
    }

    public string EmptyTaskMessage
    {
        get => _emptyTaskMessage;
        private set => SetProperty(ref _emptyTaskMessage, value);
    }

    public ObservableCollection<DashboardTaskRow> TaskRows { get; } = new();
    public ObservableCollection<DashboardTaskRow> NearestDeadlines { get; } = new();
    public ObservableCollection<TaskSuggestion> SuggestedTasks { get; } = new();
    public ObservableCollection<WorkItem> StaleWorkItems { get; } = new();
    public ObservableCollection<WorkItem> RecentlyCompletedWorkItems { get; } = new();
    public ObservableCollection<string> AllowedApplications { get; } = new();
    public ObservableCollection<ExternalResourceItem> ExternalFiles { get; } = new();
    public ObservableCollection<string> ProjectItems { get; } = new();
    public ObservableCollection<ProjectActivityEntry> RecentActivity { get; } = new();

    public ICommand RefreshCommand { get; }
    public ICommand CreateFirstTaskCommand { get; }
    public ICommand NewTaskCommand { get; }
    public ICommand NewFolderCommand { get; }
    public ICommand AddFileCommand { get; }
    public ICommand AddAllowedAppCommand { get; }
    public ICommand ToggleFocusModeCommand { get; }
    public ICommand OpenSidePanelCommand { get; }
    public ICommand OpenProjectSettingsCommand { get; }
    public ICommand OpenTaskCommand { get; }
    public ICommand OpenActiveTasksCommand { get; }
    public ICommand EditTaskCommand { get; }
    public ICommand CompleteTaskCommand { get; }
    public ICommand DeleteTaskCommand { get; }

    public double WorkItemProgressPercent
    {
        get => _workItemProgressPercent;
        private set => SetProperty(ref _workItemProgressPercent, value);
    }

    public string ActiveMilestoneTitle
    {
        get => _activeMilestoneTitle;
        private set => SetProperty(ref _activeMilestoneTitle, value);
    }

    public int WorkItemsInProgress
    {
        get => _workItemsInProgress;
        private set => SetProperty(ref _workItemsInProgress, value);
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

    public string CurrentWorkContext
    {
        get => _currentWorkContext;
        private set => SetProperty(ref _currentWorkContext, value);
    }

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        var project = _projectService.CurrentProject;
        if (project is null)
        {
            ApplyNoProjectState();
            return;
        }

        var tasks = await _taskService.GetTasksAsync(cancellationToken).ConfigureAwait(true);
        var stats = await _statisticsService.GetProjectStatsAsync(cancellationToken).ConfigureAwait(true);
        var productivity = await _statisticsService.GetProductivityStatsAsync(cancellationToken).ConfigureAwait(true);
        var externalResources = await _externalResourceService.GetByProjectAsync(project.Id, cancellationToken).ConfigureAwait(true);
        var focusSession = await _focusModeService.GetCurrentSessionAsync(cancellationToken).ConfigureAwait(true);
        var tree = await _projectService.GetCurrentTreeAsync(cancellationToken).ConfigureAwait(true);
        var progressSnapshot = await _projectProgressService.GetSnapshotAsync(project, cancellationToken).ConfigureAwait(true);

        var projectItems = FlattenProjectItems(tree).Take(12).ToList();
        var taskRows = tasks.Select(MapTask).ToList();
        var nearestDeadlines = taskRows
            .Where(x => x.DeadlineUtc.HasValue && !x.IsCompleted)
            .OrderBy(x => x.DeadlineUtc)
            .Take(5)
            .ToList();

        var completedTasks = taskRows.Count(x => x.IsCompleted);
        var overdueTasks = taskRows.Count(x => x.IsOverdue);
        var activeTasks = taskRows.Count - completedTasks;

        var productivityToday = productivity.FirstOrDefault(x => x.DateUtc.Date == DateTime.UtcNow.Date);
        var workedWeekHours = productivity
            .Where(x => x.DateUtc.Date >= DateTime.UtcNow.Date.AddDays(-6))
            .Sum(x => x.FocusHours);

        var focusApps = focusSession?.AllowedApplications.Count > 0
            ? focusSession.AllowedApplications
            : ["ReachIT"];

        var activity = BuildRecentActivity(taskRows, externalResources, focusSession);

        DashboardData = new ProjectDashboardData
        {
            Project = project,
            Stats = stats,
            Tasks = tasks.ToList(),
            Productivity = productivity.ToList(),
            FocusSession = focusSession,
            AllowedApplications = focusApps.ToList(),
            ExternalResources = externalResources.ToList(),
            ProjectItems = projectItems,
            RecentActivity = activity,
            LoadedAtUtc = DateTime.UtcNow
        };

        ProjectName = string.IsNullOrWhiteSpace(project.ProjectName) ? "Unnamed project" : project.ProjectName;
        ProjectDescription = string.IsNullOrWhiteSpace(project.Description)
            ? LocalizationService.GetString("Dashboard.NoDescription", "No description added yet.")
            : project.Description;
        ProjectPath = project.ProjectDirectoryPath;
        CreatedAtText = project.CreatedAtUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
        LastModifiedText = project.UpdatedAtUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm");

        TotalTasks = taskRows.Count;
        CompletedTasks = completedTasks;
        ActiveTasks = activeTasks;
        OverdueTasks = overdueTasks;
        WorkItemProgressPercent = progressSnapshot.ProjectProgressPercent;
        OverallProgress = WorkItemProgressPercent > 0
            ? WorkItemProgressPercent
            : TotalTasks == 0 ? 0 : Math.Round((double)CompletedTasks / TotalTasks * 100d, 1);
        ActiveMilestoneTitle = progressSnapshot.ActiveMilestone?.Title ?? "-";
        WorkItemsInProgress = progressSnapshot.WorkItemsInProgress;
        FilesChangedToday = progressSnapshot.FilesChangedToday;
        FocusMinutesToday = progressSnapshot.FocusMinutesToday;
        CurrentWorkContext = progressSnapshot.CurrentWorkContext;
        ProjectState = TotalTasks == 0
            ? "Not started"
            : ActiveTasks == 0
                ? "Completed"
                : OverdueTasks > 0
                    ? "Needs attention"
                    : "In progress";

        StatusText = $"Dashboard updated at {DashboardData.LoadedAtUtc.ToLocalTime():HH:mm:ss}";

        IsFocusActive = _focusModeService.IsActive;
        FocusStatus = IsFocusActive ? $"Active ({_focusModeService.CurrentMode})" : "Inactive";
        SelectedFocusTask = nearestDeadlines.FirstOrDefault()?.Title ?? "No focus task selected";

        WorkedTodayText = FormatHours(productivityToday?.FocusHours ?? 0);
        WorkedWeekText = FormatHours(workedWeekHours);
        CompletedTasksToday = productivityToday?.CompletedTasks ?? 0;
        ProductivityScore = Math.Clamp((int)Math.Round((CompletedTasksToday * 18) + (workedWeekHours * 6) - (overdueTasks * 4)), 0, 100);
        InterruptionsCount = Math.Max(0, activeTasks - CompletedTasksToday);
        TotalFiles = stats.TotalFiles > 0 ? stats.TotalFiles : CountFileNodes(tree);
        EmptyTaskMessage = TaskRows.Count == 0
            ? "No tasks yet. Create your first task to start planning."
            : string.Empty;

        SyncCollection(TaskRows, taskRows);
        SyncCollection(NearestDeadlines, nearestDeadlines);
        SyncCollection(SuggestedTasks, progressSnapshot.SuggestedTasks);
        SyncCollection(StaleWorkItems, progressSnapshot.StaleTasks);
        SyncCollection(RecentlyCompletedWorkItems, progressSnapshot.RecentlyCompletedTasks);
        SyncCollection(AllowedApplications, focusApps);
        SyncCollection(ExternalFiles, externalResources);
        SyncCollection(ProjectItems, projectItems);
        SyncCollection(RecentActivity, activity);
    }

    private async Task CreateTaskAsync()
    {
        await _taskService.AddTaskAsync(new TaskItem
        {
            Title = LocalizationService.GetString("Dashboard.NewTaskTitle", "New Task"),
            Description = LocalizationService.GetString("Dashboard.NewTaskDescription", "Define task details"),
            DueDateUtc = DateTime.UtcNow.AddDays(1),
            IsCompleted = false
        }).ConfigureAwait(true);

        await LoadAsync().ConfigureAwait(true);
    }

    private async Task CreateFolderAsync()
    {
        await _projectService.CreateFolderAsync(null).ConfigureAwait(true);
        RequestRefreshTree?.Invoke(this, EventArgs.Empty);
        await LoadAsync().ConfigureAwait(true);
    }

    private async Task AddFileAsync()
    {
        await _projectService.CreateInternalFileAsync(null).ConfigureAwait(true);
        RequestRefreshTree?.Invoke(this, EventArgs.Empty);
        await LoadAsync().ConfigureAwait(true);
    }

    private void AddAllowedApplication()
    {
        const string sample = "AllowedApp.exe";
        if (!AllowedApplications.Contains(sample, StringComparer.OrdinalIgnoreCase))
        {
            AllowedApplications.Add(sample);
        }
    }

    private async Task ToggleFocusModeAsync()
    {
        if (_focusModeService.IsActive)
        {
            await _focusModeService.StopAsync().ConfigureAwait(true);
        }
        else
        {
            await _focusModeService.StartAsync().ConfigureAwait(true);
        }

        await LoadAsync().ConfigureAwait(true);
    }

    private async Task CompleteTaskAsync(DashboardTaskRow? task)
    {
        if (task is null || task.IsCompleted)
        {
            return;
        }

        task.SourceTask.IsCompleted = true;
        await _taskService.UpdateTaskAsync(task.SourceTask).ConfigureAwait(true);
        await LoadAsync().ConfigureAwait(true);
    }

    private async Task DeleteTaskAsync(DashboardTaskRow? task)
    {
        if (task is null)
        {
            return;
        }

        await _taskService.DeleteTaskAsync(task.SourceTask.Id).ConfigureAwait(true);
        await LoadAsync().ConfigureAwait(true);
    }

    private void ApplyNoProjectState()
    {
        DashboardData = new ProjectDashboardData();
        ProjectName = "No project";
        ProjectDescription = "Open a .rit project to view dashboard details.";
        ProjectPath = "-";
        CreatedAtText = "-";
        LastModifiedText = "-";
        ProjectState = "Idle";
        OverallProgress = 0;
        TotalTasks = 0;
        CompletedTasks = 0;
        ActiveTasks = 0;
        OverdueTasks = 0;
        IsFocusActive = false;
        FocusStatus = "Inactive";
        SelectedFocusTask = "No focus task selected";
        WorkedTodayText = "0h 00m";
        WorkedWeekText = "0h 00m";
        CompletedTasksToday = 0;
        ProductivityScore = 0;
        InterruptionsCount = 0;
        TotalFiles = 0;
        WorkItemProgressPercent = 0;
        ActiveMilestoneTitle = "-";
        WorkItemsInProgress = 0;
        FilesChangedToday = 0;
        FocusMinutesToday = 0;
        CurrentWorkContext = string.Empty;
        StatusText = "Open a .rit project to initialize dashboard data.";
        EmptyTaskMessage = "No tasks yet. Create your first task to start planning.";

        TaskRows.Clear();
        NearestDeadlines.Clear();
        SuggestedTasks.Clear();
        StaleWorkItems.Clear();
        RecentlyCompletedWorkItems.Clear();
        AllowedApplications.Clear();
        ExternalFiles.Clear();
        ProjectItems.Clear();
        RecentActivity.Clear();
    }

    private static DashboardTaskRow MapTask(TaskItem task)
    {
        var due = task.DueDateUtc?.ToLocalTime();
        var isOverdue = !task.IsCompleted && due.HasValue && due.Value < DateTime.Now;
        var status = task.IsCompleted
            ? "Completed"
            : isOverdue
                ? "Overdue"
                : "Active";

        var priority = !due.HasValue
            ? "Normal"
            : due.Value.Date <= DateTime.Now.Date.AddDays(1)
                ? "High"
                : due.Value.Date <= DateTime.Now.Date.AddDays(4)
                    ? "Medium"
                    : "Low";

        return new DashboardTaskRow
        {
            SourceTask = task,
            Title = task.Title,
            Status = status,
            Priority = priority,
            DeadlineText = due?.ToString("yyyy-MM-dd HH:mm") ?? "No deadline",
            DeadlineUtc = task.DueDateUtc,
            Progress = task.IsCompleted ? 100 : 0,
            IsCompleted = task.IsCompleted,
            IsOverdue = isOverdue
        };
    }

    private static string FormatHours(double hours)
    {
        var ts = TimeSpan.FromHours(Math.Max(0, hours));
        return $"{(int)ts.TotalHours}h {ts.Minutes:D2}m";
    }

    private static int CountFileNodes(IEnumerable<ProjectTreeNode> nodes)
    {
        var count = 0;
        foreach (var node in nodes)
        {
            if (!node.IsDirectory)
            {
                count++;
            }

            count += CountFileNodes(node.Children);
        }

        return count;
    }

    private static List<string> FlattenProjectItems(IEnumerable<ProjectTreeNode> nodes)
    {
        var list = new List<string>();
        foreach (var node in nodes)
        {
            list.Add(node.FullPath);
            list.AddRange(FlattenProjectItems(node.Children));
        }

        return list;
    }

    private static List<ProjectActivityEntry> BuildRecentActivity(
        IEnumerable<DashboardTaskRow> tasks,
        IEnumerable<ExternalResourceItem> externalResources,
        FocusSession? focusSession)
    {
        var activity = new List<ProjectActivityEntry>();

        foreach (var task in tasks.Take(5))
        {
            activity.Add(new ProjectActivityEntry
            {
                TimestampUtc = task.DeadlineUtc ?? DateTime.UtcNow,
                Category = task.IsCompleted ? "Completed tasks" : "Created tasks",
                Description = task.Title
            });
        }

        foreach (var external in externalResources.Take(4))
        {
            activity.Add(new ProjectActivityEntry
            {
                TimestampUtc = external.AddedAtUtc,
                Category = "Opened files",
                Description = external.DisplayName
            });
        }

        if (focusSession is not null)
        {
            activity.Add(new ProjectActivityEntry
            {
                TimestampUtc = focusSession.StartedAtUtc,
                Category = "Focus Mode events",
                Description = focusSession.IsActive
                    ? $"Focus started ({focusSession.ModeType})"
                    : $"Focus stopped ({focusSession.ModeType})"
            });
        }

        if (activity.Count == 0)
        {
            activity.Add(new ProjectActivityEntry
            {
                TimestampUtc = DateTime.UtcNow,
                Category = "Activity",
                Description = "No activity yet. Start by creating a task or adding files."
            });
        }

        return activity
            .OrderByDescending(x => x.TimestampUtc)
            .Take(10)
            .ToList();
    }

    private static void SyncCollection<T>(ObservableCollection<T> target, IEnumerable<T> source)
    {
        target.Clear();
        foreach (var item in source)
        {
            target.Add(item);
        }
    }
}

public sealed class DashboardTaskRow
{
    public required TaskItem SourceTask { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string Priority { get; init; } = string.Empty;
    public string DeadlineText { get; init; } = string.Empty;
    public DateTime? DeadlineUtc { get; init; }
    public int Progress { get; init; }
    public bool IsCompleted { get; init; }
    public bool IsOverdue { get; init; }
}
