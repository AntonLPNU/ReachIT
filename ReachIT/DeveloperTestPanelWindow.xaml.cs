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
    }

    private async void OnGenerateDemoProjectClick(object sender, RoutedEventArgs e)
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
    }

    private async void OnGenerateRandomFilesClick(object sender, RoutedEventArgs e)
    {
        if (_projectService.CurrentProject is null)
        {
            AppendOutput("No current project. Generate or open a project first.");
            return;
        }

        var count = ReadCount();
        var files = await _developerProjectGeneratorService
            .GenerateRandomFilesAsync(_projectService.CurrentProject, count)
            .ConfigureAwait(true);

        var areaCount = files
            .Select(GetDeveloperArea)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();
        AppendOutput($"Generated {files.Count} random files and {1 + areaCount + files.Count * 4} linked hierarchy tasks.");
        AppendFiles(files);
        await _refreshWorkspaceAsync().ConfigureAwait(true);
    }

    private async void OnGenerateFormatCatalogClick(object sender, RoutedEventArgs e)
    {
        if (_projectService.CurrentProject is null)
        {
            AppendOutput("No current project. Generate or open a project first.");
            return;
        }

        var files = await _developerProjectGeneratorService
            .GenerateFormatCatalogAsync(_projectService.CurrentProject)
            .ConfigureAwait(true);

        AppendOutput($"Generated format catalog with {files.Count} files.");
        AppendFiles(files);
        await _refreshWorkspaceAsync().ConfigureAwait(true);
    }

    private async void OnTouchFilesClick(object sender, RoutedEventArgs e)
    {
        if (_projectService.CurrentProject is null)
        {
            AppendOutput("No current project. Generate or open a project first.");
            return;
        }

        var count = ReadCount();
        var files = await _developerProjectGeneratorService
            .TouchRandomFilesAsync(_projectService.CurrentProject, count)
            .ConfigureAwait(true);

        AppendOutput($"Touched {files.Count} files. Activity tracking should pick up changes if it is enabled.");
        AppendFiles(files);
        await _refreshWorkspaceAsync().ConfigureAwait(true);
    }

    private async void OnOpenRandomFileClick(object sender, RoutedEventArgs e)
    {
        if (_projectService.CurrentProject is null)
        {
            AppendOutput("No current project. Generate or open a project first.");
            return;
        }

        var file = _developerProjectGeneratorService.PickRandomProjectFile(_projectService.CurrentProject);
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
