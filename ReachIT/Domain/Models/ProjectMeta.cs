// Contains project-level metadata and .rit entry information.
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
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
