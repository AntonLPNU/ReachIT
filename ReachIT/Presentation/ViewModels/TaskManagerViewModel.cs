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
    private readonly AsyncCommand _linkTaskToNodeCommand;
    private readonly AsyncCommand _markCompletedCommand;
    private TaskItem? _selectedTask;
    private string _editTitle = string.Empty;
    private string _editDescription = string.Empty;
    private DateTime? _editDeadlineDate;
    private bool _editIsCompleted;
    private int _overdueTasksCount;
    private int _dueTodayCount;
    private int _withoutDeadlineCount;
    private string _deadlineValidationMessage = string.Empty;

    public TaskManagerViewModel(ITaskService taskService)
    {
        _taskService = taskService;

        AddTaskCommand = new AsyncCommand(_ => AddTaskAsync());
        _editTaskCommand = new AsyncCommand(_ => EditTaskAsync(), _ => SelectedTask is not null);
        _deleteTaskCommand = new AsyncCommand(_ => DeleteTaskAsync(), _ => SelectedTask is not null);
        _linkTaskToNodeCommand = new AsyncCommand(_ => LinkTaskToNodeAsync(), _ => SelectedTask is not null);
        _markCompletedCommand = new AsyncCommand(_ => MarkCompletedAsync(), _ => SelectedTask is not null && !SelectedTask.IsCompleted);

        EditTaskCommand = _editTaskCommand;
        DeleteTaskCommand = _deleteTaskCommand;
        LinkTaskToNodeCommand = _linkTaskToNodeCommand;
        MarkCompletedCommand = _markCompletedCommand;
        SetDeadlineTodayCommand = new RelayCommand(_ => EditDeadlineDate = DateTime.Today);
        SetDeadlineTomorrowCommand = new RelayCommand(_ => EditDeadlineDate = DateTime.Today.AddDays(1));
        ClearDeadlineCommand = new RelayCommand(_ => EditDeadlineDate = null);
    }

    public ObservableCollection<TaskItem> Tasks { get; } = new();

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
    public ICommand LinkTaskToNodeCommand { get; }
    public ICommand MarkCompletedCommand { get; }
    public ICommand SetDeadlineTodayCommand { get; }
    public ICommand SetDeadlineTomorrowCommand { get; }
    public ICommand ClearDeadlineCommand { get; }

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        Tasks.Clear();
        var tasks = await _taskService.GetTasksAsync(cancellationToken).ConfigureAwait(true);
        var ordered = tasks
            .OrderBy(x => x.IsCompleted)
            .ThenBy(x => x.DueDateUtc ?? DateTime.MaxValue)
            .ThenBy(x => x.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();

        OverdueTasksCount = ordered.Count(x => !x.IsCompleted && x.DueDateUtc.HasValue && x.DueDateUtc.Value.ToLocalTime() < DateTime.Now);
        DueTodayCount = ordered.Count(x => !x.IsCompleted && x.DueDateUtc.HasValue && x.DueDateUtc.Value.ToLocalTime().Date == DateTime.Today);
        WithoutDeadlineCount = ordered.Count(x => !x.DueDateUtc.HasValue);

        var selectedId = SelectedTask?.Id;
        foreach (var task in ordered)
        {
            Tasks.Add(task);
        }

        SelectedTask = selectedId.HasValue
            ? Tasks.FirstOrDefault(x => x.Id == selectedId.Value)
            : Tasks.FirstOrDefault();

        RaiseCommandStates();
    }

    private async Task AddTaskAsync()
    {
        var newTask = new TaskItem
        {
            Title = "New Task",
            Description = "TODO: update task details",
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
        SelectedTask.DueDateUtc = EditDeadlineDate.HasValue
            ? DateTime.SpecifyKind(EditDeadlineDate.Value.Date.AddHours(23).AddMinutes(59), DateTimeKind.Local).ToUniversalTime()
            : null;

        await _taskService.UpdateTaskAsync(SelectedTask).ConfigureAwait(true);
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

    private async Task LinkTaskToNodeAsync()
    {
        if (SelectedTask is null)
        {
            return;
        }

        // TODO: Replace with selected node id from explorer binding/context.
        await _taskService.LinkTaskToNodeAsync(SelectedTask.Id, Guid.Empty).ConfigureAwait(true);
        MessageBox.Show("Task linked to placeholder node. TODO: use current project tree selection.", "ReachIT", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void PopulateEditorFromSelection()
    {
        if (SelectedTask is null)
        {
            EditTitle = string.Empty;
            EditDescription = string.Empty;
            EditDeadlineDate = null;
            EditIsCompleted = false;
            DeadlineValidationMessage = string.Empty;
            return;
        }

        EditTitle = SelectedTask.Title;
        EditDescription = SelectedTask.Description;
        EditDeadlineDate = SelectedTask.DueDateUtc?.ToLocalTime().Date;
        EditIsCompleted = SelectedTask.IsCompleted;
        DeadlineValidationMessage = string.Empty;
    }

    private void RaiseCommandStates()
    {
        _editTaskCommand.RaiseCanExecuteChanged();
        _deleteTaskCommand.RaiseCanExecuteChanged();
        _linkTaskToNodeCommand.RaiseCanExecuteChanged();
        _markCompletedCommand.RaiseCanExecuteChanged();
    }
}
