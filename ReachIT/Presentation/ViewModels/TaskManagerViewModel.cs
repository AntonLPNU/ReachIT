// Provides task manager commands and list state.
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Text;
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
    private readonly IDialogService _dialogService;
    private readonly ITaskBoardSyncService _taskBoardSyncService;
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
    private bool _isCanvasView = true;
    private TaskDiagramStyle _diagramStyle = TaskDiagramStyle.ReachIt;
    private double _canvasWidth = 960;
    private double _canvasHeight = 520;
    private TaskRelatedFileNode? _selectedRelatedFile;
    private string _taskWorkspacePath = "No workspace folder";
    private string _relatedFilesSummary = "No files linked to this task.";

    public TaskManagerViewModel(
        ITaskService taskService,
        IProjectService projectService,
        IDialogService dialogService,
        ITaskBoardSyncService taskBoardSyncService)
    {
        _taskService = taskService;
        _projectService = projectService;
        _dialogService = dialogService;
        _taskBoardSyncService = taskBoardSyncService;

        AddTaskCommand = new AsyncCommand(_ => AddTaskAsync());
        SelectTaskCommand = new RelayCommand(p => SelectTask(p as TaskItem));
        SetListViewCommand = new RelayCommand(_ => IsCanvasView = false);
        SetCanvasViewCommand = new RelayCommand(_ => IsCanvasView = true);
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
        CreateWorkspaceFolderCommand = new AsyncCommand(_ => CreateWorkspaceFolderAsync(), _ => SelectedTask is not null);
        ChooseWorkspaceFolderCommand = new AsyncCommand(_ => ChooseWorkspaceFolderAsync(), _ => SelectedTask is not null);
        NewTaskFileCommand = new AsyncCommand(_ => CreateLinkedFileAsync(), _ => SelectedTask is not null);
        NewTaskFolderCommand = new AsyncCommand(_ => CreateLinkedFolderAsync(), _ => SelectedTask is not null);
        LinkExistingFileCommand = new AsyncCommand(_ => LinkExistingFileAsync(), _ => SelectedTask is not null);
        ImportExternalFileCommand = new AsyncCommand(_ => ImportExternalFileAsync(), _ => SelectedTask is not null);
        RenameRelatedFileCommand = new AsyncCommand(_ => RenameRelatedFileAsync(), _ => SelectedRelatedFile is not null);
        OpenTaskWorkspaceCommand = new RelayCommand(_ => OpenTaskWorkspace(), _ => HasTaskWorkspace);
        RevealTaskWorkspaceCommand = new RelayCommand(_ => RevealTaskWorkspace(), _ => HasTaskWorkspace);
        OpenRelatedFileCommand = new RelayCommand(_ => OpenRelatedFile(), _ => SelectedRelatedFile is not null);
        RevealRelatedFileCommand = new RelayCommand(_ => RevealRelatedFile(), _ => SelectedRelatedFile is not null);
    }

    public ObservableCollection<TaskItem> Tasks { get; } = new();
    public ObservableCollection<TaskItem> FilteredTasks { get; } = new();
    public ObservableCollection<TaskItem> TreeTasks { get; } = new();
    public ObservableCollection<TaskCanvasNode> CanvasTasks { get; } = new();
    public ObservableCollection<TaskCanvasConnector> CanvasConnectors { get; } = new();
    public ObservableCollection<TaskRelatedFileNode> RelatedFiles { get; } = new();

    public event EventHandler? RequestProjectTreeRefresh;

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

    public bool IsCanvasView
    {
        get => _isCanvasView;
        set
        {
            if (SetProperty(ref _isCanvasView, value))
            {
                OnPropertyChanged(nameof(IsListView));
            }
        }
    }

    public bool IsListView => !IsCanvasView;

    public TaskDiagramStyle DiagramStyle
    {
        get => _diagramStyle;
        set
        {
            if (SetProperty(ref _diagramStyle, value))
            {
                OnPropertyChanged(nameof(IsReachItDiagramStyle));
                OnPropertyChanged(nameof(IsClassicDiagramStyle));
            }
        }
    }

    public bool IsReachItDiagramStyle
    {
        get => DiagramStyle == TaskDiagramStyle.ReachIt;
        set
        {
            if (value)
            {
                DiagramStyle = TaskDiagramStyle.ReachIt;
            }
        }
    }

    public bool IsClassicDiagramStyle
    {
        get => DiagramStyle == TaskDiagramStyle.Classic;
        set
        {
            if (value)
            {
                DiagramStyle = TaskDiagramStyle.Classic;
            }
        }
    }

    public double CanvasWidth
    {
        get => _canvasWidth;
        private set => SetProperty(ref _canvasWidth, value);
    }

    public double CanvasHeight
    {
        get => _canvasHeight;
        private set => SetProperty(ref _canvasHeight, value);
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
                _ = RefreshRelatedFilesAsync();
                UpdateCanvasSelection();
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

    public TaskRelatedFileNode? SelectedRelatedFile
    {
        get => _selectedRelatedFile;
        set
        {
            if (SetProperty(ref _selectedRelatedFile, value))
            {
                RaiseCommandStates();
            }
        }
    }

    public string TaskWorkspacePath
    {
        get => _taskWorkspacePath;
        private set => SetProperty(ref _taskWorkspacePath, value);
    }

    public bool HasTaskWorkspace => SelectedTask is not null && !string.IsNullOrWhiteSpace(SelectedTask.AttachedFilePath);

    public string RelatedFilesSummary
    {
        get => _relatedFilesSummary;
        private set => SetProperty(ref _relatedFilesSummary, value);
    }

    public bool HasRelatedFiles => RelatedFiles.Count > 0;

    public ICommand AddTaskCommand { get; }
    public ICommand SelectTaskCommand { get; }
    public ICommand SetListViewCommand { get; }
    public ICommand SetCanvasViewCommand { get; }
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
    public ICommand CreateWorkspaceFolderCommand { get; }
    public ICommand ChooseWorkspaceFolderCommand { get; }
    public ICommand NewTaskFileCommand { get; }
    public ICommand NewTaskFolderCommand { get; }
    public ICommand LinkExistingFileCommand { get; }
    public ICommand ImportExternalFileCommand { get; }
    public ICommand RenameRelatedFileCommand { get; }
    public ICommand OpenTaskWorkspaceCommand { get; }
    public ICommand RevealTaskWorkspaceCommand { get; }
    public ICommand OpenRelatedFileCommand { get; }
    public ICommand RevealRelatedFileCommand { get; }

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

        BuildCanvasLayout();
        OnPropertyChanged(nameof(EmptyStateVisibility));
    }

    private void BuildCanvasLayout()
    {
        CanvasTasks.Clear();
        CanvasConnectors.Clear();

        var taskList = FilteredTasks.ToList();
        const double nodeWidth = 250;
        const double nodeHeight = 104;
        const double horizontalGap = 48;
        const double verticalGap = 96;
        const double leftPadding = 24;
        const double topPadding = 24;

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

        var projectTitle = _projectService.CurrentProject?.ProjectName;
        if (string.IsNullOrWhiteSpace(projectTitle))
        {
            projectTitle = "ReachIT Project";
        }

        var root = TaskCanvasNode.CreateProject(projectTitle!, 0, topPadding, nodeWidth + 80, nodeHeight);
        CanvasTasks.Add(root);

        if (taskList.Count == 0)
        {
            root.Left = leftPadding;
            CanvasWidth = 960;
            CanvasHeight = 520;
            UpdateCanvasSelection();
            return;
        }

        var nodesByTask = new Dictionary<Guid, TaskCanvasNode>();
        var visited = new HashSet<Guid>();
        var nextLeaf = 0;

        double Place(TaskItem task, int depth)
        {
            if (!visited.Add(task.Id))
            {
                return leftPadding + nextLeaf * (nodeWidth + horizontalGap);
            }

            var children = byParent.TryGetValue(task.Id, out var childList)
                ? childList
                : [];

            double x;
            if (children.Count == 0)
            {
                x = leftPadding + nextLeaf * (nodeWidth + horizontalGap);
                nextLeaf++;
            }
            else
            {
                var childXs = children.Select(child => Place(child, depth + 1)).ToList();
                x = childXs.Average();
            }

            var node = TaskCanvasNode.CreateTask(task, x, topPadding + (depth + 1) * (nodeHeight + verticalGap), nodeWidth, nodeHeight);
            nodesByTask[task.Id] = node;
            CanvasTasks.Add(node);
            return x;
        }

        if (byParent.TryGetValue(Guid.Empty, out var roots))
        {
            foreach (var rootTask in roots)
            {
                Place(rootTask, 0);
            }
        }

        foreach (var task in taskList.Where(x => !visited.Contains(x.Id)))
        {
            Place(task, 0);
        }

        foreach (var task in taskList.Where(x => x.ParentTaskId.HasValue))
        {
            if (!nodesByTask.TryGetValue(task.Id, out var child) ||
                !nodesByTask.TryGetValue(task.ParentTaskId!.Value, out var parent))
            {
                continue;
            }

            CanvasConnectors.Add(TaskCanvasConnector.Create(parent.CenterX, parent.Bottom, child.CenterX, child.Top));
        }

        var rootChildren = taskList
            .Where(x => !x.ParentTaskId.HasValue || !taskIds.Contains(x.ParentTaskId.Value))
            .Where(x => nodesByTask.ContainsKey(x.Id))
            .Select(x => nodesByTask[x.Id])
            .ToList();

        if (rootChildren.Count > 0)
        {
            root.Left = Math.Max(leftPadding, rootChildren.Average(x => x.CenterX) - root.Width / 2);
            foreach (var child in rootChildren)
            {
                CanvasConnectors.Add(TaskCanvasConnector.Create(root.CenterX, root.Bottom, child.CenterX, child.Top));
            }
        }
        else
        {
            root.Left = leftPadding;
        }

        CanvasWidth = Math.Max(960, CanvasTasks.Max(x => x.Right) + leftPadding);
        CanvasHeight = Math.Max(520, CanvasTasks.Count == 0 ? 520 : CanvasTasks.Max(x => x.Top + x.Height) + topPadding);
        UpdateCanvasSelection();
    }

    private void SelectTask(TaskItem? task)
    {
        if (task is not null)
        {
            SelectedTask = task;
        }
    }

    private void UpdateCanvasSelection()
    {
        var selectedId = SelectedTask?.Id;
        foreach (var node in CanvasTasks)
        {
            node.IsSelected = node.Task is not null && selectedId.HasValue && node.Task.Id == selectedId.Value;
        }
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
        var isCompleting = !SelectedTask.IsCompleted && EditIsCompleted;
        if (isCompleting && !await ConfirmCompletionAsync(SelectedTask).ConfigureAwait(true))
        {
            return;
        }

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
        await RefreshRelatedFilesAsync().ConfigureAwait(true);
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
        await _taskBoardSyncService.ExportCurrentProjectAsync().ConfigureAwait(true);
        ApplyFilter();
        await RefreshRelatedFilesAsync().ConfigureAwait(true);
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
        await _taskBoardSyncService.ExportCurrentProjectAsync().ConfigureAwait(true);
        await LoadAsync().ConfigureAwait(true);
    }

    private async Task DeleteTaskAsync()
    {
        if (SelectedTask is null)
        {
            return;
        }

        await _taskService.DeleteTaskAsync(SelectedTask.Id).ConfigureAwait(true);
        await _taskBoardSyncService.ExportCurrentProjectAsync().ConfigureAwait(true);
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

        if (!await ConfirmCompletionAsync(task).ConfigureAwait(true))
        {
            return;
        }

        SelectedTask = task;
        task.IsCompleted = true;
        task.Status = "Done";
        task.CompletedAtUtc = DateTime.UtcNow;
        task.StartedAtUtc ??= task.CompletedAtUtc;
        await _taskService.UpdateTaskAsync(task).ConfigureAwait(true);
        await _taskBoardSyncService.ExportCurrentProjectAsync().ConfigureAwait(true);
        await LoadAsync().ConfigureAwait(true);
    }

    private async Task<bool> ConfirmCompletionAsync(TaskItem task)
    {
        var openSubtasks = GetTaskAndDescendants(task)
            .Skip(1)
            .Count(x => !x.IsCompleted);
        var links = await _taskService.GetTaskFileLinksAsync(task.Id, includeDescendants: true).ConfigureAwait(true);
        var linkedItems = links.Count > 0
            ? links.Select(x => x.FilePath).Distinct(StringComparer.OrdinalIgnoreCase).Count()
            : GetTaskAndDescendants(task)
                .Select(x => x.AttachedFilePath)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count();

        var doneCriteria = string.IsNullOrWhiteSpace(task.Description)
            ? "No done criteria written in the description."
            : task.Description.Trim();

        var message =
            $"Confirm that this task is complete:{Environment.NewLine}{Environment.NewLine}" +
            $"Task: {task.Title}{Environment.NewLine}" +
            $"Done criteria: {doneCriteria}{Environment.NewLine}" +
            $"Open subtasks: {openSubtasks}{Environment.NewLine}" +
            $"Linked files/folders: {linkedItems}{Environment.NewLine}{Environment.NewLine}" +
            "Use OK only when the result is ready and the useful files are linked to the task.";

        return MessageBox.Show(
            message,
            "Confirm task completion",
            MessageBoxButton.OKCancel,
            openSubtasks > 0 ? MessageBoxImage.Warning : MessageBoxImage.Question) == MessageBoxResult.OK;
    }

    private async Task CreateWorkspaceFolderAsync()
    {
        if (SelectedTask is null)
        {
            return;
        }

        var folderPath = CreateWorkspaceDirectory(SelectedTask);
        if (string.IsNullOrWhiteSpace(folderPath))
        {
            DeadlineValidationMessage = "Open a project before creating a task folder.";
            return;
        }

        SelectedTask.AttachedFilePath = folderPath;
        await _taskService.UpdateTaskAsync(SelectedTask).ConfigureAwait(true);
        await _taskService.LinkTaskFileAsync(_projectService.CurrentProject!.Id, SelectedTask.Id, folderPath, isDirectory: true, source: "TaskWorkspace")
            .ConfigureAwait(true);
        await _taskBoardSyncService.ExportCurrentProjectAsync().ConfigureAwait(true);
        RequestProjectTreeRefresh?.Invoke(this, EventArgs.Empty);
        await LoadAsync().ConfigureAwait(true);
        SelectedTask = Tasks.FirstOrDefault(x => x.Id == SelectedTask.Id);
    }

    private async Task ChooseWorkspaceFolderAsync()
    {
        if (SelectedTask is null)
        {
            return;
        }

        var project = _projectService.CurrentProject;
        if (project is null)
        {
            DeadlineValidationMessage = "Open a project before choosing a task folder.";
            return;
        }

        var folderPath = _dialogService.ShowOpenFolderDialog(project.ProjectDirectoryPath);
        if (string.IsNullOrWhiteSpace(folderPath))
        {
            return;
        }

        if (!IsInsideProject(project, folderPath))
        {
            DeadlineValidationMessage = "Choose a folder inside the current project.";
            return;
        }

        SelectedTask.AttachedFilePath = Path.GetFullPath(folderPath);
        await _taskService.UpdateTaskAsync(SelectedTask).ConfigureAwait(true);
        await _taskService.LinkTaskFileAsync(project.Id, SelectedTask.Id, SelectedTask.AttachedFilePath, isDirectory: true, source: "TaskWorkspace")
            .ConfigureAwait(true);
        await _taskBoardSyncService.ExportCurrentProjectAsync().ConfigureAwait(true);
        RequestProjectTreeRefresh?.Invoke(this, EventArgs.Empty);
        await LoadAsync().ConfigureAwait(true);
        SelectedTask = Tasks.FirstOrDefault(x => x.Id == SelectedTask.Id);
    }

    private async Task CreateLinkedFileAsync()
    {
        if (SelectedTask is null || _projectService.CurrentProject is not { } project)
        {
            return;
        }

        var targetDirectory = ResolveTaskExplorerTargetDirectory(project);
        Directory.CreateDirectory(targetDirectory);
        var filePath = GetUniquePath(targetDirectory, "NewFile", ".txt");
        await File.WriteAllTextAsync(filePath, string.Empty).ConfigureAwait(true);
        await _taskService.LinkTaskFileAsync(project.Id, SelectedTask.Id, filePath, isDirectory: false, source: "TaskExplorerNewFile")
            .ConfigureAwait(true);
        await _taskBoardSyncService.ExportCurrentProjectAsync().ConfigureAwait(true);
        RequestProjectTreeRefresh?.Invoke(this, EventArgs.Empty);
        await RefreshRelatedFilesAsync().ConfigureAwait(true);
    }

    private async Task CreateLinkedFolderAsync()
    {
        if (SelectedTask is null || _projectService.CurrentProject is not { } project)
        {
            return;
        }

        var targetDirectory = ResolveTaskExplorerTargetDirectory(project);
        Directory.CreateDirectory(targetDirectory);
        var folderPath = GetUniquePath(targetDirectory, "NewFolder", string.Empty);
        Directory.CreateDirectory(folderPath);
        await _taskService.LinkTaskFileAsync(project.Id, SelectedTask.Id, folderPath, isDirectory: true, source: "TaskExplorerNewFolder")
            .ConfigureAwait(true);
        await _taskBoardSyncService.ExportCurrentProjectAsync().ConfigureAwait(true);
        RequestProjectTreeRefresh?.Invoke(this, EventArgs.Empty);
        await RefreshRelatedFilesAsync().ConfigureAwait(true);
    }

    private async Task LinkExistingFileAsync()
    {
        if (SelectedTask is null || _projectService.CurrentProject is not { } project)
        {
            return;
        }

        var filePath = _dialogService.ShowOpenFileDialog("All Files (*.*)|*.*");
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return;
        }

        if (!IsInsideProject(project, filePath))
        {
            DeadlineValidationMessage = "Choose a file inside the current project.";
            return;
        }

        await _taskService.LinkTaskFileAsync(project.Id, SelectedTask.Id, filePath, isDirectory: false, source: "TaskExplorerExistingFile")
            .ConfigureAwait(true);
        await _taskBoardSyncService.ExportCurrentProjectAsync().ConfigureAwait(true);
        await RefreshRelatedFilesAsync().ConfigureAwait(true);
    }

    private async Task ImportExternalFileAsync()
    {
        if (SelectedTask is null || _projectService.CurrentProject is not { } project)
        {
            return;
        }

        var sourcePath = _dialogService.ShowOpenFileDialog("All Files (*.*)|*.*");
        if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
        {
            return;
        }

        var targetDirectory = ResolveTaskExplorerTargetDirectory(project);
        Directory.CreateDirectory(targetDirectory);
        var targetPath = GetUniqueImportedPath(targetDirectory, Path.GetFileName(sourcePath));
        File.Copy(sourcePath, targetPath, overwrite: false);

        await _taskService.LinkTaskFileAsync(project.Id, SelectedTask.Id, targetPath, isDirectory: false, source: "TaskExplorerImport")
            .ConfigureAwait(true);
        await _taskBoardSyncService.ExportCurrentProjectAsync().ConfigureAwait(true);
        RequestProjectTreeRefresh?.Invoke(this, EventArgs.Empty);
        await RefreshRelatedFilesAsync().ConfigureAwait(true);
    }

    private async Task RenameRelatedFileAsync()
    {
        if (SelectedRelatedFile is null || _projectService.CurrentProject is not { } project)
        {
            return;
        }

        var oldPath = SelectedRelatedFile.FullPath;
        if (!IsInsideProject(project, oldPath) || (!File.Exists(oldPath) && !Directory.Exists(oldPath)))
        {
            DeadlineValidationMessage = "Only existing project files can be renamed here.";
            return;
        }

        var currentName = Path.GetFileName(oldPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        var newName = Interaction.InputBox("Enter a new name:", "Rename task file", currentName).Trim();
        if (string.IsNullOrWhiteSpace(newName) || newName == currentName)
        {
            return;
        }

        if (newName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            DeadlineValidationMessage = "File name contains invalid characters.";
            return;
        }

        var parentDirectory = Path.GetDirectoryName(oldPath);
        if (string.IsNullOrWhiteSpace(parentDirectory))
        {
            return;
        }

        var newPath = Path.Combine(parentDirectory, newName);
        if (File.Exists(newPath) || Directory.Exists(newPath))
        {
            DeadlineValidationMessage = "A file or folder with this name already exists.";
            return;
        }

        if (SelectedRelatedFile.IsDirectory)
        {
            Directory.Move(oldPath, newPath);
        }
        else
        {
            File.Move(oldPath, newPath);
        }

        await _taskService.MoveLinkedPathAsync(project.Id, oldPath, newPath, SelectedRelatedFile.IsDirectory)
            .ConfigureAwait(true);
        await _taskBoardSyncService.ExportCurrentProjectAsync().ConfigureAwait(true);
        RequestProjectTreeRefresh?.Invoke(this, EventArgs.Empty);
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
            _ = RefreshRelatedFilesAsync();
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
        _ = RefreshRelatedFilesAsync();
        OnPropertyChanged(nameof(IsTaskSelected));
    }

    private void RaiseCommandStates()
    {
        _editTaskCommand.RaiseCanExecuteChanged();
        _deleteTaskCommand.RaiseCanExecuteChanged();
        _markCompletedCommand.RaiseCanExecuteChanged();
        if (CreateWorkspaceFolderCommand is AsyncCommand createWorkspace) createWorkspace.RaiseCanExecuteChanged();
        if (ChooseWorkspaceFolderCommand is AsyncCommand chooseWorkspace) chooseWorkspace.RaiseCanExecuteChanged();
        if (NewTaskFileCommand is AsyncCommand newTaskFile) newTaskFile.RaiseCanExecuteChanged();
        if (NewTaskFolderCommand is AsyncCommand newTaskFolder) newTaskFolder.RaiseCanExecuteChanged();
        if (LinkExistingFileCommand is AsyncCommand linkExistingFile) linkExistingFile.RaiseCanExecuteChanged();
        if (ImportExternalFileCommand is AsyncCommand importExternalFile) importExternalFile.RaiseCanExecuteChanged();
        if (RenameRelatedFileCommand is AsyncCommand renameRelatedFile) renameRelatedFile.RaiseCanExecuteChanged();
        if (OpenTaskWorkspaceCommand is RelayCommand openWorkspace) openWorkspace.RaiseCanExecuteChanged();
        if (RevealTaskWorkspaceCommand is RelayCommand revealWorkspace) revealWorkspace.RaiseCanExecuteChanged();
        if (OpenRelatedFileCommand is RelayCommand openRelated) openRelated.RaiseCanExecuteChanged();
        if (RevealRelatedFileCommand is RelayCommand revealRelated) revealRelated.RaiseCanExecuteChanged();
        OnPropertyChanged(nameof(HasTaskWorkspace));
        OnPropertyChanged(nameof(HasRelatedFiles));
    }

    private async Task RefreshRelatedFilesAsync()
    {
        RelatedFiles.Clear();
        SelectedRelatedFile = null;

        if (SelectedTask is null)
        {
            TaskWorkspacePath = "No workspace folder";
            RelatedFilesSummary = "No files linked to this task.";
            RaiseCommandStates();
            return;
        }

        TaskWorkspacePath = string.IsNullOrWhiteSpace(SelectedTask.AttachedFilePath)
            ? "No workspace folder"
            : SelectedTask.AttachedFilePath;

        var links = await _taskService.GetTaskFileLinksAsync(SelectedTask.Id, includeDescendants: true).ConfigureAwait(true);
        if (links.Count == 0)
        {
            links = GetLegacyLinks(SelectedTask).ToList();
        }

        var addedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var nodeBudget = 200;

        foreach (var link in links)
        {
            var path = link.FilePath;
            if (string.IsNullOrWhiteSpace(path) || !addedPaths.Add(path))
            {
                continue;
            }

            var taskTitle = Tasks.FirstOrDefault(t => t.Id == link.TaskItemId)?.Title ?? SelectedTask.Title;
            var node = BuildRelatedFileNode(path, taskTitle, ref nodeBudget);
            if (node is not null)
            {
                RelatedFiles.Add(node);
            }
        }

        RelatedFilesSummary = RelatedFiles.Count == 0
            ? "No files linked to this task or its subtasks."
            : $"{RelatedFiles.Count} linked workspace item(s).";

        OnPropertyChanged(nameof(HasRelatedFiles));
        RaiseCommandStates();
    }

    private IEnumerable<TaskFileLink> GetLegacyLinks(TaskItem root)
    {
        foreach (var task in GetTaskAndDescendants(root))
        {
            if (string.IsNullOrWhiteSpace(task.AttachedFilePath))
            {
                continue;
            }

            yield return new TaskFileLink
            {
                ProjectId = _projectService.CurrentProject?.Id ?? Guid.Empty,
                TaskItemId = task.Id,
                FilePath = task.AttachedFilePath,
                IsDirectory = Directory.Exists(task.AttachedFilePath),
                LinkedAtUtc = task.CompletedAtUtc ?? task.StartedAtUtc ?? DateTime.UtcNow,
                LinkSource = "Legacy"
            };
        }
    }

    private IEnumerable<TaskItem> GetTaskAndDescendants(TaskItem root)
    {
        yield return root;

        var byParent = Tasks
            .Where(t => t.ParentTaskId.HasValue)
            .GroupBy(t => t.ParentTaskId!.Value)
            .ToDictionary(g => g.Key, g => g.ToList());

        foreach (var child in Descendants(root.Id, new HashSet<Guid>()))
        {
            yield return child;
        }

        IEnumerable<TaskItem> Descendants(Guid parentId, HashSet<Guid> visited)
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

                yield return child;
                foreach (var descendant in Descendants(child.Id, visited))
                {
                    yield return descendant;
                }
            }
        }
    }

    private static TaskRelatedFileNode? BuildRelatedFileNode(string path, string taskTitle, ref int budget)
    {
        if (budget <= 0)
        {
            return null;
        }

        try
        {
            var fullPath = Path.GetFullPath(path);
            if (Directory.Exists(fullPath))
            {
                budget--;
                var directoryInfo = new DirectoryInfo(fullPath);
                var node = new TaskRelatedFileNode(directoryInfo.Name, fullPath, true, taskTitle);
                foreach (var directory in directoryInfo.EnumerateDirectories().OrderBy(x => x.Name))
                {
                    if (ShouldSkipDirectory(directory.Name))
                    {
                        continue;
                    }

                    var child = BuildRelatedFileNode(directory.FullName, taskTitle, ref budget);
                    if (child is not null)
                    {
                        node.Children.Add(child);
                    }
                }

                foreach (var file in directoryInfo.EnumerateFiles().OrderBy(x => x.Name))
                {
                    if (budget <= 0)
                    {
                        break;
                    }

                    budget--;
                    node.Children.Add(new TaskRelatedFileNode(file.Name, file.FullName, false, taskTitle));
                }

                return node;
            }

            if (File.Exists(fullPath))
            {
                budget--;
                return new TaskRelatedFileNode(Path.GetFileName(fullPath), fullPath, false, taskTitle);
            }
        }
        catch
        {
            return new TaskRelatedFileNode(path, path, false, taskTitle);
        }

        return new TaskRelatedFileNode(path, path, false, taskTitle);
    }

    private static bool ShouldSkipDirectory(string name)
    {
        return name.Equals(".git", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("bin", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("obj", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("node_modules", StringComparison.OrdinalIgnoreCase);
    }

    private string? CreateWorkspaceDirectory(TaskItem task)
    {
        var project = _projectService.CurrentProject;
        if (project is null || string.IsNullOrWhiteSpace(project.ProjectDirectoryPath))
        {
            return null;
        }

        var tasksRoot = Path.Combine(project.ProjectDirectoryPath, "Tasks");
        Directory.CreateDirectory(tasksRoot);

        var baseName = Slugify(task.Title);
        var folderPath = EnsureUniqueDirectory(Path.Combine(tasksRoot, baseName));
        Directory.CreateDirectory(folderPath);
        return folderPath;
    }

    private string ResolveTaskExplorerTargetDirectory(ProjectMeta project)
    {
        if (SelectedRelatedFile is { IsDirectory: true } selectedDirectory && IsInsideProject(project, selectedDirectory.FullPath))
        {
            return selectedDirectory.FullPath;
        }

        if (SelectedRelatedFile is { IsDirectory: false } selectedFile)
        {
            var parent = Path.GetDirectoryName(selectedFile.FullPath);
            if (!string.IsNullOrWhiteSpace(parent) && IsInsideProject(project, parent))
            {
                return parent;
            }
        }

        if (SelectedTask?.AttachedFilePath is { Length: > 0 } attachedPath)
        {
            if (Directory.Exists(attachedPath) && IsInsideProject(project, attachedPath))
            {
                return attachedPath;
            }

            var parent = Path.GetDirectoryName(attachedPath);
            if (!string.IsNullOrWhiteSpace(parent) && IsInsideProject(project, parent))
            {
                return parent;
            }
        }

        var taskRoot = Path.Combine(project.ProjectDirectoryPath, "Tasks", Slugify(SelectedTask?.Title ?? "New Task"));
        Directory.CreateDirectory(taskRoot);
        return taskRoot;
    }

    private static string GetUniquePath(string directoryPath, string baseName, string extension)
    {
        var index = 0;
        while (true)
        {
            var name = index == 0 ? $"{baseName}{extension}" : $"{baseName}{index}{extension}";
            var path = Path.Combine(directoryPath, name);
            if (!File.Exists(path) && !Directory.Exists(path))
            {
                return path;
            }

            index++;
        }
    }

    private static string GetUniqueImportedPath(string directoryPath, string fileName)
    {
        var nameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);
        var extension = Path.GetExtension(fileName);
        return GetUniquePath(directoryPath, string.IsNullOrWhiteSpace(nameWithoutExtension) ? "ImportedFile" : nameWithoutExtension, extension);
    }

    private static string EnsureUniqueDirectory(string folderPath)
    {
        if (!Directory.Exists(folderPath))
        {
            return folderPath;
        }

        var parent = Path.GetDirectoryName(folderPath) ?? string.Empty;
        var name = Path.GetFileName(folderPath);
        for (var i = 2; i < 1000; i++)
        {
            var candidate = Path.Combine(parent, $"{name} {i}");
            if (!Directory.Exists(candidate))
            {
                return candidate;
            }
        }

        return Path.Combine(parent, $"{name} {DateTime.Now:yyyyMMddHHmmss}");
    }

    private static string Slugify(string title)
    {
        var invalid = Path.GetInvalidFileNameChars().ToHashSet();
        var builder = new StringBuilder();
        foreach (var ch in title.Trim())
        {
            builder.Append(invalid.Contains(ch) ? '-' : ch);
        }

        var value = builder.ToString().Trim(' ', '.', '-');
        return string.IsNullOrWhiteSpace(value) ? "New Task" : value;
    }

    private static bool IsInsideProject(ProjectMeta project, string folderPath)
    {
        try
        {
            var root = Path.GetFullPath(project.ProjectDirectoryPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var target = Path.GetFullPath(folderPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return target.Equals(root, StringComparison.OrdinalIgnoreCase) ||
                   target.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private void OpenTaskWorkspace()
    {
        if (SelectedTask?.AttachedFilePath is { Length: > 0 } path)
        {
            OpenPath(path);
        }
    }

    private void RevealTaskWorkspace()
    {
        if (SelectedTask?.AttachedFilePath is { Length: > 0 } path)
        {
            RevealPath(path);
        }
    }

    private void OpenRelatedFile()
    {
        if (SelectedRelatedFile is not null)
        {
            OpenPath(SelectedRelatedFile.FullPath);
        }
    }

    private void RevealRelatedFile()
    {
        if (SelectedRelatedFile is not null)
        {
            RevealPath(SelectedRelatedFile.FullPath);
        }
    }

    private static void OpenPath(string path)
    {
        if (!File.Exists(path) && !Directory.Exists(path))
        {
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = path,
            UseShellExecute = true
        });
    }

    private static void RevealPath(string path)
    {
        if (!File.Exists(path) && !Directory.Exists(path))
        {
            return;
        }

        var target = Directory.Exists(path) ? $"\"{path}\"" : $"/select,\"{path}\"";
        Process.Start(new ProcessStartInfo
        {
            FileName = "explorer.exe",
            Arguments = target,
            UseShellExecute = true
        });
    }
}

public sealed class TaskRelatedFileNode
{
    public TaskRelatedFileNode(string name, string fullPath, bool isDirectory, string taskTitle)
    {
        Name = name;
        FullPath = fullPath;
        IsDirectory = isDirectory;
        TaskTitle = taskTitle;
    }

    public string Name { get; }
    public string FullPath { get; }
    public bool IsDirectory { get; }
    public string TaskTitle { get; }
    public ObservableCollection<TaskRelatedFileNode> Children { get; } = [];
    public string Kind => IsDirectory ? "Folder" : "File";
    public string IconKind => IsDirectory ? "FolderOpen" : "FileDocument";
}

public sealed class TaskCanvasNode : ViewModelBase
{
    private bool _isSelected;
    private double _left;

    private TaskCanvasNode(TaskItem? task, string title, string description, string status, string queuePositionText, bool isCompleted, bool isProjectRoot, double left, double top, double width, double height)
    {
        Task = task;
        Title = title;
        Description = description;
        Status = status;
        QueuePositionText = queuePositionText;
        IsCompleted = isCompleted;
        IsProjectRoot = isProjectRoot;
        _left = left;
        Top = top;
        Width = width;
        Height = height;
    }

    public static TaskCanvasNode CreateProject(string title, double left, double top, double width, double height)
    {
        return new TaskCanvasNode(null, title, "Project overview", "Project", string.Empty, false, true, left, top, width, height);
    }

    public static TaskCanvasNode CreateTask(TaskItem task, double left, double top, double width, double height)
    {
        return new TaskCanvasNode(
            task,
            task.Title,
            task.Description,
            task.Status,
            task.QueuePositionText,
            task.IsCompleted,
            false,
            left,
            top,
            width,
            height);
    }

    public TaskItem? Task { get; }
    public string Title { get; }
    public string Description { get; }
    public string Status { get; }
    public string QueuePositionText { get; }
    public bool IsCompleted { get; }
    public bool IsProjectRoot { get; }
    public double Left
    {
        get => _left;
        set
        {
            if (SetProperty(ref _left, value))
            {
                OnPropertyChanged(nameof(Right));
                OnPropertyChanged(nameof(CenterX));
            }
        }
    }

    public double Top { get; }
    public double Width { get; }
    public double Height { get; }
    public double Right => Left + Width;
    public double CenterX => Left + Width / 2;
    public double MiddleY => Top + Height / 2;
    public double Bottom => Top + Height;

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
}

public sealed record TaskCanvasConnector(double X1, double Y1, double X2, double Y2, double MidY)
{
    public static TaskCanvasConnector Create(double x1, double y1, double x2, double y2)
    {
        return new TaskCanvasConnector(x1, y1, x2, y2, y1 + (y2 - y1) / 2);
    }
}

public enum TaskDiagramStyle
{
    ReachIt,
    Classic
}
