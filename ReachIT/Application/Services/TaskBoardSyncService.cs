using System.IO;
using System.Text;
using ReachIT.Application.Contracts;
using ReachIT.Domain.Models;

namespace ReachIT.Application.Services;

public sealed class TaskBoardSyncService : ITaskBoardSyncService
{
    private const string BoardFileName = "ReachIT.tasks.md";
    private readonly IProjectService _projectService;
    private readonly ITaskService _taskService;

    public TaskBoardSyncService(IProjectService projectService, ITaskService taskService)
    {
        _projectService = projectService;
        _taskService = taskService;
    }

    public Task<string?> ExportCurrentProjectAsync(CancellationToken cancellationToken = default)
    {
        var project = _projectService.CurrentProject;
        return project is null
            ? Task.FromResult<string?>(null)
            : ExportAsync(project, cancellationToken);
    }

    public async Task<string?> ExportAsync(ProjectMeta project, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(project.ProjectDirectoryPath) ||
            !Directory.Exists(project.ProjectDirectoryPath))
        {
            return null;
        }

        var tasks = await _taskService.GetTasksAsync(cancellationToken).ConfigureAwait(false);
        var boardPath = Path.Combine(project.ProjectDirectoryPath, BoardFileName);
        var markdown = BuildMarkdown(project, tasks);

        await File.WriteAllTextAsync(boardPath, markdown, Encoding.UTF8, cancellationToken).ConfigureAwait(false);
        return boardPath;
    }

    private static string BuildMarkdown(ProjectMeta project, IReadOnlyList<TaskItem> tasks)
    {
        var rootPath = string.IsNullOrWhiteSpace(project.ProjectDirectoryPath)
            ? string.Empty
            : Path.GetFullPath(project.ProjectDirectoryPath);

        var builder = new StringBuilder();
        builder.AppendLine($"# Project: {Escape(project.ProjectName)}");
        builder.AppendLine();
        builder.AppendLine("> ReachIT task board. Status is represented by section: TODO, IN PROGRESS, DONE.");
        builder.AppendLine();
        AppendSection(builder, "TODO", tasks.Where(IsTodo), rootPath);
        AppendSection(builder, "IN PROGRESS", tasks.Where(IsInProgress), rootPath);
        AppendSection(builder, "DONE", tasks.Where(t => t.IsCompleted), rootPath);
        return builder.ToString();
    }

    private static void AppendSection(StringBuilder builder, string title, IEnumerable<TaskItem> tasks, string rootPath)
    {
        builder.AppendLine($"## {title}");
        builder.AppendLine();

        var rows = tasks
            .OrderBy(t => t.Priority)
            .ThenBy(t => t.DueDateUtc)
            .ThenBy(t => t.Title)
            .ToList();

        if (rows.Count == 0)
        {
            builder.AppendLine("_No tasks._");
            builder.AppendLine();
            return;
        }

        foreach (var task in rows)
        {
            var check = task.IsCompleted ? "x" : " ";
            builder.Append($"- [{check}] (id:{task.Id}) [p{Math.Max(1, task.Priority)}] {Escape(task.Title)}");

            if (!string.IsNullOrWhiteSpace(task.Description))
            {
                builder.Append($" - {EscapeSingleLine(task.Description)}");
            }

            builder.AppendLine();

            if (task.DueDateUtc.HasValue)
            {
                builder.AppendLine($"  - due: {task.DueDateUtc.Value.ToLocalTime():yyyy-MM-dd}");
            }

            if (!string.IsNullOrWhiteSpace(task.AttachedFilePath))
            {
                builder.AppendLine($"  - file: {EscapeSingleLine(ToProjectRelativePath(rootPath, task.AttachedFilePath))}");
            }
        }

        builder.AppendLine();
    }

    private static bool IsTodo(TaskItem task)
    {
        return !task.IsCompleted && !IsInProgress(task);
    }

    private static bool IsInProgress(TaskItem task)
    {
        return !task.IsCompleted &&
               (task.StartedAtUtc.HasValue ||
                task.Status.Contains("progress", StringComparison.OrdinalIgnoreCase) ||
                task.Status.Contains("doing", StringComparison.OrdinalIgnoreCase));
    }

    private static string ToProjectRelativePath(string rootPath, string path)
    {
        if (string.IsNullOrWhiteSpace(rootPath))
        {
            return path;
        }

        try
        {
            var fullPath = Path.GetFullPath(path);
            if (fullPath.StartsWith(rootPath.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            {
                return Path.GetRelativePath(rootPath, fullPath);
            }
        }
        catch
        {
            return path;
        }

        return path;
    }

    private static string Escape(string value)
    {
        return value.ReplaceLineEndings(" ").Trim();
    }

    private static string EscapeSingleLine(string value)
    {
        return Escape(value).Replace("|", "\\|");
    }
}
