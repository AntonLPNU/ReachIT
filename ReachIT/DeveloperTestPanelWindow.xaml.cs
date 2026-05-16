using System.Diagnostics;
using System.IO;
using System.Windows;
using ReachIT.Application.Contracts;

namespace ReachIT;

public partial class DeveloperTestPanelWindow : Window
{
    private readonly IProjectService _projectService;
    private readonly IDeveloperProjectGeneratorService _developerProjectGeneratorService;
    private readonly Func<Task> _refreshWorkspaceAsync;

    public DeveloperTestPanelWindow(
        IProjectService projectService,
        IDeveloperProjectGeneratorService developerProjectGeneratorService,
        Func<Task> refreshWorkspaceAsync)
    {
        _projectService = projectService;
        _developerProjectGeneratorService = developerProjectGeneratorService;
        _refreshWorkspaceAsync = refreshWorkspaceAsync;
        InitializeComponent();
        RefreshProjectText();
        AppendOutput("Developer panel ready.");
    }

    private async void OnGenerateDemoProjectClick(object sender, RoutedEventArgs e)
    {
        await RunPanelActionAsync("Generating demo project", async () =>
        {
            AppendOutput("Choose where to create the demo project...");
            var project = await _developerProjectGeneratorService.GenerateDemoProjectAsync().ConfigureAwait(true);
            if (project is null)
            {
                AppendOutput("Demo project generation cancelled.");
                return;
            }

            AppendOutput($"Demo project generated: {project.ProjectDirectoryPath}");
            RefreshProjectText();
            await _refreshWorkspaceAsync().ConfigureAwait(true);
        }).ConfigureAwait(true);
    }

    private async void OnGenerateRandomFilesClick(object sender, RoutedEventArgs e)
    {
        await RunPanelActionAsync("Generating random files", async () =>
        {
            if (!EnsureProject())
            {
                return;
            }

            var count = ReadCount();
            var files = await _developerProjectGeneratorService
                .GenerateRandomFilesAsync(_projectService.CurrentProject!, count)
                .ConfigureAwait(true);

            var areaCount = files
                .Select(GetDeveloperArea)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count();
            AppendOutput($"Generated {files.Count} random files and {1 + areaCount + files.Count * 4} linked hierarchy tasks.");
            AppendFiles(files);
            await _refreshWorkspaceAsync().ConfigureAwait(true);
        }).ConfigureAwait(true);
    }

    private async void OnGenerateFormatCatalogClick(object sender, RoutedEventArgs e)
    {
        await RunPanelActionAsync("Generating format catalog", async () =>
        {
            if (!EnsureProject())
            {
                return;
            }

            var files = await _developerProjectGeneratorService
                .GenerateFormatCatalogAsync(_projectService.CurrentProject!)
                .ConfigureAwait(true);

            AppendOutput($"Generated format catalog with {files.Count} files.");
            AppendFiles(files);
            await _refreshWorkspaceAsync().ConfigureAwait(true);
        }).ConfigureAwait(true);
    }

    private async void OnGenerateTestTasksClick(object sender, RoutedEventArgs e)
    {
        await RunPanelActionAsync("Generating test tasks", async () =>
        {
            if (!EnsureProject())
            {
                return;
            }

            var count = ReadCount();
            var created = await _developerProjectGeneratorService
                .GenerateTestTasksAsync(_projectService.CurrentProject!, count)
                .ConfigureAwait(true);

            AppendOutput($"Generated {created} test tasks.");
            await _refreshWorkspaceAsync().ConfigureAwait(true);
        }).ConfigureAwait(true);
    }

    private async void OnTouchFilesClick(object sender, RoutedEventArgs e)
    {
        await RunPanelActionAsync("Touching project files", async () =>
        {
            if (!EnsureProject())
            {
                return;
            }

            var count = ReadCount();
            var files = await _developerProjectGeneratorService
                .TouchRandomFilesAsync(_projectService.CurrentProject!, count)
                .ConfigureAwait(true);

            AppendOutput($"Touched {files.Count} files. Activity tracking should pick up changes if it is enabled.");
            AppendFiles(files);
            await _refreshWorkspaceAsync().ConfigureAwait(true);
        }).ConfigureAwait(true);
    }

