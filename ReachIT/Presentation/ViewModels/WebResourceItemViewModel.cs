// Represents one project web resource sidecar in the browser workspace.
using System.IO;
using ReachIT.Domain.Models;

namespace ReachIT.Presentation.ViewModels;

public sealed class WebResourceItemViewModel : ViewModelBase
{
    private string _title = string.Empty;
    private string _primaryUrl = string.Empty;
    private string _host = string.Empty;
    private string _readingProgress = string.Empty;

    public string FilePath { get; init; } = string.Empty;

    public string RelativePath { get; init; } = string.Empty;

    public string Title
    {
        get => _title;
        set => SetProperty(ref _title, value);
    }

    public string PrimaryUrl
    {
        get => _primaryUrl;
        set => SetProperty(ref _primaryUrl, value);
    }

    public string Host
    {
        get => _host;
        set => SetProperty(ref _host, value);
    }

    public string ReadingProgress
    {
        get => _readingProgress;
        set => SetProperty(ref _readingProgress, value);
    }

    public static WebResourceItemViewModel FromMetadata(string filePath, string projectDirectory, WebResourceLinkMetadata metadata)
    {
        return new WebResourceItemViewModel
        {
            FilePath = filePath,
            RelativePath = Path.GetRelativePath(projectDirectory, filePath),
            Title = string.IsNullOrWhiteSpace(metadata.Title) ? Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(filePath)) : metadata.Title,
            PrimaryUrl = metadata.PrimaryUrl,
            Host = TryGetHost(metadata.PrimaryUrl),
            ReadingProgress = metadata.ReadingProgress
        };
    }

    private static string TryGetHost(string url)
    {
        return Uri.TryCreate(url, UriKind.Absolute, out var uri) ? uri.Host : string.Empty;
    }
}
