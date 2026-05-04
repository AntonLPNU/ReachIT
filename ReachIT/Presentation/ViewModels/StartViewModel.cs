// Drives start menu actions before opening workspace.
using System.IO;
using System.Diagnostics;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using ReachIT.Application.Contracts;
using ReachIT.Domain.Models;
using ReachIT.Presentation.Commands;

namespace ReachIT.Presentation.ViewModels;

public sealed class StartViewModel : ViewModelBase
{
    private readonly IProjectService _projectService;
    private readonly IRecentFilesService _recentFilesService;

    public StartViewModel(IProjectService projectService, IRecentFilesService recentFilesService)
    {
        _projectService = projectService;
        _recentFilesService = recentFilesService;

        OpenProjectCommand = new AsyncCommand(_ => OpenProjectAsync());
        CreateProjectCommand = new RelayCommand(_ => RequestCreateProject?.Invoke(this, EventArgs.Empty));
        OpenRecentProjectCommand = new AsyncCommand(p => OpenRecentProjectAsync(p as ProjectMeta));
        RemoveRecentProjectCommand = new AsyncCommand(p => RemoveRecentProjectAsync(p as ProjectMeta));
        OpenRecentProjectFolderCommand = new AsyncCommand(p => OpenRecentProjectFolderAsync(p as ProjectMeta));
        OpenRecentExternalFileCommand = new RelayCommand(_ =>
        {
            // TODO: Open preview/attach flow for selected recent external file.
        });
        OpenSettingsCommand = new RelayCommand(_ => RequestOpenSettings?.Invoke(this, EventArgs.Empty));
        ExitCommand = new RelayCommand(_ => RequestClose?.Invoke(this, false));
    }

    public ObservableCollection<ProjectMeta> RecentProjects { get; } = new();
    public ObservableCollection<RecentExternalFileItem> RecentExternalFiles { get; } = new();

    public string? OpenedProjectFolderPath { get; private set; }

    public ICommand OpenProjectCommand { get; }
    public ICommand CreateProjectCommand { get; }
    public ICommand OpenRecentProjectCommand { get; }
    public ICommand RemoveRecentProjectCommand { get; }
    public ICommand OpenRecentProjectFolderCommand { get; }
    public ICommand OpenRecentExternalFileCommand { get; }
    public ICommand OpenSettingsCommand { get; }
    public ICommand ExitCommand { get; }

    public event EventHandler<bool>? RequestClose;
    public event EventHandler? RequestCreateProject;
    public event EventHandler? RequestOpenSettings;

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        RecentProjects.Clear();
        var projects = await _projectService.GetRecentProjectsAsync(cancellationToken).ConfigureAwait(true);
        foreach (var project in projects)
        {
            RecentProjects.Add(project);
        }

        RecentExternalFiles.Clear();
        var external = await _recentFilesService.GetRecentExternalFilesAsync(cancellationToken).ConfigureAwait(true);
        foreach (var item in external)
        {
            RecentExternalFiles.Add(item);
        }
    }

    public void CompleteOpen(string projectFolderPath)
    {
        OpenedProjectFolderPath = projectFolderPath;
        RequestClose?.Invoke(this, true);
    }

    private async Task OpenProjectAsync()
    {
        var project = await _projectService.OpenProjectFromDialogAsync().ConfigureAwait(true);
        if (!string.IsNullOrWhiteSpace(project?.ProjectDirectoryPath))
        {
            CompleteOpen(project.ProjectDirectoryPath);
        }
    }

    private async Task OpenRecentProjectAsync(ProjectMeta? project)
    {
        if (project is null || string.IsNullOrWhiteSpace(project.ProjectDirectoryPath))
        {
            return;
        }

        if (!Directory.Exists(project.ProjectDirectoryPath))
        {
            MessageBox.Show("Project folder no longer exists. It will be removed from recent list.", "ReachIT", MessageBoxButton.OK, MessageBoxImage.Warning);
            await _recentFilesService.RemoveRecentProjectAsync(project.Id).ConfigureAwait(true);
            await LoadAsync().ConfigureAwait(true);
            return;
        }

        var opened = await _projectService.OpenProjectAsync(project.ProjectDirectoryPath).ConfigureAwait(true);
        if (!string.IsNullOrWhiteSpace(opened?.ProjectDirectoryPath))
        {
            CompleteOpen(opened.ProjectDirectoryPath);
        }
    }

    private async Task RemoveRecentProjectAsync(ProjectMeta? project)
    {
        if (project is null)
        {
            return;
        }

        var result = MessageBox.Show(
            "Remove this project from recent list?",
            "ReachIT",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes)
        {
            return;
        }

        await _recentFilesService.RemoveRecentProjectAsync(project.Id).ConfigureAwait(true);
        await LoadAsync().ConfigureAwait(true);
    }

    private async Task OpenRecentProjectFolderAsync(ProjectMeta? project)
    {
        if (project is null || string.IsNullOrWhiteSpace(project.ProjectDirectoryPath))
        {
            return;
        }

        if (!Directory.Exists(project.ProjectDirectoryPath))
        {
            var removeResult = MessageBox.Show(
                "Project folder not found. Remove this item from recent list?",
                "ReachIT",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (removeResult == MessageBoxResult.Yes)
            {
                await _recentFilesService.RemoveRecentProjectAsync(project.Id).ConfigureAwait(true);
                await LoadAsync().ConfigureAwait(true);
            }

            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = "explorer.exe",
            Arguments = $"\"{project.ProjectDirectoryPath}\"",
            UseShellExecute = true
        });
    }
}
