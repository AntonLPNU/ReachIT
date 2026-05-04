// Provides task manager commands and list state.
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using Microsoft.VisualBasic;
using ReachIT.Application.Contracts;
using ReachIT.Domain.Models;
using ReachIT.Presentation.Commands;

namespace ReachIT.Presentation.ViewModels;

public sealed class TaskManagerViewModel : ViewModelBase
{
    private readonly ITaskService _taskService;
    private readonly IProjectService _projectService;
    private readonly IGitService _gitService;
    private readonly AsyncCommand _editTaskCommand;
    private readonly AsyncCommand _deleteTaskCommand;
    private readonly AsyncCommand _markCompletedCommand;
    private readonly AsyncCommand _gitInitCommand;
    private readonly AsyncCommand _gitStatusCommand;
    private readonly AsyncCommand _gitStageAllCommand;
    private readonly AsyncCommand _gitCommitCommand;
    private TaskItem? _selectedTask;
    private string _editTitle = string.Empty;
    private string _editDescription = string.Empty;
    private DateTime? _editDeadlineDate;
    private bool _editIsCompleted;
    private int _editPriority = 2;
    private string _editStatus = "To Do";
    private int _overdueTasksCount;
    private int _dueTodayCount;
    private int _withoutDeadlineCount;
    private string _deadlineValidationMessage = string.Empty;
    private string _searchText = string.Empty;
    private bool _showActiveOnly;
    private string _gitStatusText = "Open a project to use Git controls.";

    public TaskManagerViewModel(ITaskService taskService, IProjectService projectService, IGitService gitService)
    {
        _taskService = taskService;
        _projectService = projectService;
        _gitService = gitService;

        AddTaskCommand = new AsyncCommand(_ => AddTaskAsync());
        _editTaskCommand = new AsyncCommand(_ => EditTaskAsync(), _ => SelectedTask is not null);
        _deleteTaskCommand = new AsyncCommand(_ => DeleteTaskAsync(), _ => SelectedTask is not null);
        _markCompletedCommand = new AsyncCommand(_ => MarkCompletedAsync(), _ => SelectedTask is not null && !SelectedTask.IsCompleted);
        _gitInitCommand = new AsyncCommand(_ => GitInitAsync());
        _gitStatusCommand = new AsyncCommand(_ => GitStatusAsync());
        _gitStageAllCommand = new AsyncCommand(_ => GitStageAllAsync());
        _gitCommitCommand = new AsyncCommand(_ => GitCommitAsync());

        EditTaskCommand = _editTaskCommand;
        DeleteTaskCommand = _deleteTaskCommand;
        MarkCompletedCommand = _markCompletedCommand;
        GitInitCommand = _gitInitCommand;
        GitStatusCommand = _gitStatusCommand;
        GitStageAllCommand = _gitStageAllCommand;
        GitCommitCommand = _gitCommitCommand;
        OpenProjectFolderCommand = new RelayCommand(_ => OpenProjectFolder());
        SetDeadlineTodayCommand = new RelayCommand(_ => EditDeadlineDate = DateTime.Today);
        SetDeadlineTomorrowCommand = new RelayCommand(_ => EditDeadlineDate = DateTime.Today.AddDays(1));
        ClearDeadlineCommand = new RelayCommand(_ => EditDeadlineDate = null);

        SaveAllCommand = new AsyncCommand(_ => SaveAllAsync());
        AddSubtaskCommand = new AsyncCommand(_ => AddSubtaskAsync(), _ => SelectedTask is not null);
        DiscardChangesCommand = new RelayCommand(_ => DiscardChanges(), _ => SelectedTask is not null);
    }

