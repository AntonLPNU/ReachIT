// File workspace view model with metadata and safe preview logic.
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using System.Collections.ObjectModel;
using Microsoft.VisualBasic;
using ReachIT.Application.Contracts;
using ReachIT.Application.Security;
using ReachIT.Domain.Models;
using ReachIT.Presentation.Commands;

namespace ReachIT.Presentation.ViewModels;

public sealed class FileViewModel : ViewModelBase
{
    private readonly IProjectService _projectService;
    private readonly ITaskService _taskService;
    private readonly IFileInspectionService _fileInspectionService;
    private string _selectedNodeName = "Files";
    private string _selectedRelativePath = string.Empty;
    private string _selectedFullPath = string.Empty;
    private string _fileName = string.Empty;
    private string _fullPath = string.Empty;
    private string _relativePath = string.Empty;
    private string _extension = string.Empty;
    private string _fileSizeText = "-";
    private string _createdAtText = "-";
    private string _lastModifiedText = "-";
    private string _fileKind = "-";
    private string _readCapability = "-";
    private bool _exists;
    private string _textPreview = string.Empty;
    private string _imagePath = string.Empty;
    private bool _canPreviewAsText;
    private bool _canPreviewAsImage;
    private bool _canPreviewAsArchive;
    private bool _canPreviewAsAudio;
    private bool _canPreviewAsModel;
    private string _errorMessage = string.Empty;
    private string _attachedTasksSummary = "No attached tasks";
    private bool _hasAttachedTasks;
    private TaskItem? _selectedAttachedTask;
    private bool _isWebResource;
    private string _webTitle = string.Empty;
    private string _webPrimaryUrl = string.Empty;
    private string _webAlternateUrlsText = string.Empty;
    private string _webAllowedFocusHostsText = string.Empty;
    private string _webReadingProgress = string.Empty;
    private string _webLastReadMarker = string.Empty;
    private string _webHighlightsText = string.Empty;
    private string _webNotes = string.Empty;

    public FileViewModel(
        IProjectService projectService,
        ITaskService taskService,
        IFileInspectionService fileInspectionService)
    {
        _projectService = projectService;
        _taskService = taskService;
        _fileInspectionService = fileInspectionService;

        OpenFileCommand = new RelayCommand(_ => OpenFile());
        OpenWebResourceCommand = new RelayCommand(_ => OpenWebResource());
        SaveWebResourceCommand = new AsyncCommand(_ => SaveWebResourceAsync());
        RevealInExplorerCommand = new RelayCommand(_ => RevealInExplorer());
        CreateSnapshotCommand = new RelayCommand(_ =>
        {
            MessageBox.Show("TODO: Create snapshot for selected file.", "ReachIT", MessageBoxButton.OK, MessageBoxImage.Information);
        });
        AttachTaskCommand = new AsyncCommand(_ => AttachTaskAsync());
    }

    public string SelectedNodeName
    {
        get => _selectedNodeName;
        set => SetProperty(ref _selectedNodeName, value);
    }

    public string SelectedRelativePath
    {
        get => _selectedRelativePath;
        set
        {
            if (SetProperty(ref _selectedRelativePath, value))
            {
                RelativePath = value;
            }
        }
    }

    public string SelectedFullPath
    {
        get => _selectedFullPath;
        set
        {
            if (SetProperty(ref _selectedFullPath, value))
            {
                FullPath = value;
                _ = LoadSelectedFileAsync(value);
            }
        }
    }

    public string FileName
    {
        get => _fileName;
        set => SetProperty(ref _fileName, value);
    }

    public string FullPath
    {
        get => _fullPath;
        set => SetProperty(ref _fullPath, value);
    }

    public string RelativePath
    {
        get => _relativePath;
        set => SetProperty(ref _relativePath, value);
    }

    public string Extension
    {
        get => _extension;
        set => SetProperty(ref _extension, value);
    }

    public string FileSizeText
    {
        get => _fileSizeText;
        set => SetProperty(ref _fileSizeText, value);
    }

    public string CreatedAtText
    {
        get => _createdAtText;
        set => SetProperty(ref _createdAtText, value);
    }

    public string LastModifiedText
    {
        get => _lastModifiedText;
        set => SetProperty(ref _lastModifiedText, value);
    }

    public string FileKind
    {
        get => _fileKind;
        set => SetProperty(ref _fileKind, value);
    }

    public string ReadCapability
    {
        get => _readCapability;
        set => SetProperty(ref _readCapability, value);
    }

    public bool Exists
    {
        get => _exists;
        set => SetProperty(ref _exists, value);
    }

    public string TextPreview
    {
        get => _textPreview;
        set => SetProperty(ref _textPreview, value);
    }

