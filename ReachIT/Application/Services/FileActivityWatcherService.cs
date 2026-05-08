using System.Collections.Concurrent;
using System.IO;
using System.Text.Json;
using System.Timers;
using ReachIT.Application.Contracts;
using ReachIT.Domain.Enums;
using ReachIT.Domain.Models;

namespace ReachIT.Application.Services;

public sealed class FileActivityWatcherService : IFileActivityWatcherService
{
    private readonly ConcurrentDictionary<string, PendingFileEvent> _pending = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, FileTextStats> _lastTextStats = new(StringComparer.OrdinalIgnoreCase);
    private readonly System.Timers.Timer _debounceTimer;
    private FileSystemWatcher? _watcher;
    private ProjectMeta? _project;
    private HashSet<string> _ignoredFolders = new(StringComparer.OrdinalIgnoreCase);
    private bool _trackTextStatistics;

    public event EventHandler<ActivityEvent>? ActivityDetected;

    public FileActivityWatcherService()
    {
        _debounceTimer = new System.Timers.Timer(1200);
        _debounceTimer.Elapsed += FlushPending;
        _debounceTimer.AutoReset = true;
    }

    public void Start(ProjectMeta project, IReadOnlyCollection<string> ignoredFolders, bool trackTextStatistics)
    {
        Stop();

        if (string.IsNullOrWhiteSpace(project.ProjectDirectoryPath) || !Directory.Exists(project.ProjectDirectoryPath))
        {
            return;
        }

        _project = project;
        _ignoredFolders = ignoredFolders.Count == 0
            ? new HashSet<string>(DefaultIgnoredFolders, StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(ignoredFolders.Concat(DefaultIgnoredFolders), StringComparer.OrdinalIgnoreCase);
        _trackTextStatistics = trackTextStatistics;

        _watcher = new FileSystemWatcher(project.ProjectDirectoryPath)
        {
            IncludeSubdirectories = true,
            EnableRaisingEvents = true,
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite | NotifyFilters.Size
        };

        _watcher.Created += OnCreated;
        _watcher.Changed += OnChanged;
        _watcher.Deleted += OnDeleted;
        _watcher.Renamed += OnRenamed;
        _debounceTimer.Start();
    }

    public void Stop()
    {
        _debounceTimer.Stop();
        if (_watcher is not null)
        {
            _watcher.EnableRaisingEvents = false;
            _watcher.Created -= OnCreated;
            _watcher.Changed -= OnChanged;
            _watcher.Deleted -= OnDeleted;
            _watcher.Renamed -= OnRenamed;
            _watcher.Dispose();
            _watcher = null;
        }

        _pending.Clear();
        _project = null;
    }

    private void OnCreated(object sender, FileSystemEventArgs e) => Queue(e.FullPath, ActivityEventType.FileCreated, null);
    private void OnChanged(object sender, FileSystemEventArgs e) => Queue(e.FullPath, ActivityEventType.FileChanged, null);
    private void OnDeleted(object sender, FileSystemEventArgs e) => Queue(e.FullPath, ActivityEventType.FileDeleted, null);

    private void OnRenamed(object sender, RenamedEventArgs e)
    {
        Queue(e.FullPath, ActivityEventType.FileRenamed, e.OldFullPath);
    }

    private void Queue(string path, ActivityEventType type, string? oldPath)
    {
        if (_project is null || ShouldIgnore(path))
        {
            return;
        }

        var key = $"{type}:{path}";
        _pending[key] = new PendingFileEvent(path, oldPath, type, DateTime.UtcNow);
    }

    private void FlushPending(object? sender, ElapsedEventArgs e)
    {
        if (_project is null)
        {
            return;
        }

        var cutoff = DateTime.UtcNow.AddMilliseconds(-900);
        foreach (var pair in _pending.ToArray())
        {
            if (pair.Value.QueuedAt > cutoff || !_pending.TryRemove(pair.Key, out var pending))
            {
                continue;
            }

            foreach (var activityEvent in CreateEvents(_project, pending))
            {
                ActivityDetected?.Invoke(this, activityEvent);
            }
        }
    }

    private IEnumerable<ActivityEvent> CreateEvents(ProjectMeta project, PendingFileEvent pending)
    {
        var isDirectory = Directory.Exists(pending.Path);
        var metadata = new Dictionary<string, object?>
        {
            ["oldPath"] = pending.OldPath,
            ["extension"] = Path.GetExtension(pending.Path),
            ["name"] = Path.GetFileName(pending.Path)
        };

        yield return new ActivityEvent
        {
            ProjectId = project.Id,
            Timestamp = DateTime.UtcNow,
            EventType = isDirectory ? ActivityEventType.FolderChanged : pending.EventType,
            FilePath = isDirectory ? null : pending.Path,
            FolderPath = isDirectory ? pending.Path : Path.GetDirectoryName(pending.Path),
            MetadataJson = JsonSerializer.Serialize(metadata)
        };

        if (_trackTextStatistics && pending.EventType is ActivityEventType.FileChanged or ActivityEventType.FileCreated && IsTextFile(pending.Path) && File.Exists(pending.Path))
        {
            var current = TryReadTextStats(pending.Path);
            if (current is null)
            {
                yield break;
            }

            _lastTextStats.TryGetValue(pending.Path, out var previous);
            _lastTextStats[pending.Path] = current;

            var deltaChars = previous is null ? current.Characters : current.Characters - previous.Characters;
            var deltaWords = previous is null ? current.Words : current.Words - previous.Words;
            var deltaLines = previous is null ? current.Lines : current.Lines - previous.Lines;

            yield return new ActivityEvent
            {
                ProjectId = project.Id,
                Timestamp = DateTime.UtcNow,
                EventType = ActivityEventType.TextChanged,
                FilePath = pending.Path,
                FolderPath = Path.GetDirectoryName(pending.Path),
                Value = deltaChars,
                MetadataJson = JsonSerializer.Serialize(new
                {
                    characters = current.Characters,
                    words = current.Words,
                    lines = current.Lines,
                    deltaCharacters = deltaChars,
                    deltaWords,
                    deltaLines
                })
            };
        }
    }

    private bool ShouldIgnore(string path)
    {
        if (_project is null)
        {
            return true;
        }

        var relative = Path.GetRelativePath(_project.ProjectDirectoryPath, path);
        var normalizedRelative = relative.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
        if (_ignoredFolders.Contains(normalizedRelative) || _ignoredFolders.Contains(Path.GetFileName(path)))
        {
            return true;
        }

        var parts = relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return parts.Any(part => _ignoredFolders.Contains(part));
    }

    private static bool IsTextFile(string path)
    {
        var extension = Path.GetExtension(path).ToLowerInvariant();
        return extension is ".txt" or ".md" or ".cs" or ".xaml" or ".json" or ".xml" or ".html" or ".css" or ".js" or ".ts" or ".py" or ".yml" or ".yaml" or ".csv" or ".rtf";
    }

    private static FileTextStats? TryReadTextStats(string path)
    {
        try
        {
            var text = File.ReadAllText(path);
            var words = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;
            var lines = text.Length == 0 ? 0 : text.Count(c => c == '\n') + 1;
            return new FileTextStats(text.Length, words, lines);
        }
        catch
        {
            return null;
        }
    }

    public void Dispose()
    {
        Stop();
        _debounceTimer.Dispose();
    }

    private static readonly string[] DefaultIgnoredFolders = ["bin", "obj", ".git", ".vs", "node_modules", "packages", "build", "dist"];
    private sealed record PendingFileEvent(string Path, string? OldPath, ActivityEventType EventType, DateTime QueuedAt);
    private sealed record FileTextStats(int Characters, int Words, int Lines);
}