    public ObservableCollection<TaskItem> Tasks { get; } = new();
    public ObservableCollection<TaskItem> FilteredTasks { get; } = new();

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                ApplyFilter();
            }
        }
    }

    public bool ShowActiveOnly
    {
        get => _showActiveOnly;
        set
        {
            if (SetProperty(ref _showActiveOnly, value))
            {
                ApplyFilter();
            }
        }
    }

    public Visibility EmptyStateVisibility => FilteredTasks.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

    public bool IsTaskSelected => SelectedTask is not null;

    public TaskItem? SelectedTask
    {
        get => _selectedTask;
        set
        {
            if (SetProperty(ref _selectedTask, value))
            {
                PopulateEditorFromSelection();
                RaiseCommandStates();
            }
        }
    }

    public string EditTitle
    {
        get => _editTitle;
        set => SetProperty(ref _editTitle, value);
    }

    public string EditDescription
    {
        get => _editDescription;
        set => SetProperty(ref _editDescription, value);
    }

    public DateTime? EditDeadlineDate
    {
        get => _editDeadlineDate;
        set => SetProperty(ref _editDeadlineDate, value);
    }

    public bool EditIsCompleted
    {
        get => _editIsCompleted;
        set => SetProperty(ref _editIsCompleted, value);
    }

    public int EditPriority
    {
        get => _editPriority;
        set => SetProperty(ref _editPriority, value);
    }

    public string EditStatus
    {
        get => _editStatus;
        set => SetProperty(ref _editStatus, value);
    }

    public int OverdueTasksCount
    {
        get => _overdueTasksCount;
        private set => SetProperty(ref _overdueTasksCount, value);
    }

    public int DueTodayCount
    {
        get => _dueTodayCount;
        private set => SetProperty(ref _dueTodayCount, value);
    }

    public int WithoutDeadlineCount
    {
        get => _withoutDeadlineCount;
        private set => SetProperty(ref _withoutDeadlineCount, value);
    }

    public string DeadlineValidationMessage
    {
        get => _deadlineValidationMessage;
        private set => SetProperty(ref _deadlineValidationMessage, value);
    }

    public string GitStatusText
    {
        get => _gitStatusText;
        private set => SetProperty(ref _gitStatusText, value);
    }

    public ICommand AddTaskCommand { get; }
    public ICommand EditTaskCommand { get; }
    public ICommand DeleteTaskCommand { get; }
    public ICommand MarkCompletedCommand { get; }
    public ICommand SetDeadlineTodayCommand { get; }
    public ICommand SetDeadlineTomorrowCommand { get; }
    public ICommand ClearDeadlineCommand { get; }
    public ICommand SaveAllCommand { get; }
    public ICommand AddSubtaskCommand { get; }
    public ICommand DiscardChangesCommand { get; }
    public ICommand GitInitCommand { get; }
    public ICommand GitStatusCommand { get; }
    public ICommand GitStageAllCommand { get; }
    public ICommand GitCommitCommand { get; }
    public ICommand OpenProjectFolderCommand { get; }

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        Tasks.Clear();
        var tasks = await _taskService.GetTasksAsync(cancellationToken).ConfigureAwait(true);
        var ordered = OrderTasksForHierarchy(tasks).ToList();

        OverdueTasksCount = ordered.Count(x => !x.IsCompleted && x.DueDateUtc.HasValue && x.DueDateUtc.Value.ToLocalTime() < DateTime.Now);
        DueTodayCount = ordered.Count(x => !x.IsCompleted && x.DueDateUtc.HasValue && x.DueDateUtc.Value.ToLocalTime().Date == DateTime.Today);
        WithoutDeadlineCount = ordered.Count(x => !x.DueDateUtc.HasValue);

        var selectedId = SelectedTask?.Id;
        foreach (var task in ordered)
        {
            Tasks.Add(task);
        }

        ApplyFilter();

        SelectedTask = selectedId.HasValue
            ? Tasks.FirstOrDefault(x => x.Id == selectedId.Value)
            : Tasks.FirstOrDefault();

        RaiseCommandStates();
        OnPropertyChanged(nameof(EmptyStateVisibility));
        OnPropertyChanged(nameof(IsTaskSelected));
    }

    public void ShowActiveTasks()
    {
        ShowActiveOnly = true;
    }

    public void ShowAllTasks()
    {
        ShowActiveOnly = false;
    }

    private void ApplyFilter()
    {
        FilteredTasks.Clear();
        var query = Tasks.AsEnumerable();

        if (ShowActiveOnly)
        {
            query = query.Where(t => !t.IsCompleted);
        }

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            query = query.Where(t => t.Title.Contains(SearchText, StringComparison.OrdinalIgnoreCase) 
                                     || t.Description.Contains(SearchText, StringComparison.OrdinalIgnoreCase));
        }

        foreach (var t in query)
        {
            FilteredTasks.Add(t);
        }

        OnPropertyChanged(nameof(EmptyStateVisibility));
    }

    private static IEnumerable<TaskItem> OrderTasksForHierarchy(IEnumerable<TaskItem> tasks)
    {
        var rootId = Guid.Empty;
        var taskList = tasks.ToList();
        var taskIds = taskList.Select(x => x.Id).ToHashSet();
        var visited = new HashSet<Guid>();
        var byParent = taskList
            .GroupBy(x => x.ParentTaskId.HasValue && taskIds.Contains(x.ParentTaskId.Value)
                ? x.ParentTaskId.Value
                : rootId)
            .ToDictionary(x => x.Key, x => x
                .OrderBy(t => t.IsCompleted)
                .ThenBy(t => t.DueDateUtc ?? DateTime.MaxValue)
                .ThenBy(t => t.Title, StringComparer.OrdinalIgnoreCase)
                .ToList());

        foreach (var root in GetChildren(rootId, 0))
        {
            yield return root;
        }

        IEnumerable<TaskItem> GetChildren(Guid parentId, int depth)
        {
            if (!byParent.TryGetValue(parentId, out var children))
            {
                yield break;
            }

            foreach (var child in children)
            {
                if (!visited.Add(child.Id))
                {
                    continue;
                }

                child.DisplayTitle = depth == 0
                    ? child.Title
                    : $"{new string(' ', depth * 4)}-- {child.Title}";

                yield return child;

                foreach (var descendant in GetChildren(child.Id, depth + 1))
                {
                    yield return descendant;
                }
            }
        }
    }

    private async Task AddTaskAsync()
    {
        var newTask = new TaskItem
        {
            Title = "New Task",
            Description = "Define task details",
            IsCompleted = false,
            DueDateUtc = DateTime.UtcNow.Date.AddDays(1).AddHours(20)
        };

        await _taskService.AddTaskAsync(newTask).ConfigureAwait(true);
        await LoadAsync().ConfigureAwait(true);
        SelectedTask = Tasks.FirstOrDefault(x => x.Id == newTask.Id);
    }

    private async Task EditTaskAsync()
    {
        if (SelectedTask is null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(EditTitle))
        {
            DeadlineValidationMessage = "Task title is required.";
            return;
        }

        DeadlineValidationMessage = string.Empty;

        SelectedTask.Title = EditTitle.Trim();
        SelectedTask.Description = EditDescription?.Trim() ?? string.Empty;
        SelectedTask.IsCompleted = EditIsCompleted;
        SelectedTask.Priority = EditPriority;
        SelectedTask.Status = EditStatus?.Trim() ?? "To Do";
        SelectedTask.DueDateUtc = EditDeadlineDate.HasValue
            ? DateTime.SpecifyKind(EditDeadlineDate.Value.Date.AddHours(23).AddMinutes(59), DateTimeKind.Local).ToUniversalTime()
            : null;

        SelectedTask.HasUnsavedChanges = true;
        // Don't save immediately to DB so we can demo Save All behavior unless desired.
        // Actually, let's just mark it unsaved. If users want to save immediately, we'd do the below:
        // await _taskService.UpdateTaskAsync(SelectedTask).ConfigureAwait(true);

        // Let's force UI to refresh the row for Unsaved indicator
        var idx = Tasks.IndexOf(SelectedTask);
        if (idx >= 0) { Tasks[idx] = SelectedTask; }
        ApplyFilter();
        SelectedTask = FilteredTasks.FirstOrDefault(t => t.Id == SelectedTask.Id);
    }

    private void DiscardChanges()
    {
        PopulateEditorFromSelection();
    }

    private async Task SaveAllAsync()
    {
        var unsaved = Tasks.Where(t => t.HasUnsavedChanges).ToList();
        foreach (var t in unsaved)
        {
            await _taskService.UpdateTaskAsync(t).ConfigureAwait(true);
            t.HasUnsavedChanges = false;
        }
        ApplyFilter();
    }

    private async Task AddSubtaskAsync()
    {
        if (SelectedTask is null) return;

        var sub = new TaskItem
        {
            Title = "New Subtask",
            ParentTaskId = SelectedTask.Id
        };
        await _taskService.AddTaskAsync(sub).ConfigureAwait(true);
        await LoadAsync().ConfigureAwait(true);
    }

    private async Task DeleteTaskAsync()
    {
        if (SelectedTask is null)
        {
            return;
        }

        await _taskService.DeleteTaskAsync(SelectedTask.Id).ConfigureAwait(true);
        await LoadAsync().ConfigureAwait(true);
    }

    private async Task MarkCompletedAsync()
    {
        if (SelectedTask is null)
        {
            return;
        }

        SelectedTask.IsCompleted = true;
        await _taskService.UpdateTaskAsync(SelectedTask).ConfigureAwait(true);
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

    private void PopulateEditorFromSelection()
    {
        if (SelectedTask is null)
        {
            EditTitle = string.Empty;
            EditDescription = string.Empty;
            EditDeadlineDate = null;
            EditIsCompleted = false;
            EditPriority = 2;
            EditStatus = "To Do";
            DeadlineValidationMessage = string.Empty;
            OnPropertyChanged(nameof(IsTaskSelected));
            return;
        }

        EditTitle = SelectedTask.Title;
        EditDescription = SelectedTask.Description;
        EditDeadlineDate = SelectedTask.DueDateUtc?.ToLocalTime().Date;
        EditIsCompleted = SelectedTask.IsCompleted;
        EditPriority = SelectedTask.Priority;
        EditStatus = SelectedTask.Status;
        DeadlineValidationMessage = string.Empty;
        OnPropertyChanged(nameof(IsTaskSelected));
    }

    private void RaiseCommandStates()
    {
        _editTaskCommand.RaiseCanExecuteChanged();
        _deleteTaskCommand.RaiseCanExecuteChanged();
        _markCompletedCommand.RaiseCanExecuteChanged();
    }
}