    public string ImagePath
    {
        get => _imagePath;
        set => SetProperty(ref _imagePath, value);
    }

    public bool CanPreviewAsText
    {
        get => _canPreviewAsText;
        set => SetProperty(ref _canPreviewAsText, value);
    }

    public bool CanPreviewAsImage
    {
        get => _canPreviewAsImage;
        set => SetProperty(ref _canPreviewAsImage, value);
    }

    public bool CanPreviewAsArchive
    {
        get => _canPreviewAsArchive;
        set => SetProperty(ref _canPreviewAsArchive, value);
    }

    public bool CanPreviewAsAudio
    {
        get => _canPreviewAsAudio;
        set => SetProperty(ref _canPreviewAsAudio, value);
    }

    public bool CanPreviewAsModel
    {
        get => _canPreviewAsModel;
        set => SetProperty(ref _canPreviewAsModel, value);
    }

    public string ErrorMessage
    {
        get => _errorMessage;
        set => SetProperty(ref _errorMessage, value);
    }

    public string AttachedTasksSummary
    {
        get => _attachedTasksSummary;
        set => SetProperty(ref _attachedTasksSummary, value);
    }

    public bool HasAttachedTasks
    {
        get => _hasAttachedTasks;
        set => SetProperty(ref _hasAttachedTasks, value);
    }

    public TaskItem? SelectedAttachedTask
    {
        get => _selectedAttachedTask;
        set => SetProperty(ref _selectedAttachedTask, value);
    }

    public ObservableCollection<TaskItem> AttachedTasks { get; } = [];

    public ICommand OpenFileCommand { get; }
    public ICommand OpenWebResourceCommand { get; }
    public ICommand SaveWebResourceCommand { get; }
    public ICommand RevealInExplorerCommand { get; }
    public ICommand CreateSnapshotCommand { get; }
    public ICommand AttachTaskCommand { get; }

    public bool IsWebResource
    {
        get => _isWebResource;
        set
        {
            if (SetProperty(ref _isWebResource, value))
            {
                OnPropertyChanged(nameof(IsRegularFile));
            }
        }
    }

    public bool IsRegularFile => !IsWebResource;

    public string WebTitle
    {
        get => _webTitle;
        set => SetProperty(ref _webTitle, value);
    }

    public string WebPrimaryUrl
    {
        get => _webPrimaryUrl;
        set => SetProperty(ref _webPrimaryUrl, value);
    }

    public string WebAlternateUrlsText
    {
        get => _webAlternateUrlsText;
        set => SetProperty(ref _webAlternateUrlsText, value);
    }

    public string WebAllowedFocusHostsText
    {
        get => _webAllowedFocusHostsText;
        set => SetProperty(ref _webAllowedFocusHostsText, value);
    }

    public string WebReadingProgress
    {
        get => _webReadingProgress;
        set => SetProperty(ref _webReadingProgress, value);
    }

    public string WebLastReadMarker
    {
        get => _webLastReadMarker;
        set => SetProperty(ref _webLastReadMarker, value);
    }

    public string WebHighlightsText
    {
        get => _webHighlightsText;
        set => SetProperty(ref _webHighlightsText, value);
    }

    public string WebNotes
    {
        get => _webNotes;
        set => SetProperty(ref _webNotes, value);
    }

    private async Task LoadSelectedFileAsync(string selectedPath)
    {
        ResetPreviewState();

        if (string.IsNullOrWhiteSpace(selectedPath))
        {
            Exists = false;
            ErrorMessage = "No file selected.";
            return;
        }

        var fullPath = Path.GetFullPath(selectedPath);
        FullPath = fullPath;
        FileName = Path.GetFileName(fullPath);
        SelectedNodeName = FileName;

        var currentProject = _projectService.CurrentProject;
        if (currentProject is not null && !string.IsNullOrWhiteSpace(currentProject.ProjectDirectoryPath))
        {
            var rootPath = Path.GetFullPath(currentProject.ProjectDirectoryPath);
            if (IsInside(rootPath, fullPath))
            {
                RelativePath = Path.GetRelativePath(rootPath, fullPath);
                SelectedRelativePath = RelativePath;
            }
        }

        if (Directory.Exists(fullPath))
        {
            Exists = true;
            ErrorMessage = "Selected item is a folder. File preview is available only for files.";
            return;
        }

        if (!File.Exists(fullPath))
        {
            Exists = false;
            ErrorMessage = "Selected file does not exist.";
            return;
        }

        Exists = true;

        await LoadAttachedTasksAsync(fullPath).ConfigureAwait(true);

        var fileInfo = new FileInfo(fullPath);
        Extension = fileInfo.Extension;
        FileSizeText = FormatSize(fileInfo.Length);
        CreatedAtText = fileInfo.CreationTime.ToString("yyyy-MM-dd HH:mm:ss");
        LastModifiedText = fileInfo.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss");

        if (IsWebResourceLinkFile(fullPath))
        {
            await LoadWebResourceAsync(fullPath).ConfigureAwait(true);
            return;
        }

        var inspection = await _fileInspectionService.InspectAsync(fullPath).ConfigureAwait(true);
        FileKind = inspection.FileKind;
        ReadCapability = inspection.ReadCapability;
        TextPreview = inspection.PreviewText;
        CanPreviewAsText = inspection.HasTextPreview;
        CanPreviewAsImage = inspection.HasImagePreview;
        CanPreviewAsArchive = inspection.HasArchivePreview;
        CanPreviewAsAudio = inspection.HasAudioPreview;
        CanPreviewAsModel = inspection.HasModelPreview;
        ImagePath = inspection.HasImagePreview ? fullPath : string.Empty;
        ErrorMessage = inspection.Message;
    }

