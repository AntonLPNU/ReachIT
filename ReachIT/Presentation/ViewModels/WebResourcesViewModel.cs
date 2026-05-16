// Project browser workspace for web links, reading metadata, and focus hosts.
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using Microsoft.VisualBasic;
using ReachIT.Application.Contracts;
using ReachIT.Application.Security;
using ReachIT.Domain.Models;
using ReachIT.Presentation.Commands;

namespace ReachIT.Presentation.ViewModels;

public sealed class WebResourcesViewModel : ViewModelBase
{
    private readonly IProjectService _projectService;
    private readonly IExternalResourceService _externalResourceService;
    private readonly ITaskService _taskService;
    private readonly IActiveBrowserUrlService _activeBrowserUrlService;
    private WebResourceItemViewModel? _selectedResource;
    private string _statusText = "Open a project to manage web resources.";
    private string _title = string.Empty;
    private string _primaryUrl = string.Empty;
    private string _alternateUrlsText = string.Empty;
    private string _allowedFocusHostsText = string.Empty;
    private string _readingProgress = string.Empty;
    private string _lastReadMarker = string.Empty;
    private string _highlightsText = string.Empty;
    private string _notes = string.Empty;

    public WebResourcesViewModel(
        IProjectService projectService,
        IExternalResourceService externalResourceService,
        ITaskService taskService,
        IActiveBrowserUrlService activeBrowserUrlService)
    {
        _projectService = projectService;
        _externalResourceService = externalResourceService;
        _taskService = taskService;
        _activeBrowserUrlService = activeBrowserUrlService;

        RefreshCommand = new AsyncCommand(_ => LoadAsync());
        AddUrlCommand = new AsyncCommand(_ => AddUrlAsync());
        SaveCommand = new AsyncCommand(_ => SaveAsync());
        OpenCommand = new RelayCommand(_ => OpenSelectedUrl());
        CreateReadingTaskCommand = new AsyncCommand(_ => CreateReadingTaskAsync());
    }

    public ObservableCollection<WebResourceItemViewModel> Resources { get; } = [];

    public ObservableCollection<string> FocusHosts { get; } = [];

    public ICommand RefreshCommand { get; }

    public ICommand AddUrlCommand { get; }

    public ICommand SaveCommand { get; }

    public ICommand OpenCommand { get; }

    public ICommand CreateReadingTaskCommand { get; }

    public WebResourceItemViewModel? SelectedResource
    {
        get => _selectedResource;
        set
        {
            if (SetProperty(ref _selectedResource, value))
            {
                _ = LoadSelectedResourceAsync(value);
                OnPropertyChanged(nameof(HasSelection));
            }
        }
    }

    public bool HasSelection => SelectedResource is not null;

    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

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

    public string AlternateUrlsText
    {
        get => _alternateUrlsText;
        set => SetProperty(ref _alternateUrlsText, value);
    }

    public string AllowedFocusHostsText
    {
        get => _allowedFocusHostsText;
        set => SetProperty(ref _allowedFocusHostsText, value);
    }

    public string ReadingProgress
    {
        get => _readingProgress;
        set => SetProperty(ref _readingProgress, value);
    }

    public string LastReadMarker
    {
        get => _lastReadMarker;
        set => SetProperty(ref _lastReadMarker, value);
    }

    public string HighlightsText
    {
        get => _highlightsText;
        set => SetProperty(ref _highlightsText, value);
    }

    public string Notes
    {
        get => _notes;
        set => SetProperty(ref _notes, value);
    }

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        Resources.Clear();
        FocusHosts.Clear();
        ClearEditor();

        var project = _projectService.CurrentProject;
        if (project is null || string.IsNullOrWhiteSpace(project.ProjectDirectoryPath) || !Directory.Exists(project.ProjectDirectoryPath))
        {
            StatusText = "Open a project to manage web resources.";
            return;
        }

        var webResourcesDirectory = Path.Combine(project.ProjectDirectoryPath, "Web Resources");
        if (!Directory.Exists(webResourcesDirectory))
        {
            StatusText = "No web resources yet. Add a URL to create the Web Resources folder.";
            return;
        }

