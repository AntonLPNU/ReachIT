// Represents a logical node in the ReachIT project explorer.
using System.Collections.ObjectModel;
using ReachIT.Domain.Enums;

namespace ReachIT.Domain.Models;

public class ProjectTreeNode
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? ProjectMetaId { get; set; }
    public Guid? ParentId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string FullPath { get; set; } = string.Empty;
    public string RelativePath { get; set; } = string.Empty;
    public ProjectTreeNodeType NodeType { get; set; } = ProjectTreeNodeType.Folder;
    public bool IsDirectory { get; set; }
    public bool IsExternal { get; set; }
    public string? ExternalTargetPathOrUrl { get; set; }
    public bool IsExpanded { get; set; }
    public ObservableCollection<ProjectTreeNode> Children { get; set; } = [];
}
