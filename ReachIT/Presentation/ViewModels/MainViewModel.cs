// Coordinates workspace shell behavior, explorer actions, and view navigation.
using System.Windows.Input;
using ReachIT.Application.Contracts;
using ReachIT.Domain.Enums;
using ReachIT.Domain.Models;
using ReachIT.Presentation.Commands;

namespace ReachIT.Presentation.ViewModels;

public sealed class MainViewModel : ViewModelBase
{
    private readonly IProjectService _projectService;
    private readonly IExternalResourceService _externalResourceService;
    private readonly INavigationService _navigationService;
    private readonly IDialogService _dialogService;

    private object? _currentWorkspaceViewModel;
    private bool _isAppBarModeEnabled;

    public event EventHandler? RequestToggleSidePanel;
    public event EventHandler? RequestOpenMainWorkspace;
    public event EventHandler? RequestToggleAppBarMode;

    public MainViewModel(
        IProjectService projectService,
        IExternalResourceService externalResourceService,
        INavigationService navigationService,
        IDialogService dialogService,
        ProjectTreeViewModel projectTreeViewModel,
        RecentExternalFilesPanelViewModel recentExternalFilesPanelViewModel,
        MainDashboardViewModel mainDashboardViewModel,
        ProjectInfoViewModel projectInfoViewModel,
        FileViewModel fileViewModel,
        TaskManagerViewModel taskManagerViewModel,
        StatisticsViewModel statisticsViewModel,
        PlanningViewModel planningViewModel,
        VersionsViewModel versionsViewModel,
        SettingsViewModel settingsViewModel,
        FocusModeViewModel focusModeViewModel)
    {
        _projectService = projectService;
        _externalResourceService = externalResourceService;
        _navigationService = navigationService;
        _dialogService = dialogService;

        ProjectTreeViewModel = projectTreeViewModel;
        RecentExternalFilesPanelViewModel = recentExternalFilesPanelViewModel;

        MainDashboardViewModel = mainDashboardViewModel;
        ProjectInfoViewModel = projectInfoViewModel;
        FileViewModel = fileViewModel;
        TaskManagerViewModel = taskManagerViewModel;
        StatisticsViewModel = statisticsViewModel;
        PlanningViewModel = planningViewModel;
        VersionsViewModel = versionsViewModel;
        SettingsViewModel = settingsViewModel;
        FocusModeViewModel = focusModeViewModel;

        OpenProjectCommand = new AsyncCommand(_ => OpenProjectAsync());
        SaveCommand = new AsyncCommand(_ => _projectService.SaveProjectAsync());
        SaveAllCommand = new AsyncCommand(_ => _projectService.SaveAllAsync());
        ToggleSidePanelCommand = new RelayCommand(_ => RequestToggleSidePanel?.Invoke(this, EventArgs.Empty));
        OpenMainWorkspaceCommand = new RelayCommand(_ => RequestOpenMainWorkspace?.Invoke(this, EventArgs.Empty));
        ToggleAppBarModeCommand = new RelayCommand(_ => RequestToggleAppBarMode?.Invoke(this, EventArgs.Empty));

        NewFileCommand = new AsyncCommand(_ => CreateNewFileAsync());
        NewFolderCommand = new AsyncCommand(_ => CreateNewFolderAsync());
        RefreshTreeCommand = new AsyncCommand(_ => RefreshTreeAsync());
        CollapseAllCommand = new RelayCommand(_ => ProjectTreeViewModel.CollapseAll());
        MoreActionsCommand = new RelayCommand(_ =>
        {
            // TODO: Open explorer context action list.
        });
        AttachExternalFileCommand = new AsyncCommand(_ => AttachExternalFileAsync());
        CopyIntoProjectCommand = new AsyncCommand(_ => CopyIntoProjectAsync());
        SaveAsLinkCommand = new AsyncCommand(_ => SaveAsLinkAsync());

        OpenTaskManagerCommand = new RelayCommand(_ => Navigate(TaskManagerViewModel));
        OpenStatisticsCommand = new RelayCommand(_ => Navigate(StatisticsViewModel));
        OpenPlanningCommand = new RelayCommand(_ => Navigate(PlanningViewModel));
        OpenVersionsCommand = new RelayCommand(_ => Navigate(VersionsViewModel));
        OpenSettingsCommand = new RelayCommand(_ => Navigate(SettingsViewModel));
        OpenMainViewCommand = new RelayCommand(_ => Navigate(MainDashboardViewModel));
        OpenProjectInfoCommand = new RelayCommand(_ => Navigate(ProjectInfoViewModel));
        OpenFocusCommand = new RelayCommand(_ => Navigate(FocusModeViewModel));

        StartFocusCommand = new AsyncCommand(_ =>
        {
            FocusModeViewModel.StartCommand.Execute(null);
            return Task.CompletedTask;
        });
        StopFocusCommand = new AsyncCommand(_ =>
        {
            FocusModeViewModel.StopCommand.Execute(null);
            return Task.CompletedTask;
        });

        SelectTreeNodeCommand = new RelayCommand(p => SelectTreeNode(p as ProjectTreeNode));

        _navigationService.Navigated += OnNavigated;
        Navigate(MainDashboardViewModel);
    }

