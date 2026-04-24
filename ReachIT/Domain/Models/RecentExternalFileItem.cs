// Represents a recent external resource shown in quick-access panel.
using ReachIT.Domain.Enums;

namespace ReachIT.Domain.Models;

public class RecentExternalFileItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string DisplayName { get; set; } = string.Empty;
    public string SourcePathOrUrl { get; set; } = string.Empty;
    public ExternalResourceType ResourceType { get; set; } = ExternalResourceType.ExternalFile;
    public DateTime LastOpenedAtUtc { get; set; } = DateTime.UtcNow;
}
