// Coordinates workspace shell behavior, explorer actions, and view navigation.
using Microsoft.VisualBasic;
using System.IO;
using System.Windows;
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
    private readonly IFileSystemProjectExplorerService _fileSystemProjectExplorerService;
    private readonly IWorkUnitService _workUnitService;
    private readonly INavigationService _navigationService;
    private readonly IDialogService _dialogService;
    private readonly RelayCommand _goBackCommand;
    private readonly RelayCommand _goForwardCommand;

    private object? _currentWorkspaceViewModel;
    private bool _isAppBarModeEnabled;

    public event EventHandler? RequestToggleSidePanel;
    public event EventHandler? RequestHideSidePanel;
    public event EventHandler? RequestOpenMainWorkspace;
    public event EventHandler? RequestToggleAppBarMode;
    public event EventHandler? RequestExitApplication;

    public MainViewModel(
        IProjectService projectService,
        IExternalResourceService externalResourceService,
        IFileSystemProjectExplorerService fileSystemProjectExplorerService,
        IWorkUnitService workUnitService,
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
        _fileSystemProjectExplorerService = fileSystemProjectExplorerService;
        _workUnitService = workUnitService;
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
        ExitApplicationCommand = new RelayCommand(_ => RequestExitApplication?.Invoke(this, EventArgs.Empty));

        NewFileCommand = new AsyncCommand(_ => CreateNewFileAsync());
        NewFolderCommand = new AsyncCommand(_ => CreateNewFolderAsync());
        RefreshTreeCommand = new AsyncCommand(_ => RefreshTreeAsync());
        OpenSelectedNodeCommand = new AsyncCommand(_ => OpenSelectedNodeAsync());
        RevealSelectedNodeCommand = new RelayCommand(_ => RevealSelectedNode());
        RenameSelectedNodeCommand = new AsyncCommand(_ => RenameSelectedNodeAsync());
        DeleteSelectedNodeCommand = new AsyncCommand(_ => DeleteSelectedNodeAsync());
        CollapseAllCommand = new RelayCommand(_ => ProjectTreeViewModel.CollapseAll());
        MoreActionsCommand = new RelayCommand(_ =>
        {
            // TODO: Open explorer context action list.
        });
        AttachExternalFileCommand = new AsyncCommand(_ => AttachExternalFileAsync());
        CopyIntoProjectCommand = new AsyncCommand(_ => CopyIntoProjectAsync());
        SaveAsLinkCommand = new AsyncCommand(_ => SaveAsLinkAsync());

        OpenTaskManagerCommand = new RelayCommand(_ =>
        {
            TaskManagerViewModel.ShowAllTasks();
            Navigate(TaskManagerViewModel);
        });
        OpenStatisticsCommand = new RelayCommand(_ => Navigate(StatisticsViewModel));
        OpenPlanningCommand = new RelayCommand(_ => Navigate(PlanningViewModel));
        OpenVersionsCommand = new RelayCommand(_ => Navigate(VersionsViewModel));
        OpenSettingsCommand = new RelayCommand(_ => Navigate(SettingsViewModel));
        OpenMainViewCommand = new RelayCommand(_ => Navigate(MainDashboardViewModel));
        OpenProjectInfoCommand = new RelayCommand(_ => Navigate(ProjectInfoViewModel));
        OpenFocusCommand = new RelayCommand(_ => Navigate(FocusModeViewModel));
        _goBackCommand = new RelayCommand(_ => GoBack(), _ => _navigationService.CanGoBack);
        _goForwardCommand = new RelayCommand(_ => GoForward(), _ => _navigationService.CanGoForward);
        GoBackCommand = _goBackCommand;
        GoForwardCommand = _goForwardCommand;

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

        MainDashboardViewModel.RequestOpenSidePanel += (_, _) => RequestToggleSidePanel?.Invoke(this, EventArgs.Empty);
        MainDashboardViewModel.RequestOpenProjectSettings += (_, _) => Navigate(SettingsViewModel);
        MainDashboardViewModel.RequestOpenTaskManager += (_, _) =>
        {
            TaskManagerViewModel.ShowAllTasks();
            Navigate(TaskManagerViewModel);
        };
        MainDashboardViewModel.RequestOpenActiveTasks += (_, _) =>
        {
            TaskManagerViewModel.ShowActiveTasks();
            Navigate(TaskManagerViewModel);
        };
        MainDashboardViewModel.RequestOpenStatistics += (_, _) => Navigate(StatisticsViewModel);
        MainDashboardViewModel.RequestRefreshTree += async (_, _) => await RefreshTreeAsync().ConfigureAwait(true);

        _navigationService.Navigated += OnNavigated;
        _navigationService.NavigationStateChanged += (_, _) =>
        {
            _goBackCommand.RaiseCanExecuteChanged();
            _goForwardCommand.RaiseCanExecuteChanged();
        };
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
    public ICommand ExitApplicationCommand { get; }
    public ICommand NewFileCommand { get; }
    public ICommand NewFolderCommand { get; }
    public ICommand RefreshTreeCommand { get; }
    public ICommand RenameSelectedNodeCommand { get; }
    public ICommand DeleteSelectedNodeCommand { get; }
    public ICommand OpenSelectedNodeCommand { get; }
    public ICommand RevealSelectedNodeCommand { get; }
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
    public ICommand GoBackCommand { get; }
    public ICommand GoForwardCommand { get; }
    public ICommand StartFocusCommand { get; }
    public ICommand StopFocusCommand { get; }
    public ICommand SelectTreeNodeCommand { get; }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await SettingsViewModel.LoadAsync(cancellationToken).ConfigureAwait(true);
        ProjectTreeViewModel.SetNodes(await _projectService.GetCurrentTreeAsync(cancellationToken).ConfigureAwait(true), preserveState: false);
        await MainDashboardViewModel.LoadAsync(cancellationToken).ConfigureAwait(true);
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

    private async Task RefreshTreeAsync(string? preferredSelectedPath = null, IEnumerable<string>? additionalExpandedPaths = null)
    {
        var nodes = await _projectService.GetCurrentTreeAsync().ConfigureAwait(true);
        ProjectTreeViewModel.SetNodes(
            nodes,
            preserveState: true,
            preferredSelectedPath: preferredSelectedPath,
            additionalExpandedPaths: additionalExpandedPaths);

        if (ProjectTreeViewModel.SelectedNode is not null)
        {
            SelectTreeNode(ProjectTreeViewModel.SelectedNode);
        }
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
                _ = MainDashboardViewModel.LoadAsync();
                Navigate(MainDashboardViewModel);
                break;
            case ProjectTreeNodeType.RitConfigFile:
                _ = MainDashboardViewModel.LoadAsync();
                Navigate(MainDashboardViewModel);
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
        await MainDashboardViewModel.LoadAsync().ConfigureAwait(true);
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
        await MainDashboardViewModel.LoadAsync().ConfigureAwait(true);
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
        await MainDashboardViewModel.LoadAsync().ConfigureAwait(true);
        await RecentExternalFilesPanelViewModel.LoadAsync().ConfigureAwait(true);
    }

    private async Task CreateNewFileAsync()
    {
        if (_projectService.CurrentProject is null)
        {
            return;
        }

        var selectedNode = ProjectTreeViewModel.SelectedNode;
        var createdNode = await _projectService.CreateInternalFileAsync(selectedNode).ConfigureAwait(true);
        if (createdNode is null)
        {
            return;
        }

        var parentDirectoryPath = ResolveTargetDirectoryPath(_projectService.CurrentProject.ProjectDirectoryPath, selectedNode);
        if (!ProjectTreeViewModel.TryAddNode(createdNode, parentDirectoryPath))
        {
            await RefreshTreeAsync(
                preferredSelectedPath: createdNode.FullPath,
                additionalExpandedPaths: [parentDirectoryPath])
                .ConfigureAwait(true);
            await MainDashboardViewModel.LoadAsync().ConfigureAwait(true);
            return;
        }

        SelectTreeNode(ProjectTreeViewModel.SelectedNode);
        await MainDashboardViewModel.LoadAsync().ConfigureAwait(true);
    }

    private async Task CreateNewFolderAsync()
    {
        if (_projectService.CurrentProject is null)
        {
            return;
        }

        var selectedNode = ProjectTreeViewModel.SelectedNode;
        var createdNode = await _projectService.CreateFolderAsync(selectedNode).ConfigureAwait(true);
        if (createdNode is null)
        {
            return;
        }

        var parentDirectoryPath = ResolveTargetDirectoryPath(_projectService.CurrentProject.ProjectDirectoryPath, selectedNode);
        if (!ProjectTreeViewModel.TryAddNode(createdNode, parentDirectoryPath))
        {
            await RefreshTreeAsync(
                preferredSelectedPath: createdNode.FullPath,
                additionalExpandedPaths: [parentDirectoryPath])
                .ConfigureAwait(true);
            await MainDashboardViewModel.LoadAsync().ConfigureAwait(true);
            return;
        }

        SelectTreeNode(ProjectTreeViewModel.SelectedNode);
        await MainDashboardViewModel.LoadAsync().ConfigureAwait(true);
    }

    private async Task OpenSelectedNodeAsync()
    {
        var selectedNode = ProjectTreeViewModel.SelectedNode;
        if (selectedNode is null)
        {
            return;
        }

        if (selectedNode.NodeType == ProjectTreeNodeType.RitConfigFile
            || selectedNode.NodeType == ProjectTreeNodeType.ProjectRoot
            || selectedNode.FullPath.EndsWith(".rit", StringComparison.OrdinalIgnoreCase))
        {
            var projectFolderPath = selectedNode.NodeType == ProjectTreeNodeType.ProjectRoot
                ? selectedNode.FullPath
                : Path.GetDirectoryName(selectedNode.FullPath);

            if (!string.IsNullOrWhiteSpace(projectFolderPath))
            {
                var openedProject = await _projectService.OpenProjectAsync(projectFolderPath).ConfigureAwait(true);
                if (openedProject is not null)
                {
                    await InitializeAsync().ConfigureAwait(true);
                    Navigate(MainDashboardViewModel);
                    return;
                }
            }
        }

        try
        {
            _fileSystemProjectExplorerService.OpenWithDefaultApp(selectedNode);
            if (_projectService.CurrentProject is not null)
            {
                await _workUnitService.RecordFileActivityAsync(_projectService.CurrentProject, selectedNode.FullPath).ConfigureAwait(true);
                await MainDashboardViewModel.LoadAsync().ConfigureAwait(true);
            }

            if (SettingsViewModel.HideSidePanelAfterExternalFileOpen && ShouldAutoHideSidePanelForOpenedNode(selectedNode))
            {
                RequestHideSidePanel?.Invoke(this, EventArgs.Empty);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "ReachIT", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private static bool ShouldAutoHideSidePanelForOpenedNode(ProjectTreeNode node)
    {
        if (node.IsDirectory)
        {
            return false;
        }

        if (node.NodeType is ProjectTreeNodeType.RitConfigFile or ProjectTreeNodeType.ProjectRoot)
        {
            return false;
        }

        var extension = Path.GetExtension(node.FullPath);
        if (string.IsNullOrWhiteSpace(extension))
        {
            return true;
        }

        return !extension.Equals(".txt", StringComparison.OrdinalIgnoreCase)
               && !extension.Equals(".md", StringComparison.OrdinalIgnoreCase)
               && !extension.Equals(".json", StringComparison.OrdinalIgnoreCase)
               && !extension.Equals(".rit", StringComparison.OrdinalIgnoreCase)
               && !extension.Equals(".cs", StringComparison.OrdinalIgnoreCase)
               && !extension.Equals(".xaml", StringComparison.OrdinalIgnoreCase)
               && !extension.Equals(".png", StringComparison.OrdinalIgnoreCase)
               && !extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase)
               && !extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase);
    }

    private void RevealSelectedNode()
    {
        var selectedNode = ProjectTreeViewModel.SelectedNode;
        if (selectedNode is null)
        {
            return;
        }

        try
        {
            _fileSystemProjectExplorerService.RevealInExplorer(selectedNode);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "ReachIT", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async Task RenameSelectedNodeAsync()
    {
        if (_projectService.CurrentProject is null)
        {
            return;
        }

        var selectedNode = ProjectTreeViewModel.SelectedNode;
        if (selectedNode is null)
        {
            return;
        }

        var newName = Interaction.InputBox("Enter a new name:", "Rename", selectedNode.Name);
        if (string.IsNullOrWhiteSpace(newName) || string.Equals(newName, selectedNode.Name, StringComparison.Ordinal))
        {
            return;
        }

        try
        {
            await _fileSystemProjectExplorerService
                .RenameNodeAsync(_projectService.CurrentProject, selectedNode, newName)
                .ConfigureAwait(true);

            if (!ProjectTreeViewModel.TryRenameNode(selectedNode, newName, out var renamedFullPath))
            {
                await RefreshTreeAsync().ConfigureAwait(true);
                await MainDashboardViewModel.LoadAsync().ConfigureAwait(true);
                return;
            }

            ProjectTreeViewModel.SelectNodeByPath(renamedFullPath);
            SelectTreeNode(ProjectTreeViewModel.SelectedNode);
            await MainDashboardViewModel.LoadAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "ReachIT", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async Task DeleteSelectedNodeAsync()
    {
        if (_projectService.CurrentProject is null)
        {
            return;
        }

        var selectedNode = ProjectTreeViewModel.SelectedNode;
        if (selectedNode is null)
        {
            return;
        }

        var confirmation = MessageBox.Show(
            $"Delete '{selectedNode.Name}'?",
            "ReachIT",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (confirmation != MessageBoxResult.Yes)
        {
            return;
        }

        if (selectedNode.IsDirectory)
        {
            var strongConfirmation = MessageBox.Show(
                "Folder delete can remove important files. Continue?",
                "ReachIT",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (strongConfirmation != MessageBoxResult.Yes)
            {
                return;
            }
        }

        try
        {
            var deletedPath = selectedNode.FullPath;
            await _fileSystemProjectExplorerService
                .DeleteNodeAsync(_projectService.CurrentProject, selectedNode)
                .ConfigureAwait(true);

            if (!ProjectTreeViewModel.TryRemoveNode(deletedPath, out var fallbackSelectionNode))
            {
                var fallbackPath = Path.GetDirectoryName(deletedPath);
                await RefreshTreeAsync(preferredSelectedPath: fallbackPath, additionalExpandedPaths: [fallbackPath ?? string.Empty]).ConfigureAwait(true);
                await MainDashboardViewModel.LoadAsync().ConfigureAwait(true);
                return;
            }

            if (fallbackSelectionNode is not null)
            {
                SelectTreeNode(fallbackSelectionNode);
            }

            await MainDashboardViewModel.LoadAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "ReachIT", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private static string ResolveTargetDirectoryPath(string projectDirectoryPath, ProjectTreeNode? selectedNode)
    {
        if (selectedNode is null || selectedNode.IsExternal)
        {
            return projectDirectoryPath;
        }

        if (selectedNode.IsDirectory)
        {
            return selectedNode.FullPath;
        }

        var parentDirectory = Path.GetDirectoryName(selectedNode.FullPath);
        return string.IsNullOrWhiteSpace(parentDirectory)
            ? projectDirectoryPath
            : parentDirectory;
    }

    private void Navigate(object target)
    {
        _navigationService.NavigateTo(target);
    }

    private void GoBack()
    {
        _navigationService.GoBack();
    }

    private void GoForward()
    {
        _navigationService.GoForward();
    }

    private void OnNavigated(object? sender, object? target)
    {
        CurrentWorkspaceViewModel = target;
    }
}