    public ProjectTreeViewModel ProjectTreeViewModel { get; }
    public RecentExternalFilesPanelViewModel RecentExternalFilesPanelViewModel { get; }

    public MainDashboardViewModel MainDashboardViewModel { get; }
    public ProjectInfoViewModel ProjectInfoViewModel { get; }
    public FileViewModel FileViewModel { get; }
    public TaskManagerViewModel TaskManagerViewModel { get; }
    public StatisticsViewModel StatisticsViewModel { get; }
    public PlanningViewModel PlanningViewModel { get; }
    public VersionsViewModel VersionsViewModel { get; }
    public SettingsViewModel SettingsViewModel { get; }
    public FocusModeViewModel FocusModeViewModel { get; }

    public object? CurrentWorkspaceViewModel
    {
        get => _currentWorkspaceViewModel;
        private set => SetProperty(ref _currentWorkspaceViewModel, value);
    }

    public bool IsAppBarModeEnabled
    {
        get => _isAppBarModeEnabled;
        private set => SetProperty(ref _isAppBarModeEnabled, value);
    }

    public ICommand OpenProjectCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand SaveAllCommand { get; }
    public ICommand ToggleSidePanelCommand { get; }
    public ICommand OpenMainWorkspaceCommand { get; }
    public ICommand ToggleAppBarModeCommand { get; }
    public ICommand NewFileCommand { get; }
    public ICommand NewFolderCommand { get; }
    public ICommand RefreshTreeCommand { get; }
    public ICommand CollapseAllCommand { get; }
    public ICommand MoreActionsCommand { get; }
    public ICommand AttachExternalFileCommand { get; }
    public ICommand CopyIntoProjectCommand { get; }
    public ICommand SaveAsLinkCommand { get; }
    public ICommand OpenTaskManagerCommand { get; }
    public ICommand OpenStatisticsCommand { get; }
    public ICommand OpenPlanningCommand { get; }
    public ICommand OpenVersionsCommand { get; }
    public ICommand OpenSettingsCommand { get; }
    public ICommand OpenMainViewCommand { get; }
    public ICommand OpenProjectInfoCommand { get; }
    public ICommand OpenFocusCommand { get; }
    public ICommand StartFocusCommand { get; }
    public ICommand StopFocusCommand { get; }
    public ICommand SelectTreeNodeCommand { get; }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        ProjectTreeViewModel.SetNodes(await _projectService.GetCurrentTreeAsync(cancellationToken).ConfigureAwait(true));
        await TaskManagerViewModel.LoadAsync(cancellationToken).ConfigureAwait(true);
        await StatisticsViewModel.LoadAsync(cancellationToken).ConfigureAwait(true);
        await RecentExternalFilesPanelViewModel.LoadAsync(cancellationToken).ConfigureAwait(true);
    }

    public void SetAppBarMode(bool enabled)
    {
        IsAppBarModeEnabled = enabled;
    }

    private async Task OpenProjectAsync()
    {
        var project = await _projectService.OpenProjectFromDialogAsync().ConfigureAwait(true);
        if (project is not null)
        {
            await InitializeAsync().ConfigureAwait(true);
        }
    }

    private async Task RefreshTreeAsync()
    {
        var nodes = await _projectService.GetCurrentTreeAsync().ConfigureAwait(true);
        ProjectTreeViewModel.SetNodes(nodes);
    }

    private void SelectTreeNode(ProjectTreeNode? node)
    {
        if (node is null)
        {
            return;
        }

        ProjectTreeViewModel.SelectedNode = node;

        switch (node.NodeType)
        {
            case ProjectTreeNodeType.ProjectRoot:
                Navigate(ProjectInfoViewModel);
                break;
            case ProjectTreeNodeType.RitConfigFile:
                Navigate(ProjectInfoViewModel);
                break;
            default:
                FileViewModel.SelectedNodeName = node.Name;
                FileViewModel.SelectedRelativePath = node.RelativePath;
                FileViewModel.SelectedFullPath = node.FullPath;
                Navigate(FileViewModel);
                break;
        }
    }

    private async Task AttachExternalFileAsync()
    {
        if (_projectService.CurrentProject is null)
        {
            return;
        }

        var sourcePath = RecentExternalFilesPanelViewModel.SelectedItem?.SourcePathOrUrl;
        if (string.IsNullOrWhiteSpace(sourcePath))
        {
            sourcePath = _dialogService.ShowOpenFileDialog("All Files (*.*)|*.*");
            if (string.IsNullOrWhiteSpace(sourcePath))
            {
                return;
            }
        }

        await _externalResourceService.AttachAsync(_projectService.CurrentProject.Id, sourcePath).ConfigureAwait(true);
        await RefreshTreeAsync().ConfigureAwait(true);
        await RecentExternalFilesPanelViewModel.LoadAsync().ConfigureAwait(true);
    }

    private async Task CopyIntoProjectAsync()
    {
        if (_projectService.CurrentProject is null)
        {
            return;
        }

        var sourcePath = RecentExternalFilesPanelViewModel.SelectedItem?.SourcePathOrUrl;
        if (string.IsNullOrWhiteSpace(sourcePath))
        {
            sourcePath = _dialogService.ShowOpenFileDialog("All Files (*.*)|*.*");
            if (string.IsNullOrWhiteSpace(sourcePath))
            {
                return;
            }
        }

        await _externalResourceService.CopyIntoProjectAsync(_projectService.CurrentProject.Id, sourcePath).ConfigureAwait(true);
        await RefreshTreeAsync().ConfigureAwait(true);
        await RecentExternalFilesPanelViewModel.LoadAsync().ConfigureAwait(true);
    }

    private async Task SaveAsLinkAsync()
    {
        if (_projectService.CurrentProject is null)
        {
            return;
        }

        var sourcePath = RecentExternalFilesPanelViewModel.SelectedItem?.SourcePathOrUrl;
        if (string.IsNullOrWhiteSpace(sourcePath))
        {
            sourcePath = _dialogService.ShowOpenFileDialog("All Files (*.*)|*.*");
            if (string.IsNullOrWhiteSpace(sourcePath))
            {
                // TODO: Add dedicated input for WebLink/URL values in explorer actions.
                return;
            }
        }

        await _externalResourceService.SaveAsLinkAsync(_projectService.CurrentProject.Id, sourcePath).ConfigureAwait(true);
        await RefreshTreeAsync().ConfigureAwait(true);
        await RecentExternalFilesPanelViewModel.LoadAsync().ConfigureAwait(true);
    }

    private async Task CreateNewFileAsync()
    {
        if (_projectService.CurrentProject is null)
        {
            return;
        }

        var selectedNode = ProjectTreeViewModel.SelectedNode;
        await _projectService.CreateInternalFileAsync(selectedNode).ConfigureAwait(true);
        await RefreshTreeAsync().ConfigureAwait(true);
    }

    private async Task CreateNewFolderAsync()
    {
        if (_projectService.CurrentProject is null)
        {
            return;
        }

        var selectedNode = ProjectTreeViewModel.SelectedNode;
        await _projectService.CreateFolderAsync(selectedNode).ConfigureAwait(true);
        await RefreshTreeAsync().ConfigureAwait(true);
    }

    private void Navigate(object target)
    {
        _navigationService.NavigateTo(target);
    }

    private void OnNavigated(object? sender, object? target)
    {
        CurrentWorkspaceViewModel = target;
    }
}
