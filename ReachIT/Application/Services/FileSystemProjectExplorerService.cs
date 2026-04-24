// Reads project filesystem structure and builds explorer nodes.
using System.IO;
using ReachIT.Application.Contracts;
using ReachIT.Domain.Enums;
using ReachIT.Domain.Models;

namespace ReachIT.Application.Services;

public sealed class FileSystemProjectExplorerService : IFileSystemProjectExplorerService
{
    private readonly IExternalResourceService _externalResourceService;

    public FileSystemProjectExplorerService(IExternalResourceService externalResourceService)
    {
        _externalResourceService = externalResourceService;
    }

    public async Task<IReadOnlyList<ProjectTreeNode>> BuildTreeAsync(ProjectMeta project, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(project.ProjectDirectoryPath) || !Directory.Exists(project.ProjectDirectoryPath))
        {
            return [];
        }

        var rootDirectory = new DirectoryInfo(project.ProjectDirectoryPath);
        var root = BuildDirectoryNode(project, rootDirectory, parentRelativePath: string.Empty, isRoot: true);

        var externalResources = await _externalResourceService.GetByProjectAsync(project.Id, cancellationToken).ConfigureAwait(false);
        if (externalResources.Count > 0)
        {
            var externalRoot = new ProjectTreeNode
            {
                Id = Guid.NewGuid(),
                ProjectMetaId = project.Id,
                Name = "External Resources",
                FullPath = project.ProjectDirectoryPath,
                RelativePath = "External Resources",
                NodeType = ProjectTreeNodeType.VirtualNode,
                IsDirectory = true,
                IsExternal = true
            };

            foreach (var resource in externalResources)
            {
                var nodeType = resource.ResourceType switch
                {
                    ExternalResourceType.WebLink => ProjectTreeNodeType.WebLink,
                    ExternalResourceType.OfflinePage => ProjectTreeNodeType.OfflinePage,
                    _ => ProjectTreeNodeType.ExternalFileLink
                };

                externalRoot.Children.Add(new ProjectTreeNode
                {
                    Id = Guid.NewGuid(),
                    ProjectMetaId = project.Id,
                    ParentId = externalRoot.Id,
                    Name = resource.DisplayName,
                    FullPath = resource.StoredPath ?? resource.SourcePathOrUrl,
                    RelativePath = resource.DisplayName,
                    NodeType = nodeType,
                    IsDirectory = false,
                    IsExternal = true,
                    ExternalTargetPathOrUrl = resource.SourcePathOrUrl
                });
            }

            root.Children.Add(externalRoot);
        }

