// Stores logical project items loaded from a .rit workspace.
using ReachIT.Domain.Enums;

namespace ReachIT.Domain.Models;

public class ProjectItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProjectMetaId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string RelativePath { get; set; } = string.Empty;
    public ProjectItemType ItemType { get; set; } = ProjectItemType.InternalFile;
    public bool IsLinkedOnly { get; set; }
}
