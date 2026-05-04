// Handles project create/open and .rit metadata persistence for folder-based projects.
using System.IO;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ReachIT.Application.Contracts;
using ReachIT.Domain.Enums;
using ReachIT.Domain.Models;

namespace ReachIT.Application.Services;

public sealed class ProjectService : IProjectService
{
    private readonly IDialogService _dialogService;
    private readonly IRecentFilesService _recentFilesService;
    private readonly IDatabaseService _databaseService;
    private readonly IFileSystemProjectExplorerService _fileSystemProjectExplorerService;

    public ProjectService(
        IDialogService dialogService,
        IRecentFilesService recentFilesService,
        IDatabaseService databaseService,
        IFileSystemProjectExplorerService fileSystemProjectExplorerService)
    {
        _dialogService = dialogService;
        _recentFilesService = recentFilesService;
        _databaseService = databaseService;
        _fileSystemProjectExplorerService = fileSystemProjectExplorerService;
    }

    public ProjectMeta? CurrentProject { get; private set; }

    public async Task<ProjectMeta> CreateProjectAsync(CreateProjectRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.ProjectName))
        {
            throw new ArgumentException("Project name is required.", nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.SaveLocation))
        {
            throw new ArgumentException("Save location is required.", nameof(request));
        }

        var safeProjectName = string.Join("_", request.ProjectName.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries));
        if (string.IsNullOrWhiteSpace(safeProjectName))
        {
            safeProjectName = "ReachITProject";
        }

        var projectDirectory = Path.Combine(request.SaveLocation, safeProjectName);
        Directory.CreateDirectory(projectDirectory);
        CreateTemplateStructure(projectDirectory, request.TemplateType);

        var meta = new ProjectMeta
        {
            Id = Guid.NewGuid(),
            ProjectName = request.ProjectName,
            Description = request.Description,
            ProjectDirectoryPath = projectDirectory,
            RitFilePath = Path.Combine(projectDirectory, ".reachit.json"),
            TemplateType = request.TemplateType,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };

        await SaveRitFileAsync(meta, cancellationToken).ConfigureAwait(false);
        await UpsertProjectMetaAsync(meta, cancellationToken).ConfigureAwait(false);

        CurrentProject = meta;
        await _recentFilesService.AddRecentProjectAsync(meta, cancellationToken).ConfigureAwait(false);

        foreach (var externalPath in request.InitialExternalFiles)
        {
            await _recentFilesService.AddRecentExternalFileAsync(new RecentExternalFileItem
            {
                DisplayName = Path.GetFileName(externalPath),
                SourcePathOrUrl = externalPath,
                LastOpenedAtUtc = DateTime.UtcNow
            }, cancellationToken).ConfigureAwait(false);
        }

        return meta;
    }

    private static void CreateTemplateStructure(string projectDirectory, ProjectTemplateType templateType)
    {
        IReadOnlyList<string> folders = templateType switch
        {
            ProjectTemplateType.StudyProject => ["Notes", "Materials", "Reports", "Tasks", "Sources"],
            ProjectTemplateType.FreelanceProject => ["Client", "Files", "Deliverables", "Tasks", "Versions", "References"],
            ProjectTemplateType.CreativeProject => ["Assets", "References", "Exports", "Versions", "Notes"],
            ProjectTemplateType.ResearchProject => ["Sources", "Notes", "Links", "Drafts", "Results"],
            _ => []
        };

        foreach (var folder in folders)
        {
            Directory.CreateDirectory(Path.Combine(projectDirectory, folder));
        }
    }

    public async Task<ProjectMeta?> OpenProjectFromDialogAsync(CancellationToken cancellationToken = default)
    {
        var selectedFolder = _dialogService.ShowOpenFolderDialog();
        if (string.IsNullOrWhiteSpace(selectedFolder))
        {
            return null;
        }

        return await OpenProjectAsync(selectedFolder, cancellationToken).ConfigureAwait(false);
    }

    public async Task<ProjectMeta?> OpenProjectAsync(string projectFolderPath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(projectFolderPath) || !Directory.Exists(projectFolderPath))
        {
            return null;
        }

        var ritPath = Path.Combine(projectFolderPath, ".reachit.json");
        var meta = await LoadProjectMetaFromRitAsync(ritPath, projectFolderPath, cancellationToken).ConfigureAwait(false);
        if (meta is null)
        {
            return null;
        }

        if (!File.Exists(meta.RitFilePath))
        {
            await SaveRitFileAsync(meta, cancellationToken).ConfigureAwait(false);
        }

        await UpsertProjectMetaAsync(meta, cancellationToken).ConfigureAwait(false);

        CurrentProject = meta;
        await _recentFilesService.AddRecentProjectAsync(meta, cancellationToken).ConfigureAwait(false);
        return meta;
    }

    public async Task SaveProjectAsync(CancellationToken cancellationToken = default)
    {
        if (CurrentProject is null)
        {
            return;
        }

        CurrentProject.UpdatedAtUtc = DateTime.UtcNow;
        await UpsertProjectMetaAsync(CurrentProject, cancellationToken).ConfigureAwait(false);
        await SaveRitFileAsync(CurrentProject, cancellationToken).ConfigureAwait(false);
    }

    public Task SaveAllAsync(CancellationToken cancellationToken = default)
    {
        // TODO: Save task manager, focus, and settings state snapshot together with .rit metadata.
        return Task.CompletedTask;
    }

    public async Task<IReadOnlyList<ProjectTreeNode>> GetCurrentTreeAsync(CancellationToken cancellationToken = default)
    {
        if (CurrentProject is null)
        {
            return [];
        }

        return await _fileSystemProjectExplorerService.BuildTreeAsync(CurrentProject, cancellationToken).ConfigureAwait(false);
    }

    public async Task<ProjectTreeNode?> CreateInternalFileAsync(ProjectTreeNode? selectedNode, CancellationToken cancellationToken = default)
    {
        if (CurrentProject is null)
        {
            return null;
        }

        return await _fileSystemProjectExplorerService.CreateFileAsync(CurrentProject, selectedNode, cancellationToken).ConfigureAwait(false);
    }

    public async Task<ProjectTreeNode?> CreateFolderAsync(ProjectTreeNode? selectedNode, CancellationToken cancellationToken = default)
    {
        if (CurrentProject is null)
        {
            return null;
        }

        return await _fileSystemProjectExplorerService.CreateFolderAsync(CurrentProject, selectedNode, cancellationToken).ConfigureAwait(false);
    }

    public Task<IReadOnlyList<ProjectMeta>> GetRecentProjectsAsync(CancellationToken cancellationToken = default)
    {
        return _recentFilesService.GetRecentProjectsAsync(cancellationToken);
    }

    private async Task<ProjectMeta?> LoadProjectMetaFromRitAsync(string ritFilePath, string projectFolderPath, CancellationToken cancellationToken)
    {
        if (File.Exists(ritFilePath))
        {
            try
            {
                var json = await File.ReadAllTextAsync(ritFilePath, cancellationToken).ConfigureAwait(false);
                var meta = JsonSerializer.Deserialize<ProjectMeta>(json);
                if (meta is not null)
                {
                    if (meta.Id == Guid.Empty)
                    {
                        meta.Id = Guid.NewGuid();
                    }

                    meta.ProjectDirectoryPath = projectFolderPath;
                    meta.RitFilePath = ritFilePath;
                    meta.UpdatedAtUtc = DateTime.UtcNow;
                    return meta;
                }
            }
            catch (JsonException)
            {
                // TODO: Support legacy JSON formats.
            }
        }

        var fallback = new ProjectMeta
        {
            Id = Guid.NewGuid(),
            ProjectName = Path.GetFileName(projectFolderPath),
            ProjectDirectoryPath = projectFolderPath,
            RitFilePath = ritFilePath,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };

        await SaveRitFileAsync(fallback, cancellationToken).ConfigureAwait(false);
        return fallback;
    }

    private async Task SaveRitFileAsync(ProjectMeta meta, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Serialize(meta, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(meta.RitFilePath, payload, cancellationToken).ConfigureAwait(false);
    }

    private async Task UpsertProjectMetaAsync(ProjectMeta meta, CancellationToken cancellationToken)
    {
        await using var dbContext = _databaseService.CreateDbContext();
        var existing = await dbContext.Projects.FirstOrDefaultAsync(x => x.Id == meta.Id, cancellationToken).ConfigureAwait(false);
        if (existing is null)
        {
            dbContext.Projects.Add(meta);
        }
        else
        {
            existing.ProjectName = meta.ProjectName;
            existing.Description = meta.Description;
            existing.ProjectDirectoryPath = meta.ProjectDirectoryPath;
            existing.RitFilePath = meta.RitFilePath;
            existing.TemplateType = meta.TemplateType;
            existing.UpdatedAtUtc = DateTime.UtcNow;
        }

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
