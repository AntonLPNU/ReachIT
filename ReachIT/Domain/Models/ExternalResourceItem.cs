// Represents a connected external resource for a project.
using ReachIT.Domain.Enums;

namespace ReachIT.Domain.Models;

public class ExternalResourceItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProjectMetaId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string SourcePathOrUrl { get; set; } = string.Empty;
    public string? StoredPath { get; set; }
    public ExternalResourceType ResourceType { get; set; } = ExternalResourceType.ExternalFile;
    public ExternalResourceAttachMode AttachMode { get; set; } = ExternalResourceAttachMode.LinkOnly;
    public DateTime AddedAtUtc { get; set; } = DateTime.UtcNow;
}
