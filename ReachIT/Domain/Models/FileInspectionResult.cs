namespace ReachIT.Domain.Models;

public sealed class FileInspectionResult
{
    public string FileKind { get; init; } = "Unknown";
    public string ReadCapability { get; init; } = "Open externally";
    public string PreviewText { get; init; } = string.Empty;
    public bool HasTextPreview { get; init; }
    public bool HasImagePreview { get; init; }
    public string Message { get; init; } = string.Empty;
}