    private async void OnOpenRandomFileClick(object sender, RoutedEventArgs e)
    {
        await RunPanelActionAsync("Opening random file", async () =>
        {
            if (!EnsureProject())
            {
                return;
            }

            var file = _developerProjectGeneratorService.PickRandomProjectFile(_projectService.CurrentProject!);
            if (string.IsNullOrWhiteSpace(file))
            {
                AppendOutput("No files found in current project.");
                return;
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = file,
                UseShellExecute = true
            });

            AppendOutput($"Opened random file: {file}");
            await _refreshWorkspaceAsync().ConfigureAwait(true);
        }).ConfigureAwait(true);
    }

    private async void OnOpenProjectFolderClick(object sender, RoutedEventArgs e)
    {
        await RunPanelActionAsync("Opening project folder", () =>
        {
            if (!EnsureProject())
            {
                return Task.CompletedTask;
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"\"{_projectService.CurrentProject!.ProjectDirectoryPath}\"",
                UseShellExecute = true
            });
            AppendOutput($"Opened folder: {_projectService.CurrentProject.ProjectDirectoryPath}");
            return Task.CompletedTask;
        }).ConfigureAwait(true);
    }

    private async void OnRefreshWorkspaceClick(object sender, RoutedEventArgs e)
    {
        await RunPanelActionAsync("Refreshing workspace", async () =>
        {
            await _refreshWorkspaceAsync().ConfigureAwait(true);
            RefreshProjectText();
            AppendOutput("Workspace refreshed.");
        }).ConfigureAwait(true);
    }

    private void OnShowDiagnosticsClick(object sender, RoutedEventArgs e)
    {
        if (!EnsureProject())
        {
            return;
        }

        var project = _projectService.CurrentProject!;
        var files = Directory.Exists(project.ProjectDirectoryPath)
            ? Directory.EnumerateFiles(project.ProjectDirectoryPath, "*.*", SearchOption.AllDirectories).Count()
            : 0;
        var folders = Directory.Exists(project.ProjectDirectoryPath)
            ? Directory.EnumerateDirectories(project.ProjectDirectoryPath, "*", SearchOption.AllDirectories).Count()
            : 0;
        AppendOutput("Diagnostics:");
        AppendOutput($"- Name: {project.ProjectName}");
        AppendOutput($"- Folder: {project.ProjectDirectoryPath}");
        AppendOutput($"- .reachit: {project.RitFilePath}");
        AppendOutput($"- Files: {files}, folders: {folders}");
        AppendOutput($"- Developer tools enabled: {ReachIT.Application.DeveloperTools.IsEnabled}");
    }

    private void OnClearOutputClick(object sender, RoutedEventArgs e)
    {
        OutputTextBox.Clear();
    }

    private int ReadCount()
    {
        return int.TryParse(CountTextBox.Text, out var count)
            ? Math.Clamp(count, 1, 200)
            : 20;
    }

    private void RefreshProjectText()
    {
        ProjectText.Text = _projectService.CurrentProject is null
            ? ResourceText("Developer.CurrentProjectNone", "Current project: none")
            : $"{ResourceText("Developer.CurrentProjectPrefix", "Current project")}: {_projectService.CurrentProject.ProjectName}";
        ProjectPathText.Text = _projectService.CurrentProject?.ProjectDirectoryPath ?? "No project folder selected.";
    }

    private void AppendFiles(IEnumerable<string> files)
    {
        foreach (var file in files.Take(20))
        {
            AppendOutput($"- {file}");
        }
    }

    private void AppendOutput(string message)
    {
        OutputTextBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
        OutputTextBox.ScrollToEnd();
    }

    private bool EnsureProject()
    {
        if (_projectService.CurrentProject is not null)
        {
            return true;
        }

        AppendOutput("No current project. Generate or open a project first.");
        return false;
    }

    private async Task RunPanelActionAsync(string status, Func<Task> action)
    {
        try
        {
            SetBusy(status);
            await action().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            AppendOutput($"ERROR: {ex.Message}");
            MessageBox.Show(ex.Message, ResourceText("Developer.MessageTitle", "ReachIT Developer"), MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            RefreshProjectText();
            SetBusy("Ready", isBusy: false);
        }
    }

    private void SetBusy(string status, bool isBusy = true)
    {
        StatusText.Text = status;
        ActionPanel.IsEnabled = !isBusy;
    }

    private static string GetDeveloperArea(string filePath)
    {
        var marker = $"{Path.DirectorySeparatorChar}DevTest{Path.DirectorySeparatorChar}";
        var markerIndex = filePath.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (markerIndex < 0)
        {
            return "Generated";
        }

        var start = markerIndex + marker.Length;
        var end = filePath.IndexOf(Path.DirectorySeparatorChar, start);
        return end > start
            ? filePath[start..end]
            : "Generated";
    }

    private static string ResourceText(string key, string fallback)
    {
        return System.Windows.Application.Current?.TryFindResource(key) as string ?? fallback;
    }
}
