using System.IO;
using System.Net;
using System.Text;
using ReachIT.Application.Contracts;
using ReachIT.Domain.Models;

namespace ReachIT.Application.Services;

public sealed class ProjectReportService : IProjectReportService
{
    private readonly ITaskService _taskService;

    public ProjectReportService(ITaskService taskService)
    {
        _taskService = taskService;
    }

    public async Task<string?> ExportHtmlReportAsync(ProjectMeta project, string outputPath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            return null;
        }

        var tasks = (await _taskService.GetTasksAsync(cancellationToken).ConfigureAwait(false))
            .OrderBy(t => t.IsCompleted)
            .ThenBy(t => t.Priority <= 0 ? int.MaxValue : t.Priority)
            .ThenBy(t => t.DueDateUtc ?? DateTime.MaxValue)
            .ThenBy(t => t.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var linksByTask = new Dictionary<Guid, IReadOnlyList<TaskFileLink>>();
        foreach (var task in tasks)
        {
            linksByTask[task.Id] = await _taskService.GetTaskFileLinksAsync(task.Id, includeDescendants: false, cancellationToken)
                .ConfigureAwait(false);
        }

        var html = BuildHtml(project, tasks, linksByTask);
        var directory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await File.WriteAllTextAsync(outputPath, html, Encoding.UTF8, cancellationToken).ConfigureAwait(false);
        return outputPath;
    }