        return [root];
    }

    public async Task<ProjectTreeNode?> CreateFileAsync(ProjectMeta project, ProjectTreeNode? selectedNode, CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(project.ProjectDirectoryPath))
        {
            return null;
        }

        var targetDirectoryPath = ResolveTargetDirectory(project, selectedNode);
        Directory.CreateDirectory(targetDirectoryPath);

        var fileName = GetUniqueFileName(targetDirectoryPath, "NewFile", ".txt");
        var filePath = Path.Combine(targetDirectoryPath, fileName);
        await File.WriteAllTextAsync(filePath, string.Empty, cancellationToken).ConfigureAwait(false);

        return BuildFileNode(project, new FileInfo(filePath), Path.GetRelativePath(project.ProjectDirectoryPath, targetDirectoryPath));
    }

    public Task<ProjectTreeNode?> CreateFolderAsync(ProjectMeta project, ProjectTreeNode? selectedNode, CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(project.ProjectDirectoryPath))
        {
            return Task.FromResult<ProjectTreeNode?>(null);
        }

        var targetDirectoryPath = ResolveTargetDirectory(project, selectedNode);
        Directory.CreateDirectory(targetDirectoryPath);

        var folderName = GetUniqueFolderName(targetDirectoryPath, "NewFolder");
        var folderPath = Path.Combine(targetDirectoryPath, folderName);
        Directory.CreateDirectory(folderPath);

        var directoryNode = BuildDirectoryNode(
            project,
            new DirectoryInfo(folderPath),
            Path.GetRelativePath(project.ProjectDirectoryPath, targetDirectoryPath),
            isRoot: false);

        return Task.FromResult<ProjectTreeNode?>(directoryNode);
    }

    private static ProjectTreeNode BuildDirectoryNode(ProjectMeta project, DirectoryInfo directory, string parentRelativePath, bool isRoot)
    {
        var relativePath = Path.GetRelativePath(project.ProjectDirectoryPath, directory.FullName);
        if (relativePath == ".")
        {
            relativePath = string.Empty;
        }

        var node = new ProjectTreeNode
        {
            Id = Guid.NewGuid(),
            ProjectMetaId = project.Id,
            Name = isRoot ? directory.Name : directory.Name,
            FullPath = directory.FullName,
            RelativePath = relativePath,
            NodeType = isRoot ? ProjectTreeNodeType.ProjectRoot : ProjectTreeNodeType.Folder,
            IsDirectory = true,
            IsExternal = false,
            ParentId = null
        };

        foreach (var childDirectory in directory.GetDirectories().OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase))
        {
            var childNode = BuildDirectoryNode(project, childDirectory, relativePath, isRoot: false);
            childNode.ParentId = node.Id;
            node.Children.Add(childNode);
        }

        foreach (var file in directory.GetFiles().OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase))
        {
            var childNode = BuildFileNode(project, file, relativePath);
            childNode.ParentId = node.Id;
            node.Children.Add(childNode);
        }

        return node;
    }

    private static ProjectTreeNode BuildFileNode(ProjectMeta project, FileInfo file, string parentRelativePath)
    {
        var relativePath = Path.GetRelativePath(project.ProjectDirectoryPath, file.FullName);
        var nodeType = file.Extension.Equals(".rit", StringComparison.OrdinalIgnoreCase)
            ? ProjectTreeNodeType.RitConfigFile
            : ProjectTreeNodeType.File;

        return new ProjectTreeNode
        {
            Id = Guid.NewGuid(),
            ProjectMetaId = project.Id,
            Name = file.Name,
            FullPath = file.FullName,
            RelativePath = relativePath,
            NodeType = nodeType,
            IsDirectory = false,
            IsExternal = false
        };
    }

    private static string ResolveTargetDirectory(ProjectMeta project, ProjectTreeNode? selectedNode)
    {
        if (selectedNode is null || selectedNode.IsExternal)
        {
            return project.ProjectDirectoryPath;
        }

        if (selectedNode.IsDirectory && IsInsideProject(project.ProjectDirectoryPath, selectedNode.FullPath))
        {
            return selectedNode.FullPath;
        }

        var fileDirectory = Path.GetDirectoryName(selectedNode.FullPath);
        if (!string.IsNullOrWhiteSpace(fileDirectory) && IsInsideProject(project.ProjectDirectoryPath, fileDirectory))
        {
            return fileDirectory;
        }

        return project.ProjectDirectoryPath;
    }

    private static bool IsInsideProject(string projectRootPath, string candidatePath)
    {
        var fullRoot = Path.GetFullPath(projectRootPath).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var fullCandidate = Path.GetFullPath(candidatePath);
        return fullCandidate.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(fullCandidate.TrimEnd(Path.DirectorySeparatorChar), projectRootPath.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase);
    }

    private static string GetUniqueFileName(string directoryPath, string baseName, string extension)
    {
        var fileName = $"{baseName}{extension}";
        var index = 1;
        while (File.Exists(Path.Combine(directoryPath, fileName)))
        {
            fileName = $"{baseName}{index}{extension}";
            index++;
        }

        return fileName;
    }

    private static string GetUniqueFolderName(string directoryPath, string baseName)
    {
        var folderName = baseName;
        var index = 1;
        while (Directory.Exists(Path.Combine(directoryPath, folderName)))
        {
            folderName = $"{baseName}{index}";
            index++;
        }

        return folderName;
    }
}
