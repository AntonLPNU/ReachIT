// Manages task list state for Task Manager view.
using System.Collections.ObjectModel;
using ReachIT.Application.Contracts;
using ReachIT.Domain.Models;

namespace ReachIT.Presentation.ViewModels;

public sealed class TasksViewModel : ViewModelBase
{
    private readonly ITaskService _taskService;
    private TaskItem? _selectedTask;

    public TasksViewModel(ITaskService taskService)
    {
        _taskService = taskService;
    }

    public ObservableCollection<TaskItem> Tasks { get; } = new();

    public TaskItem? SelectedTask
    {
        get => _selectedTask;
        set => SetProperty(ref _selectedTask, value);
    }

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        var tasks = await _taskService.GetTasksAsync(cancellationToken).ConfigureAwait(true);
        Tasks.Clear();
        foreach (var task in tasks)
        {
            Tasks.Add(task);
        }
    }
}
