// File workspace view model with metadata and safe preview logic.
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Collections.ObjectModel;
using Microsoft.VisualBasic;
using ReachIT.Application.Contracts;
using ReachIT.Domain.Models;
using ReachIT.Presentation.Commands;

namespace ReachIT.Presentation.ViewModels;

public sealed class FileViewModel : ViewModelBase
{
    private const int MaxTextPreviewBytes = 200 * 1024;

    private readonly IProjectService _projectService;
    private readonly ITaskService _taskService;
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
    private bool _exists;
    private string _textPreview = string.Empty;
    private string _imagePath = string.Empty;
    private bool _canPreviewAsText;
    private bool _canPreviewAsImage;
    private string _errorMessage = string.Empty;
    private string _attachedTasksSummary = "No attached tasks";
    private bool _hasAttachedTasks;
    private TaskItem? _selectedAttachedTask;

    public FileViewModel(IProjectService projectService, ITaskService taskService)
    {
        _projectService = projectService;
        _taskService = taskService;

        OpenFileCommand = new RelayCommand(_ => OpenFile());
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
    public ICommand RevealInExplorerCommand { get; }
    public ICommand CreateSnapshotCommand { get; }
    public ICommand AttachTaskCommand { get; }

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

        if (IsTextPreviewExtension(fileInfo.Extension))
        {
            CanPreviewAsText = true;
            TextPreview = await ReadTextPreviewAsync(fullPath).ConfigureAwait(true);
            return;
        }

        if (IsImagePreviewExtension(fileInfo.Extension))
        {
            CanPreviewAsImage = true;
            ImagePath = fullPath;
            return;
        }

        ErrorMessage = "Preview is not available for this file type.";
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
        TextPreview = string.Empty;
        ImagePath = string.Empty;
        CanPreviewAsText = false;
        CanPreviewAsImage = false;
        ErrorMessage = string.Empty;
        AttachedTasks.Clear();
        AttachedTasksSummary = "No attached tasks";
        HasAttachedTasks = false;
        SelectedAttachedTask = null;
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

    private static async Task<string> ReadTextPreviewAsync(string fullPath)
    {
        await using var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        var bytesToRead = (int)Math.Min(stream.Length, MaxTextPreviewBytes);
        var buffer = new byte[bytesToRead];
        var read = await stream.ReadAsync(buffer.AsMemory(0, bytesToRead)).ConfigureAwait(false);
        var preview = System.Text.Encoding.UTF8.GetString(buffer, 0, read);

        if (stream.Length > MaxTextPreviewBytes)
        {
            preview += Environment.NewLine + Environment.NewLine + "--- Preview truncated at 200 KB ---";
        }

        return preview;
    }

    private static bool IsTextPreviewExtension(string extension)
    {
        return extension.Equals(".txt", StringComparison.OrdinalIgnoreCase)
               || extension.Equals(".md", StringComparison.OrdinalIgnoreCase)
               || extension.Equals(".json", StringComparison.OrdinalIgnoreCase)
               || extension.Equals(".rit", StringComparison.OrdinalIgnoreCase)
               || extension.Equals(".cs", StringComparison.OrdinalIgnoreCase)
               || extension.Equals(".xaml", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsImagePreviewExtension(string extension)
    {
        return extension.Equals(".png", StringComparison.OrdinalIgnoreCase)
               || extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase)
               || extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase);
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
}
