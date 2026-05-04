using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using ReachIT.Application.Contracts;
using ReachIT.Domain.Models;
using ReachIT.Presentation.Commands;
using ReachIT.Presentation.Services;

namespace ReachIT.Presentation.ViewModels;

public sealed class QuickAddTaskViewModel : ViewModelBase
{
    private readonly ITaskService _taskService;
    private readonly IRecentFilesService _recentFilesService;

    private string _title = string.Empty;
    private string _description = string.Empty;
    private ProjectMeta? _selectedProject;
    private string _parentOrGroup = string.Empty;
    private int _priority = 2;
    private DateTime? _deadlineDate = DateTime.Today;
    private string _tags = string.Empty;
    private string _statusText = string.Empty;

    public event EventHandler? Saved;
    public event EventHandler? Cancelled;

    public QuickAddTaskViewModel(ITaskService taskService, IRecentFilesService recentFilesService)
    {
        _taskService = taskService;
        _recentFilesService = recentFilesService;

        Projects = new ObservableCollection<ProjectMeta>();
        Priorities = new ObservableCollection<int> { 1, 2, 3 };

        SaveCommand = new AsyncCommand(_ => SaveAsync());
        CancelCommand = new RelayCommand(_ => Cancelled?.Invoke(this, EventArgs.Empty));
    }

    public ObservableCollection<ProjectMeta> Projects { get; }
    public ObservableCollection<int> Priorities { get; }

    public string Title
    {
        get => _title;
        set => SetProperty(ref _title, value);
    }

    public string Description
    {
        get => _description;
        set => SetProperty(ref _description, value);
    }

    public ProjectMeta? SelectedProject
    {
        get => _selectedProject;
        set => SetProperty(ref _selectedProject, value);
    }

    public string ParentOrGroup
    {
        get => _parentOrGroup;
        set => SetProperty(ref _parentOrGroup, value);
    }

    public int Priority
    {
        get => _priority;
        set => SetProperty(ref _priority, value);
    }

    public DateTime? DeadlineDate
    {
        get => _deadlineDate;
        set => SetProperty(ref _deadlineDate, value);
    }

    public string Tags
    {
        get => _tags;
        set => SetProperty(ref _tags, value);
    }

    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    public ICommand SaveCommand { get; }
    public ICommand CancelCommand { get; }

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        Projects.Clear();
        var projects = await _recentFilesService.GetRecentProjectsAsync(cancellationToken).ConfigureAwait(true);
        foreach (var project in projects)
        {
            Projects.Add(project);
        }

        SelectedProject = Projects.FirstOrDefault();
        Title = string.Empty;
        Description = string.Empty;
        ParentOrGroup = string.Empty;
        Priority = 2;
        DeadlineDate = DateTime.Today;
        Tags = string.Empty;
        StatusText = string.Empty;
    }

    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(Title))
        {
            StatusText = LocalizationService.GetString("QuickAdd.TitleRequired", "Title is required.");
            return;
        }

        var task = new TaskItem
        {
            Id = Guid.NewGuid(),
            Title = Title.Trim(),
            Description = Description.Trim(),
            Priority = Priority,
            Status = "Active",
            DueDateUtc = DeadlineDate?.Date.ToUniversalTime()
        };

        foreach (var tag in Tags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            task.Tags.Add(new TaskTag { Id = Guid.NewGuid(), TaskItemId = task.Id, Value = tag });
        }

        await _taskService.AddTaskAsync(task).ConfigureAwait(true);
        Saved?.Invoke(this, EventArgs.Empty);
    }
}
