// Placeholder planning workspace view model.
using System.Collections.ObjectModel;
using ReachIT.Application.Contracts;
using ReachIT.Domain.Models;

namespace ReachIT.Presentation.ViewModels;

public sealed class PlanningViewModel : ViewModelBase
{
    private readonly ITaskService _taskService;

    public PlanningViewModel(ITaskService taskService)
    {
        _taskService = taskService;
    }

    public ObservableCollection<TaskItem> TodayTasks { get; } = new();
    public ObservableCollection<TaskItem> UpcomingTasks { get; } = new();
    public ObservableCollection<TaskItem> SomedayTasks { get; } = new();

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        var tasks = await _taskService.GetTasksAsync(cancellationToken).ConfigureAwait(true);
        var activeTasks = tasks.Where(t => !t.IsCompleted).ToList();

        TodayTasks.Clear();
        UpcomingTasks.Clear();
        SomedayTasks.Clear();

        var today = DateTime.Today;

        foreach (var t in activeTasks)
        {
            if (t.DueDateUtc.HasValue)
            {
                var localDueDate = t.DueDateUtc.Value.ToLocalTime().Date;
                if (localDueDate <= today)
                {
                    TodayTasks.Add(t);
                }
                else
                {
                    UpcomingTasks.Add(t);
                }
            }
            else
            {
                SomedayTasks.Add(t);
            }
        }
    }
}
