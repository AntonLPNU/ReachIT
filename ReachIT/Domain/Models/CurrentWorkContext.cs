namespace ReachIT.Domain.Models;

public sealed class CurrentWorkContext
{
    public Guid ProjectId { get; set; }
    public Guid? LikelyWorkItemId { get; set; }
    public double Confidence { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string ActiveApp { get; set; } = string.Empty;
    public string? ActiveFile { get; set; }
    public List<string> RecentFiles { get; set; } = [];
    public bool IsDistracted { get; set; }
}