    private static string BuildHtml(ProjectMeta project, IReadOnlyList<TaskItem> tasks, IReadOnlyDictionary<Guid, IReadOnlyList<TaskFileLink>> linksByTask)
    {
        var now = DateTime.Now;
        var completed = tasks.Count(t => t.IsCompleted);
        var active = tasks.Count - completed;
        var overdue = tasks.Count(t => !t.IsCompleted && t.DueDateUtc.HasValue && t.DueDateUtc.Value.ToLocalTime() < now);
        var linkedFiles = linksByTask.Values
            .SelectMany(x => x)
            .Select(x => x.FilePath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();

        var builder = new StringBuilder();
        builder.AppendLine("<!doctype html>");
        builder.AppendLine("<html lang=\"uk\">");
        builder.AppendLine("<head>");
        builder.AppendLine("<meta charset=\"utf-8\">");
        builder.AppendLine($"<title>{H(project.ProjectName)} - project report</title>");
        builder.AppendLine("<style>");
        builder.AppendLine("""
            @page {
              size: A4;
              margin: 18mm 16mm 18mm 16mm;
              @bottom-center {
                content: "Page " counter(page) " / " counter(pages);
                color: #64748b;
                font-size: 10pt;
              }
            }
            * { box-sizing: border-box; }
            body { margin: 0; color: #172033; font: 11pt/1.45 "Segoe UI", Arial, sans-serif; background: #fff; }
            h1, h2, h3 { color: #0f3f63; margin: 0 0 10px; }
            h1 { font-size: 30pt; line-height: 1.1; }
            h2 { font-size: 18pt; border-bottom: 2px solid #dbeafe; padding-bottom: 6px; margin-top: 20px; }
            h3 { font-size: 13pt; margin-top: 16px; }
            a { color: #0f6ea8; text-decoration: none; }
            table { width: 100%; border-collapse: collapse; margin: 10px 0 18px; page-break-inside: avoid; }
            th, td { border: 1px solid #d8e2f0; padding: 7px 8px; text-align: left; vertical-align: top; }
            th { background: #eff6ff; color: #17324d; }
            .cover { min-height: 252mm; display: flex; flex-direction: column; justify-content: space-between; page-break-after: always; }
            .brand { letter-spacing: 0.08em; color: #0ea5e9; font-weight: 700; text-transform: uppercase; }
            .cover-title { margin-top: 52mm; }
            .subtitle { font-size: 14pt; color: #475569; max-width: 150mm; }
            .cover-footer { display: grid; grid-template-columns: 1fr 1fr; gap: 14px; color: #475569; border-top: 1px solid #d8e2f0; padding-top: 12px; }
            .page { page-break-after: always; }
            .toc ol { margin: 0; padding-left: 22px; }
            .toc li { margin: 7px 0; }
            .metrics { display: grid; grid-template-columns: repeat(4, 1fr); gap: 10px; margin: 14px 0 18px; }
            .metric { border: 1px solid #d8e2f0; border-radius: 8px; padding: 10px; background: #f8fbff; }
            .metric strong { display: block; font-size: 18pt; color: #0f3f63; }
            .status { display: inline-block; border-radius: 999px; padding: 2px 8px; font-size: 9pt; font-weight: 700; }
            .done { background: #dcfce7; color: #166534; }
            .active { background: #e0f2fe; color: #075985; }
            .overdue { background: #fee2e2; color: #991b1b; }
            .muted { color: #64748b; }
            .task { page-break-inside: avoid; border: 1px solid #d8e2f0; border-radius: 10px; padding: 12px; margin: 12px 0; }
            .file-list { margin: 6px 0 0 18px; padding: 0; }
            .file-list li { margin: 3px 0; overflow-wrap: anywhere; }
            .small { font-size: 9.5pt; }
            """);
        builder.AppendLine("</style>");
        builder.AppendLine("</head>");
        builder.AppendLine("<body>");

        builder.AppendLine("<section class=\"cover\">");
        builder.AppendLine("<div>");
        builder.AppendLine("<div class=\"brand\">ReachIT project report</div>");
        builder.AppendLine("<div class=\"cover-title\">");
        builder.AppendLine($"<h1>{H(project.ProjectName)}</h1>");
        builder.AppendLine("<p class=\"subtitle\">Звіт по роботі проєкту: призначення програми, стан задач, виконані та невиконані роботи, а також файли, пов'язані із задачами.</p>");
        builder.AppendLine("</div>");
        builder.AppendLine("</div>");
        builder.AppendLine("<div class=\"cover-footer\">");
        builder.AppendLine($"<div><strong>Дата звіту</strong><br>{now:yyyy-MM-dd HH:mm}</div>");
        builder.AppendLine($"<div><strong>Папка проєкту</strong><br>{H(project.ProjectDirectoryPath)}</div>");
        builder.AppendLine("</div>");
        builder.AppendLine("</section>");

        builder.AppendLine("<section class=\"page toc\">");
        builder.AppendLine("<h2>Зміст</h2>");
        builder.AppendLine("<ol>");
        builder.AppendLine("<li><a href=\"#about\">Що це за програма</a></li>");
        builder.AppendLine("<li><a href=\"#summary\">Підсумок стану проєкту</a></li>");
        builder.AppendLine("<li><a href=\"#tasks\">Задачі та статус виконання</a></li>");
        builder.AppendLine("<li><a href=\"#files\">Пов'язані файли</a></li>");
        builder.AppendLine("</ol>");
        builder.AppendLine("</section>");

        builder.AppendLine("<section id=\"about\">");
        builder.AppendLine("<h2>1. Що це за програма</h2>");
        builder.AppendLine("<p>ReachIT - робочий простір для локального ведення проєкту: задачі, файли, фокус-режим, активність, Git-стан і звітність зібрані в одному місці. Основна ідея - пов'язувати задачі з реальними файлами, щоб прогрес був підтверджений результатом, а не лише галочкою.</p>");
        builder.AppendLine("</section>");

        builder.AppendLine("<section id=\"summary\">");
        builder.AppendLine("<h2>2. Підсумок стану проєкту</h2>");
        builder.AppendLine("<div class=\"metrics\">");
        builder.AppendLine($"<div class=\"metric\"><span>Усього задач</span><strong>{tasks.Count}</strong></div>");
        builder.AppendLine($"<div class=\"metric\"><span>Виконано</span><strong>{completed}</strong></div>");
        builder.AppendLine($"<div class=\"metric\"><span>Активні</span><strong>{active}</strong></div>");
        builder.AppendLine($"<div class=\"metric\"><span>Пов'язані файли</span><strong>{linkedFiles}</strong></div>");
        builder.AppendLine("</div>");
        if (overdue > 0)
        {
            builder.AppendLine($"<p><span class=\"status overdue\">Прострочені задачі: {overdue}</span></p>");
        }
        builder.AppendLine("</section>");

        builder.AppendLine("<section id=\"tasks\">");
        builder.AppendLine("<h2>3. Задачі та статус виконання</h2>");
        builder.AppendLine("<table>");
        builder.AppendLine("<thead><tr><th>#</th><th>Задача</th><th>Статус</th><th>Дедлайн</th><th>Файлів</th></tr></thead><tbody>");
        var index = 1;
        foreach (var task in tasks)
        {
            var links = linksByTask.TryGetValue(task.Id, out var taskLinks) ? taskLinks : [];
            builder.AppendLine($"<tr><td>{index++}</td><td>{H(task.Title)}</td><td>{StatusHtml(task)}</td><td>{Due(task)}</td><td>{links.Count}</td></tr>");
        }
        builder.AppendLine("</tbody></table>");

        foreach (var task in tasks)
        {
            var links = linksByTask.TryGetValue(task.Id, out var taskLinks) ? taskLinks : [];
            builder.AppendLine("<article class=\"task\">");
            builder.AppendLine($"<h3>{H(task.Title)} {StatusHtml(task)}</h3>");
            builder.AppendLine($"<p>{H(string.IsNullOrWhiteSpace(task.Description) ? "Опис не заповнений." : task.Description)}</p>");
            builder.AppendLine($"<p class=\"small muted\">Черга: {task.Priority} | Дедлайн: {Due(task)} | Аудит: {H(task.CompletionAuditText)}</p>");
            if (links.Count == 0)
            {
                builder.AppendLine("<p class=\"muted\">Пов'язаних файлів немає.</p>");
            }
            else
            {
                builder.AppendLine("<ul class=\"file-list\">");
                foreach (var link in links.OrderBy(x => x.FilePath, StringComparer.OrdinalIgnoreCase))
                {
                    builder.AppendLine($"<li>{H(ToProjectRelativePath(project.ProjectDirectoryPath, link.FilePath))} <span class=\"muted\">({(link.IsDirectory ? "folder" : "file")}, {link.LinkedAtUtc.ToLocalTime():yyyy-MM-dd})</span></li>");
                }
                builder.AppendLine("</ul>");
            }
            builder.AppendLine("</article>");
        }
        builder.AppendLine("</section>");

        builder.AppendLine("<section id=\"files\">");
        builder.AppendLine("<h2>4. Пов'язані файли</h2>");
        builder.AppendLine("<table>");
        builder.AppendLine("<thead><tr><th>Файл / папка</th><th>Задача</th><th>Статус задачі</th></tr></thead><tbody>");
        foreach (var row in linksByTask.SelectMany(pair => pair.Value.Select(link => new { Task = tasks.First(t => t.Id == pair.Key), Link = link }))
                     .OrderBy(x => x.Link.FilePath, StringComparer.OrdinalIgnoreCase))
        {
            builder.AppendLine($"<tr><td>{H(ToProjectRelativePath(project.ProjectDirectoryPath, row.Link.FilePath))}</td><td>{H(row.Task.Title)}</td><td>{StatusHtml(row.Task)}</td></tr>");
        }
        builder.AppendLine("</tbody></table>");
        builder.AppendLine("</section>");

        builder.AppendLine("</body></html>");
        return builder.ToString();
    }

    private static string StatusHtml(TaskItem task)
    {
        if (task.IsCompleted)
        {
            return "<span class=\"status done\">Виконано</span>";
        }

        return task.DueDateUtc.HasValue && task.DueDateUtc.Value.ToLocalTime() < DateTime.Now
            ? "<span class=\"status overdue\">Прострочено</span>"
            : "<span class=\"status active\">Не виконано</span>";
    }

    private static string Due(TaskItem task)
    {
        return task.DueDateUtc.HasValue ? H(task.DueDateUtc.Value.ToLocalTime().ToString("yyyy-MM-dd")) : "<span class=\"muted\">Без дедлайну</span>";
    }

    private static string ToProjectRelativePath(string rootPath, string path)
    {
        try
        {
            var root = Path.GetFullPath(rootPath);
            var fullPath = Path.GetFullPath(path);
            return fullPath.StartsWith(root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                ? Path.GetRelativePath(root, fullPath)
                : path;
        }
        catch
        {
            return path;
        }
    }

    private static string H(string value)
    {
        return WebUtility.HtmlEncode(value);
    }
}
