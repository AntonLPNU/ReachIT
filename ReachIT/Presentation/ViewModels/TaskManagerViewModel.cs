// Provides task manager commands and list state.
using System.Collections.ObjectModel;
using System.Windows.Input;
using ReachIT.Application.Contracts;
using ReachIT.Domain.Models;
using ReachIT.Presentation.Commands;

namespace ReachIT.Presentation.ViewModels;

public sealed class TaskManagerViewModel : ViewModelBase
{
    private readonly ITaskService _taskService;
    private TaskItem? _selectedTask;

    public TaskManagerViewModel(ITaskService taskService)
    {
        _taskService = taskService;

        AddTaskCommand = new AsyncCommand(_ => AddTaskAsync());
        EditTaskCommand = new AsyncCommand(_ => EditTaskAsync(), _ => SelectedTask is not null);
        DeleteTaskCommand = new AsyncCommand(_ => DeleteTaskAsync(), _ => SelectedTask is not null);
        LinkTaskToNodeCommand = new AsyncCommand(_ => LinkTaskToNodeAsync(), _ => SelectedTask is not null);
    }

    public ObservableCollection<TaskItem> Tasks { get; } = new();

    public TaskItem? SelectedTask
    {
        get => _selectedTask;
        set => SetProperty(ref _selectedTask, value);
    }

    public ICommand AddTaskCommand { get; }
    public ICommand EditTaskCommand { get; }
    public ICommand DeleteTaskCommand { get; }
    public ICommand LinkTaskToNodeCommand { get; }

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        Tasks.Clear();
        var tasks = await _taskService.GetTasksAsync(cancellationToken).ConfigureAwait(true);
        foreach (var task in tasks)
        {
            Tasks.Add(task);
        }
    }

    private async Task AddTaskAsync()
    {
        var newTask = new TaskItem
        {
            Title = "New Task",
            Description = "TODO: update task details",
            IsCompleted = false
        };

        await _taskService.AddTaskAsync(newTask).ConfigureAwait(true);
        await LoadAsync().ConfigureAwait(true);
    }

    private async Task EditTaskAsync()
    {
        if (SelectedTask is null)
        {
            return;
        }

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

    private async Task LinkTaskToNodeAsync()
    {
        if (SelectedTask is null)
        {
            return;
        }

        // TODO: Replace with selected node id from explorer binding/context.
        await _taskService.LinkTaskToNodeAsync(SelectedTask.Id, Guid.Empty).ConfigureAwait(true);
    }
}
