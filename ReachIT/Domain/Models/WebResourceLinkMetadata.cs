namespace ReachIT.Domain.Models;

public sealed class WebResourceLinkMetadata
{
    public string SchemaVersion { get; set; } = "reachit.web-resource.v1";
    public string Title { get; set; } = string.Empty;
    public string PrimaryUrl { get; set; } = string.Empty;
    public List<string> AlternateUrls { get; set; } = [];
    public List<string> AllowedFocusHosts { get; set; } = [];
    public string ReadingProgress { get; set; } = string.Empty;
    public string LastReadMarker { get; set; } = string.Empty;
    public List<string> Highlights { get; set; } = [];
    public string Notes { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
