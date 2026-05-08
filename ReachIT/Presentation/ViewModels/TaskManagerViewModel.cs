// Provides task manager commands and list state.
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using ReachIT.Application.Contracts;
using ReachIT.Domain.Models;
using ReachIT.Presentation.Commands;

namespace ReachIT.Presentation.ViewModels;

public sealed class TaskManagerViewModel : ViewModelBase
{
    private readonly ITaskService _taskService;
    private readonly AsyncCommand _editTaskCommand;
    private readonly AsyncCommand _deleteTaskCommand;
    private readonly AsyncCommand _markCompletedCommand;
    private readonly AsyncCommand _markTaskCompletedCommand;
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

    public TaskManagerViewModel(ITaskService taskService)
    {
        _taskService = taskService;

        AddTaskCommand = new AsyncCommand(_ => AddTaskAsync());
        _editTaskCommand = new AsyncCommand(_ => EditTaskAsync(), _ => SelectedTask is not null);
        _deleteTaskCommand = new AsyncCommand(_ => DeleteTaskAsync(), _ => SelectedTask is not null);
        _markCompletedCommand = new AsyncCommand(_ => MarkCompletedAsync(), _ => SelectedTask is not null && !SelectedTask.IsCompleted);
        _markTaskCompletedCommand = new AsyncCommand(p => MarkTaskCompletedAsync(p as TaskItem));

        EditTaskCommand = _editTaskCommand;
        DeleteTaskCommand = _deleteTaskCommand;
        MarkCompletedCommand = _markCompletedCommand;
        MarkTaskCompletedCommand = _markTaskCompletedCommand;
        SetDeadlineTodayCommand = new RelayCommand(_ => EditDeadlineDate = DateTime.Today);
        SetDeadlineTomorrowCommand = new RelayCommand(_ => EditDeadlineDate = DateTime.Today.AddDays(1));
        ClearDeadlineCommand = new RelayCommand(_ => EditDeadlineDate = null);

        SaveAllCommand = new AsyncCommand(_ => SaveAllAsync());
        AddSubtaskCommand = new AsyncCommand(_ => AddSubtaskAsync(), _ => SelectedTask is not null);
        DiscardChangesCommand = new RelayCommand(_ => DiscardChanges(), _ => SelectedTask is not null);
    }

    public ObservableCollection<TaskItem> Tasks { get; } = new();
    public ObservableCollection<TaskItem> FilteredTasks { get; } = new();
    public ObservableCollection<TaskItem> TreeTasks { get; } = new();

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

    public ICommand AddTaskCommand { get; }
    public ICommand EditTaskCommand { get; }
    public ICommand DeleteTaskCommand { get; }
    public ICommand MarkCompletedCommand { get; }
    public ICommand MarkTaskCompletedCommand { get; }
    public ICommand SetDeadlineTodayCommand { get; }
    public ICommand SetDeadlineTomorrowCommand { get; }
    public ICommand ClearDeadlineCommand { get; }
    public ICommand SaveAllCommand { get; }
    public ICommand AddSubtaskCommand { get; }
    public ICommand DiscardChangesCommand { get; }

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        Tasks.Clear();
        var tasks = await _taskService.GetTasksAsync(cancellationToken).ConfigureAwait(true);
        var ordered = OrderTasksForHierarchy(tasks).ToList();
        await NormalizeQueueStateAsync(ordered, cancellationToken).ConfigureAwait(true);

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
        TreeTasks.Clear();
        foreach (var task in Tasks)
        {
            task.Subtasks.Clear();
        }

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

        foreach (var root in BuildTaskTree(FilteredTasks))
        {
            TreeTasks.Add(root);
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
                .ThenBy(t => t.Priority <= 0 ? int.MaxValue : t.Priority)
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

    private static IEnumerable<TaskItem> BuildTaskTree(IEnumerable<TaskItem> source)
    {
        var taskList = source.ToList();
        var taskIds = taskList.Select(x => x.Id).ToHashSet();
        var byParent = taskList
            .GroupBy(x => x.ParentTaskId.HasValue && taskIds.Contains(x.ParentTaskId.Value)
                ? x.ParentTaskId.Value
                : Guid.Empty)
                .ToDictionary(x => x.Key, x => x
                .OrderBy(t => t.IsCompleted)
                .ThenBy(t => t.Priority <= 0 ? int.MaxValue : t.Priority)
                .ThenBy(t => t.DueDateUtc ?? DateTime.MaxValue)
                .ThenBy(t => t.Title, StringComparer.OrdinalIgnoreCase)
                .ToList());

        return AttachChildren(Guid.Empty, byParent, new HashSet<Guid>());
    }

    private static IEnumerable<TaskItem> AttachChildren(
        Guid parentId,
        IReadOnlyDictionary<Guid, List<TaskItem>> byParent,
        HashSet<Guid> visited)
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

            child.DisplayTitle = child.Title;
            child.Subtasks.Clear();
            foreach (var descendant in AttachChildren(child.Id, byParent, visited))
            {
                child.Subtasks.Add(descendant);
            }

            yield return child;
        }
    }

    private async Task AddTaskAsync()
    {
        var newTask = new TaskItem
        {
            Title = "New Task",
            Description = "Define task details",
            IsCompleted = false,
            Priority = 0,
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
        if (SelectedTask.IsCompleted)
        {
            SelectedTask.Status = "Done";
            SelectedTask.CompletedAtUtc ??= DateTime.UtcNow;
            SelectedTask.StartedAtUtc ??= SelectedTask.CompletedAtUtc;
        }
        else if (SelectedTask.CompletedAtUtc.HasValue)
        {
            SelectedTask.CompletedAtUtc = null;
        }

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
            ParentTaskId = SelectedTask.Id,
            Priority = 0
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
        await MarkTaskCompletedAsync(SelectedTask).ConfigureAwait(true);
    }

    private async Task MarkTaskCompletedAsync(TaskItem? task)
    {
        if (task is null || task.IsCompleted)
        {
            return;
        }

        SelectedTask = task;
        task.IsCompleted = true;
        task.Status = "Done";
        task.CompletedAtUtc = DateTime.UtcNow;
        task.StartedAtUtc ??= task.CompletedAtUtc;
        await _taskService.UpdateTaskAsync(task).ConfigureAwait(true);
        await LoadAsync().ConfigureAwait(true);
    }

    private async Task NormalizeQueueStateAsync(IReadOnlyList<TaskItem> ordered, CancellationToken cancellationToken)
    {
        var active = ordered.FirstOrDefault(x => !x.IsCompleted);
        foreach (var task in ordered)
        {
            var originalStatus = task.Status;
            var originalStartedAt = task.StartedAtUtc;
            var originalCompletedAt = task.CompletedAtUtc;

            if (task.IsCompleted)
            {
                task.Status = "Done";
                task.CompletedAtUtc ??= DateTime.UtcNow;
                task.StartedAtUtc ??= task.CompletedAtUtc;
            }
            else if (active is not null && task.Id == active.Id)
            {
                task.Status = "In Progress";
                task.StartedAtUtc ??= DateTime.UtcNow;
                task.CompletedAtUtc = null;
            }
            else
            {
                task.Status = "Queued";
                task.CompletedAtUtc = null;
            }

            if (!string.Equals(originalStatus, task.Status, StringComparison.Ordinal) ||
                originalStartedAt != task.StartedAtUtc ||
                originalCompletedAt != task.CompletedAtUtc)
            {
                await _taskService.UpdateTaskAsync(task, cancellationToken).ConfigureAwait(true);
            }
        }
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