    private void OpenFile()
    {
        if (!Exists || string.IsNullOrWhiteSpace(FullPath))
        {
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = FullPath,
            UseShellExecute = true
        });
    }

    private void OpenWebResource()
    {
        var target = WebPrimaryUrl;
        if (string.IsNullOrWhiteSpace(target))
        {
            return;
        }

        try
        {
            target = WebResourceSecurity.NormalizeAndValidateUrl(target);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Blocked unsafe URL: {ex.Message}";
            MessageBox.Show(ErrorMessage, "ReachIT Security", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = target,
            UseShellExecute = true
        });
    }

    private void RevealInExplorer()
    {
        if (!Exists || string.IsNullOrWhiteSpace(FullPath))
        {
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = "explorer.exe",
            Arguments = $"/select,\"{FullPath}\"",
            UseShellExecute = true
        });
    }

    private void ResetPreviewState()
    {
        Exists = false;
        Extension = string.Empty;
        FileSizeText = "-";
        CreatedAtText = "-";
        LastModifiedText = "-";
        FileKind = "-";
        ReadCapability = "-";
        TextPreview = string.Empty;
        ImagePath = string.Empty;
        CanPreviewAsText = false;
        CanPreviewAsImage = false;
        ErrorMessage = string.Empty;
        AttachedTasks.Clear();
        AttachedTasksSummary = "No attached tasks";
        HasAttachedTasks = false;
        SelectedAttachedTask = null;
        ResetWebResourceState();
    }

    private async Task LoadWebResourceAsync(string fullPath)
    {
        IsWebResource = true;
        FileKind = "ReachIT web resource";
        ReadCapability = "Editable metadata";
        CanPreviewAsText = false;
        CanPreviewAsImage = false;
        CanPreviewAsArchive = false;
        CanPreviewAsAudio = false;
        CanPreviewAsModel = false;

        try
        {
            var json = await File.ReadAllTextAsync(fullPath).ConfigureAwait(true);
            var metadata = JsonSerializer.Deserialize<WebResourceLinkMetadata>(
                json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new WebResourceLinkMetadata();

            WebTitle = string.IsNullOrWhiteSpace(metadata.Title)
                ? Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(fullPath))
                : metadata.Title;
            WebPrimaryUrl = metadata.PrimaryUrl;
            WebAlternateUrlsText = ToLines(metadata.AlternateUrls);
            WebAllowedFocusHostsText = ToLines(metadata.AllowedFocusHosts);
            WebReadingProgress = metadata.ReadingProgress;
            WebLastReadMarker = metadata.LastReadMarker;
            WebHighlightsText = ToLines(metadata.Highlights);
            WebNotes = metadata.Notes;
            FileName = WebTitle;
            ErrorMessage = string.Empty;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Could not read web resource metadata: {ex.Message}";
        }
    }

    private async Task SaveWebResourceAsync()
    {
        if (!IsWebResource || !Exists || string.IsNullOrWhiteSpace(FullPath))
        {
            return;
        }

        WebResourceLinkMetadata metadata;
        try
        {
            metadata = new WebResourceLinkMetadata
            {
                Title = WebTitle.Trim(),
                PrimaryUrl = WebResourceSecurity.NormalizeAndValidateUrl(WebPrimaryUrl),
                AlternateUrls = WebResourceSecurity.NormalizeAndValidateUrls(FromLines(WebAlternateUrlsText)),
                AllowedFocusHosts = WebResourceSecurity.NormalizeAndValidateHosts(FromLines(WebAllowedFocusHostsText)),
                ReadingProgress = WebReadingProgress.Trim(),
                LastReadMarker = WebLastReadMarker.Trim(),
                Highlights = FromLines(WebHighlightsText),
                Notes = WebNotes.Trim(),
                UpdatedAtUtc = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Blocked unsafe web resource metadata: {ex.Message}";
            MessageBox.Show(ErrorMessage, "ReachIT Security", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (File.Exists(FullPath))
        {
            try
            {
                var existingJson = await File.ReadAllTextAsync(FullPath).ConfigureAwait(true);
                var existing = JsonSerializer.Deserialize<WebResourceLinkMetadata>(
                    existingJson,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (existing is not null)
                {
                    metadata.CreatedAtUtc = existing.CreatedAtUtc;
                }
            }
            catch
            {
                // Keep saving available even if the old sidecar is malformed.
            }
        }

        var json = JsonSerializer.Serialize(metadata, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(FullPath, json).ConfigureAwait(true);
        ErrorMessage = "Web resource metadata saved.";
    }

    private void ResetWebResourceState()
    {
        IsWebResource = false;
        WebTitle = string.Empty;
        WebPrimaryUrl = string.Empty;
        WebAlternateUrlsText = string.Empty;
        WebAllowedFocusHostsText = string.Empty;
        WebReadingProgress = string.Empty;
        WebLastReadMarker = string.Empty;
        WebHighlightsText = string.Empty;
        WebNotes = string.Empty;
    }

    private async Task AttachTaskAsync()
    {
        if (!Exists || string.IsNullOrWhiteSpace(FullPath) || Directory.Exists(FullPath))
        {
            return;
        }

        var attachMode = Interaction.InputBox(
            "Type task title to create and attach a new task. Leave empty to attach existing by number from the list.",
            "Attach Task",
            string.Empty);

        if (!string.IsNullOrWhiteSpace(attachMode))
        {
            await _taskService.CreateAndAttachTaskToFileAsync(attachMode, FullPath).ConfigureAwait(true);
            await LoadAttachedTasksAsync(FullPath).ConfigureAwait(true);
            return;
        }

        var allTasks = await _taskService.GetTasksAsync().ConfigureAwait(true);
        if (allTasks.Count == 0)
        {
            MessageBox.Show("There are no tasks yet. Enter a title to create one.", "ReachIT", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var options = string.Join(Environment.NewLine, allTasks.Select((task, index) => $"{index + 1}. {task.Title}"));
        var selectedNumberText = Interaction.InputBox(
            $"Choose task number to attach:{Environment.NewLine}{options}",
            "Attach Existing Task",
            "1");

        if (!int.TryParse(selectedNumberText, out var selectedNumber)
            || selectedNumber < 1
            || selectedNumber > allTasks.Count)
        {
            return;
        }

        var selectedTask = allTasks[selectedNumber - 1];
        await _taskService.AttachTaskToFileAsync(selectedTask.Id, FullPath).ConfigureAwait(true);
        await LoadAttachedTasksAsync(FullPath).ConfigureAwait(true);
    }

    private async Task LoadAttachedTasksAsync(string fullPath)
    {
        var attached = await _taskService.GetTasksByFilePathAsync(fullPath).ConfigureAwait(true);
        AttachedTasks.Clear();
        foreach (var task in attached)
        {
            AttachedTasks.Add(task);
        }

        SelectedAttachedTask = AttachedTasks.FirstOrDefault();
        HasAttachedTasks = AttachedTasks.Count > 0;
        AttachedTasksSummary = AttachedTasks.Count == 0
            ? "No attached tasks"
            : string.Join(", ", AttachedTasks.Select(x => x.Title));
    }

    private static string FormatSize(long bytes)
    {
        if (bytes < 1024)
        {
            return $"{bytes} B";
        }

        var kb = bytes / 1024d;
        if (kb < 1024)
        {
            return $"{kb:F2} KB";
        }

        var mb = kb / 1024d;
        if (mb < 1024)
        {
            return $"{mb:F2} MB";
        }

        var gb = mb / 1024d;
        return $"{gb:F2} GB";
    }

    private static bool IsInside(string rootPath, string targetPath)
    {
        var normalizedRoot = rootPath.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var normalizedTarget = Path.GetFullPath(targetPath);
        return normalizedTarget.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase)
               || string.Equals(rootPath.TrimEnd(Path.DirectorySeparatorChar), normalizedTarget.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsWebResourceLinkFile(string fullPath)
    {
        var fileName = Path.GetFileName(fullPath);
        return fileName.EndsWith(".reachit-link.json", StringComparison.OrdinalIgnoreCase);
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
}
