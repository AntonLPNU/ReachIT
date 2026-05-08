// Contains project-level metadata and .rit entry information.
using System.ComponentModel.DataAnnotations.Schema;
using ReachIT.Domain.Enums;

namespace ReachIT.Domain.Models;

public class ProjectMeta
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string ProjectName { get; set; } = "ReachIT Project";
    public string Description { get; set; } = string.Empty;
    public string ProjectDirectoryPath { get; set; } = string.Empty;
    public string RitFilePath { get; set; } = string.Empty;
    public ProjectTemplateType TemplateType { get; set; } = ProjectTemplateType.EmptyProject;
    public bool UseProjectActivitySettings { get; set; }
    public bool ProjectEnableActivityTracking { get; set; } = true;
    public bool ProjectTrackActiveWindow { get; set; } = true;
    public bool ProjectTrackFileChanges { get; set; } = true;
    public bool ProjectTrackGitChanges { get; set; } = true;
    public bool ProjectTrackTextStatistics { get; set; } = true;
    public bool ProjectPauseActivityTracking { get; set; }
    public string ProjectIgnoredFoldersSerialized { get; set; } = "bin;obj;.git;.vs;node_modules;packages;build;dist";
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    [NotMapped]
    public List<string> ProjectIgnoredFolders
    {
        get => ProjectIgnoredFoldersSerialized
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
        set => ProjectIgnoredFoldersSerialized = string.Join(';', value);
    }
}
