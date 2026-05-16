// Provides dashboard state for the main workspace landing view.
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.VisualBasic;
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
    private readonly IGitService _gitService;

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
    private double _projectHealthScore;
    private double _completedTasksPercent;
    private double _activeTasksPercent;
    private double _overdueTasksPercent;
    private double _focusTodayPercent;
    private double _fileActivityPercent;
    private double _completedTasksChartHeight;
    private double _activeTasksChartHeight;
    private double _overdueTasksChartHeight;
    private double _focusChartHeight;
    private double _fileActivityChartHeight;
    private double _healthChartHeight;
    private double _progressChartHeight;
    private double _productivityChartHeight;
    private PointCollection _projectTrendPoints = new();
    private string _activeMilestoneTitle = "-";
    private int _workItemsInProgress;
    private int _filesChangedToday;
    private double _focusMinutesToday;
    private string _currentWorkContext = string.Empty;
    private string _emptyTaskMessage = "No tasks yet. Create your first task to start planning.";
    private string _gitStatusText = "Open a project to use Git controls.";

    public MainDashboardViewModel(
        IProjectService projectService,
        ITaskService taskService,
        IStatisticsService statisticsService,
        IExternalResourceService externalResourceService,
        IFocusModeService focusModeService,
        IProjectProgressService projectProgressService,
        IGitService gitService)
    {
        _projectService = projectService;
        _taskService = taskService;
        _statisticsService = statisticsService;
        _externalResourceService = externalResourceService;
        _focusModeService = focusModeService;
        _projectProgressService = projectProgressService;
        _gitService = gitService;

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
        GitInitCommand = new AsyncCommand(_ => GitInitAsync());
        GitStatusCommand = new AsyncCommand(_ => GitStatusAsync());
        GitStageAllCommand = new AsyncCommand(_ => GitStageAllAsync());
        GitCommitCommand = new AsyncCommand(_ => GitCommitAsync());
        OpenProjectFolderCommand = new RelayCommand(_ => OpenProjectFolder());
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
    public ICommand GitInitCommand { get; }
    public ICommand GitStatusCommand { get; }
    public ICommand GitStageAllCommand { get; }
    public ICommand GitCommitCommand { get; }
    public ICommand OpenProjectFolderCommand { get; }

    public double WorkItemProgressPercent
    {
        get => _workItemProgressPercent;
        private set => SetProperty(ref _workItemProgressPercent, value);
    }

    public double ProjectHealthScore
    {
        get => _projectHealthScore;
        private set => SetProperty(ref _projectHealthScore, value);
    }

    public double CompletedTasksPercent
    {
        get => _completedTasksPercent;
        private set => SetProperty(ref _completedTasksPercent, value);
    }

    public double ActiveTasksPercent
    {
        get => _activeTasksPercent;
        private set => SetProperty(ref _activeTasksPercent, value);
    }

    public double OverdueTasksPercent
    {
        get => _overdueTasksPercent;
        private set => SetProperty(ref _overdueTasksPercent, value);
    }

    public double FocusTodayPercent
    {
        get => _focusTodayPercent;
        private set => SetProperty(ref _focusTodayPercent, value);
    }

    public double FileActivityPercent
    {
        get => _fileActivityPercent;
        private set => SetProperty(ref _fileActivityPercent, value);
    }

    public double CompletedTasksChartHeight
    {
        get => _completedTasksChartHeight;
        private set => SetProperty(ref _completedTasksChartHeight, value);
    }

    public double ActiveTasksChartHeight
    {
        get => _activeTasksChartHeight;
        private set => SetProperty(ref _activeTasksChartHeight, value);
    }

    public double OverdueTasksChartHeight
    {
        get => _overdueTasksChartHeight;
        private set => SetProperty(ref _overdueTasksChartHeight, value);
    }

    public double FocusChartHeight
    {
        get => _focusChartHeight;
        private set => SetProperty(ref _focusChartHeight, value);
    }

    public double FileActivityChartHeight
    {
        get => _fileActivityChartHeight;
        private set => SetProperty(ref _fileActivityChartHeight, value);
    }

    public double HealthChartHeight
    {
        get => _healthChartHeight;
        private set => SetProperty(ref _healthChartHeight, value);
    }

    public double ProgressChartHeight
    {
        get => _progressChartHeight;
        private set => SetProperty(ref _progressChartHeight, value);
    }

    public double ProductivityChartHeight
    {
        get => _productivityChartHeight;
        private set => SetProperty(ref _productivityChartHeight, value);
    }

    public PointCollection ProjectTrendPoints
    {
        get => _projectTrendPoints;
        private set => SetProperty(ref _projectTrendPoints, value);
    }

    public Visibility SuggestedTasksEmptyVisibility => SuggestedTasks.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    public Visibility SuggestedTasksListVisibility => SuggestedTasks.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
    public Visibility StaleWorkItemsEmptyVisibility => StaleWorkItems.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    public Visibility StaleWorkItemsListVisibility => StaleWorkItems.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
    public Visibility RecentlyCompletedEmptyVisibility => RecentlyCompletedWorkItems.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    public Visibility RecentlyCompletedListVisibility => RecentlyCompletedWorkItems.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

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

    public string GitStatusText
    {
        get => _gitStatusText;
        private set => SetProperty(ref _gitStatusText, value);
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
        CompletedTasksPercent = PercentOf(CompletedTasks, TotalTasks);
        ActiveTasksPercent = PercentOf(ActiveTasks, TotalTasks);
        OverdueTasksPercent = PercentOf(OverdueTasks, TotalTasks);
        ActiveMilestoneTitle = progressSnapshot.ActiveMilestone?.Title ?? "-";
        WorkItemsInProgress = progressSnapshot.WorkItemsInProgress;
        FilesChangedToday = progressSnapshot.FilesChangedToday;
        FocusMinutesToday = progressSnapshot.FocusMinutesToday;
        FocusTodayPercent = Math.Clamp(Math.Round(FocusMinutesToday / 120d * 100d, 1), 0, 100);
        FileActivityPercent = Math.Clamp(Math.Round(FilesChangedToday / 8d * 100d, 1), 0, 100);
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
        ProjectHealthScore = Math.Clamp(Math.Round((OverallProgress * 0.45) + (ProductivityScore * 0.35) + (FileActivityPercent * 0.1) + (FocusTodayPercent * 0.1) - (OverdueTasks * 3), 1), 0, 100);
        UpdateChartGeometry();
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
        RefreshProgressSectionVisibility();
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

    private async Task GitInitAsync()
    {
        await RunGitAndShowSummaryAsync(["init"], "Git repository initialized.").ConfigureAwait(true);
    }

    private async Task GitStatusAsync()
    {
        await RunGitAndShowSummaryAsync(["status", "--short"], "Git status refreshed.").ConfigureAwait(true);
    }

    private async Task GitStageAllAsync()
    {
        await RunGitAndShowSummaryAsync(["add", "."], "All project changes staged.").ConfigureAwait(true);
        await RunGitAndShowSummaryAsync(["status", "--short"], "Git status refreshed.").ConfigureAwait(true);
    }

    private async Task GitCommitAsync()
    {
        var message = Interaction.InputBox("Commit message:", "ReachIT Git Commit", "Project checkpoint");
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        await RunGitAndShowSummaryAsync(["commit", "-m", message.Trim()], "Commit created.").ConfigureAwait(true);
    }

    private void OpenProjectFolder()
    {
        var path = GetProjectDirectoryOrShowMessage();
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            GitStatusText = ex.Message;
            MessageBox.Show(ex.Message, "ReachIT Git", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async Task RunGitAndShowSummaryAsync(IReadOnlyList<string> arguments, string successMessage)
    {
        var path = GetProjectDirectoryOrShowMessage();
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        try
        {
            var result = await _gitService.RunAsync(path, arguments).ConfigureAwait(true);
            if (result.ExitCode != 0)
            {
                throw new InvalidOperationException(string.IsNullOrWhiteSpace(result.Error)
                    ? result.Output
                    : result.Error);
            }

            GitStatusText = string.IsNullOrWhiteSpace(result.CombinedOutput)
                ? successMessage
                : result.CombinedOutput.Trim();
        }
        catch (Exception ex)
        {
            GitStatusText = ex.Message;
            MessageBox.Show(ex.Message, "ReachIT Git", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private string? GetProjectDirectoryOrShowMessage()
    {
        var path = _projectService.CurrentProject?.ProjectDirectoryPath;
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
        {
            GitStatusText = "Open a project first.";
            return null;
        }

        return path;
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
        ProjectHealthScore = 0;
        CompletedTasksPercent = 0;
        ActiveTasksPercent = 0;
        OverdueTasksPercent = 0;
        FocusTodayPercent = 0;
        FileActivityPercent = 0;
        CompletedTasksChartHeight = 0;
        ActiveTasksChartHeight = 0;
        OverdueTasksChartHeight = 0;
        FocusChartHeight = 0;
        FileActivityChartHeight = 0;
        HealthChartHeight = 0;
        ProgressChartHeight = 0;
        ProductivityChartHeight = 0;
        ProjectTrendPoints = new PointCollection();
        ActiveMilestoneTitle = "-";
        WorkItemsInProgress = 0;
        FilesChangedToday = 0;
        FocusMinutesToday = 0;
        CurrentWorkContext = string.Empty;
        StatusText = "Open a .rit project to initialize dashboard data.";
        EmptyTaskMessage = "No tasks yet. Create your first task to start planning.";
        GitStatusText = "Open a project to use Git controls.";

        TaskRows.Clear();
        NearestDeadlines.Clear();
        SuggestedTasks.Clear();
        StaleWorkItems.Clear();
        RecentlyCompletedWorkItems.Clear();
        RefreshProgressSectionVisibility();
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

    private static double PercentOf(int value, int total)
    {
        return total <= 0
            ? 0
            : Math.Clamp(Math.Round((double)value / total * 100d, 1), 0, 100);
    }

    private void UpdateChartGeometry()
    {
        CompletedTasksChartHeight = ChartHeight(CompletedTasksPercent);
        ActiveTasksChartHeight = ChartHeight(ActiveTasksPercent);
        OverdueTasksChartHeight = ChartHeight(OverdueTasksPercent);
        FocusChartHeight = ChartHeight(FocusTodayPercent);
        FileActivityChartHeight = ChartHeight(FileActivityPercent);
        HealthChartHeight = ChartHeight(ProjectHealthScore);
        ProgressChartHeight = ChartHeight(OverallProgress);
        ProductivityChartHeight = ChartHeight(ProductivityScore);

        ProjectTrendPoints = BuildTrendPoints([
            OverallProgress,
            CompletedTasksPercent,
            Math.Max(0, 100 - OverdueTasksPercent),
            FileActivityPercent,
            FocusTodayPercent,
            ProductivityScore,
            ProjectHealthScore
        ]);
    }

    private static double ChartHeight(double percent)
    {
        return percent <= 0
            ? 3
            : Math.Clamp(Math.Round(percent * 1.35, 1), 3, 135);
    }

    private static PointCollection BuildTrendPoints(IReadOnlyList<double> values)
    {
        const double width = 320;
        const double height = 120;
        var points = new PointCollection();
        if (values.Count == 0)
        {
            return points;
        }

        var step = values.Count == 1 ? 0 : width / (values.Count - 1);
        for (var i = 0; i < values.Count; i++)
        {
            var value = Math.Clamp(values[i], 0, 100);
            points.Add(new Point(Math.Round(i * step, 1), Math.Round(height - (value / 100d * height), 1)));
        }

        return points;
    }

    private void RefreshProgressSectionVisibility()
    {
        OnPropertyChanged(nameof(SuggestedTasksEmptyVisibility));
        OnPropertyChanged(nameof(SuggestedTasksListVisibility));
        OnPropertyChanged(nameof(StaleWorkItemsEmptyVisibility));
        OnPropertyChanged(nameof(StaleWorkItemsListVisibility));
        OnPropertyChanged(nameof(RecentlyCompletedEmptyVisibility));
        OnPropertyChanged(nameof(RecentlyCompletedListVisibility));
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