        var hosts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var filePath in Directory.EnumerateFiles(webResourcesDirectory, "*.reachit-link.json", SearchOption.TopDirectoryOnly))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var metadata = await ReadMetadataAsync(filePath, cancellationToken).ConfigureAwait(true);
            if (metadata is null)
            {
                continue;
            }

            try
            {
                if (!WebResourceSecurity.IsSafeWebUrl(metadata.PrimaryUrl))
                {
                    continue;
                }

                Resources.Add(WebResourceItemViewModel.FromMetadata(filePath, project.ProjectDirectoryPath, metadata));
                AddUrlHost(hosts, metadata.PrimaryUrl);
                foreach (var url in WebResourceSecurity.NormalizeAndValidateUrls(metadata.AlternateUrls))
                {
                    AddUrlHost(hosts, url);
                }

                foreach (var host in WebResourceSecurity.NormalizeAndValidateHosts(metadata.AllowedFocusHosts))
                {
                    AddHost(hosts, host);
                }
            }
            catch
            {
                continue;
            }
        }

        foreach (var host in hosts.Order(StringComparer.OrdinalIgnoreCase))
        {
            FocusHosts.Add(host);
        }

        SelectedResource = Resources.FirstOrDefault();
        StatusText = Resources.Count == 0
            ? "No valid web resource metadata files found."
            : $"{Resources.Count} web resource(s), {FocusHosts.Count} focus host(s).";
    }

    public async Task<bool> CaptureHighlightFromClipboardAsync(string color)
    {
        if (SelectedResource is null)
        {
            await LoadAsync().ConfigureAwait(true);
        }

        if (SelectedResource is null)
        {
            StatusText = "Add or select a web resource before saving highlights.";
            MessageBox.Show(StatusText, "ReachIT", MessageBoxButton.OK, MessageBoxImage.Information);
            return false;
        }

        string copiedText;
        try
        {
            copiedText = Clipboard.ContainsText() ? Clipboard.GetText().Trim() : string.Empty;
        }
        catch
        {
            copiedText = string.Empty;
        }

        if (string.IsNullOrWhiteSpace(copiedText))
        {
            StatusText = "Copy selected text first, then choose a marker color.";
            MessageBox.Show(StatusText, "ReachIT", MessageBoxButton.OK, MessageBoxImage.Information);
            return false;
        }

        var metadata = await ReadMetadataAsync(SelectedResource.FilePath).ConfigureAwait(true);
        if (metadata is null)
        {
            StatusText = "Could not read selected web resource.";
            return false;
        }

        color = NormalizeHighlightColor(color);
        var marker = $"[{color}] {DateTime.Now:yyyy-MM-dd HH:mm} - {copiedText}";
        metadata.Highlights.Add(marker);
        metadata.UpdatedAtUtc = DateTime.UtcNow;
        await WriteMetadataAsync(SelectedResource.FilePath, metadata).ConfigureAwait(true);
        await LoadSelectedResourceAsync(SelectedResource).ConfigureAwait(true);
        StatusText = $"Saved {color} highlight.";
        return true;
    }

    private async Task AddUrlAsync()
    {
        var project = _projectService.CurrentProject;
        if (project is null)
        {
            StatusText = "Open a project before adding web resources.";
            return;
        }

        var detectedUrl = _activeBrowserUrlService.TryGetActiveBrowserUrl();
        var url = Interaction.InputBox(
            string.IsNullOrWhiteSpace(detectedUrl)
                ? "Paste a project web page URL:"
                : "Confirm the active browser page URL:",
            "Add Web Resource",
            string.IsNullOrWhiteSpace(detectedUrl) ? "https://" : detectedUrl);
        if (string.IsNullOrWhiteSpace(url) || string.Equals(url.Trim(), "https://", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        try
        {
            await _externalResourceService.SaveAsLinkAsync(project.Id, url.Trim()).ConfigureAwait(true);
            await LoadAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            StatusText = $"Blocked unsafe web resource: {ex.Message}";
            MessageBox.Show(StatusText, "ReachIT Security", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async Task LoadSelectedResourceAsync(WebResourceItemViewModel? item)
    {
        if (item is null)
        {
            ClearEditor();
            return;
        }

        var metadata = await ReadMetadataAsync(item.FilePath).ConfigureAwait(true);
        if (metadata is null)
        {
            ClearEditor();
            StatusText = "Could not read selected web resource.";
            return;
        }

        Title = metadata.Title;
        PrimaryUrl = metadata.PrimaryUrl;
        AlternateUrlsText = ToLines(metadata.AlternateUrls);
        AllowedFocusHostsText = ToLines(metadata.AllowedFocusHosts);
        ReadingProgress = metadata.ReadingProgress;
        LastReadMarker = metadata.LastReadMarker;
        HighlightsText = ToLines(metadata.Highlights);
        Notes = metadata.Notes;
    }

    private async Task SaveAsync()
    {
        if (SelectedResource is null)
        {
            return;
        }

        var existing = await ReadMetadataAsync(SelectedResource.FilePath).ConfigureAwait(true);
        WebResourceLinkMetadata metadata;
        try
        {
            metadata = new WebResourceLinkMetadata
            {
                Title = Title.Trim(),
                PrimaryUrl = WebResourceSecurity.NormalizeAndValidateUrl(PrimaryUrl),
                AlternateUrls = WebResourceSecurity.NormalizeAndValidateUrls(FromLines(AlternateUrlsText)),
                AllowedFocusHosts = WebResourceSecurity.NormalizeAndValidateHosts(FromLines(AllowedFocusHostsText)),
                ReadingProgress = ReadingProgress.Trim(),
                LastReadMarker = LastReadMarker.Trim(),
                Highlights = FromLines(HighlightsText),
                Notes = Notes.Trim(),
                CreatedAtUtc = existing?.CreatedAtUtc ?? DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            StatusText = $"Blocked unsafe web resource metadata: {ex.Message}";
            MessageBox.Show(StatusText, "ReachIT Security", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        await WriteMetadataAsync(SelectedResource.FilePath, metadata).ConfigureAwait(true);
        SelectedResource.Title = metadata.Title;
        SelectedResource.PrimaryUrl = metadata.PrimaryUrl;
        SelectedResource.Host = TryGetHost(metadata.PrimaryUrl);
        SelectedResource.ReadingProgress = metadata.ReadingProgress;
        await ReloadFocusHostsAsync().ConfigureAwait(true);
        StatusText = "Web resource saved.";
    }

    private void OpenSelectedUrl()
    {
        if (string.IsNullOrWhiteSpace(PrimaryUrl))
        {
            return;
        }

        string safeUrl;
        try
        {
            safeUrl = WebResourceSecurity.NormalizeAndValidateUrl(PrimaryUrl);
        }
        catch (Exception ex)
        {
            StatusText = $"Blocked unsafe URL: {ex.Message}";
            MessageBox.Show(StatusText, "ReachIT Security", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = safeUrl,
            UseShellExecute = true
        });
    }

    private async Task CreateReadingTaskAsync()
    {
        if (SelectedResource is null)
        {
            return;
        }

        var taskTitle = string.IsNullOrWhiteSpace(Title) ? $"Read: {SelectedResource.Title}" : $"Read: {Title}";
        await _taskService.CreateAndAttachTaskToFileAsync(taskTitle, SelectedResource.FilePath).ConfigureAwait(true);
        StatusText = $"Created reading task for {SelectedResource.Title}.";
    }

    private async Task ReloadFocusHostsAsync()
    {
        var selected = SelectedResource;
        await LoadAsync().ConfigureAwait(true);
        if (selected is not null)
        {
            SelectedResource = Resources.FirstOrDefault(item => string.Equals(item.FilePath, selected.FilePath, StringComparison.OrdinalIgnoreCase))
                               ?? Resources.FirstOrDefault();
        }
    }

    private void ClearEditor()
    {
        Title = string.Empty;
        PrimaryUrl = string.Empty;
        AlternateUrlsText = string.Empty;
        AllowedFocusHostsText = string.Empty;
        ReadingProgress = string.Empty;
        LastReadMarker = string.Empty;
        HighlightsText = string.Empty;
        Notes = string.Empty;
        OnPropertyChanged(nameof(HasSelection));
    }

    private static async Task<WebResourceLinkMetadata?> ReadMetadataAsync(string filePath, CancellationToken cancellationToken = default)
    {
        try
        {
            var json = await File.ReadAllTextAsync(filePath, cancellationToken).ConfigureAwait(true);
            return JsonSerializer.Deserialize<WebResourceLinkMetadata>(
                json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch
        {
            return null;
        }
    }

    private static Task WriteMetadataAsync(string filePath, WebResourceLinkMetadata metadata)
    {
        var json = JsonSerializer.Serialize(metadata, new JsonSerializerOptions { WriteIndented = true });
        return File.WriteAllTextAsync(filePath, json);
    }

    private static string ToLines(IEnumerable<string> values)
    {
        return string.Join(Environment.NewLine, values.Where(value => !string.IsNullOrWhiteSpace(value)));
    }

    private static List<string> FromLines(string value)
    {
        return value
            .Split([Environment.NewLine, "\n"], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(line => line.Trim())
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static void AddUrlHost(ISet<string> hosts, string url)
    {
        if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            AddHost(hosts, uri.Host);
        }
    }

    private static void AddHost(ISet<string> hosts, string host)
    {
        host = host.Trim().TrimEnd('/').ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(host))
        {
            return;
        }

        if (Uri.TryCreate(host, UriKind.Absolute, out var uri))
        {
            host = uri.Host.ToLowerInvariant();
        }

        if (host.StartsWith("www.", StringComparison.OrdinalIgnoreCase))
        {
            host = host[4..];
        }

        hosts.Add(host);
    }

    private static string TryGetHost(string url)
    {
        return Uri.TryCreate(url, UriKind.Absolute, out var uri) ? uri.Host : string.Empty;
    }

    private static string NormalizeHighlightColor(string color)
    {
        return color.Trim().ToLowerInvariant() switch
        {
            "green" => "green",
            "pink" => "pink",
            "blue" => "blue",
            _ => "yellow"
        };
    }
}
