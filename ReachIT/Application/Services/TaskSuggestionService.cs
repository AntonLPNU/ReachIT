using System.Text.Json;
using System.IO;
using Microsoft.EntityFrameworkCore;
using ReachIT.Application.Contracts;
using ReachIT.Domain.Enums;
using ReachIT.Domain.Models;

namespace ReachIT.Application.Services;

public sealed class TaskSuggestionService : ITaskSuggestionService
{
    private readonly IDatabaseService _databaseService;
    private readonly IWorkItemService _workItemService;

    public TaskSuggestionService(IDatabaseService databaseService, IWorkItemService workItemService)
    {
        _databaseService = databaseService;
        _workItemService = workItemService;
    }

    public async Task<IReadOnlyList<TaskSuggestion>> GetNewSuggestionsAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        await using var db = _databaseService.CreateDbContext();
        return await db.TaskSuggestions
            .Where(x => x.ProjectId == projectId && x.Status == TaskSuggestionStatus.New)
            .OrderByDescending(x => x.Confidence)
            .ThenByDescending(x => x.CreatedAt)
            .Take(10)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task GenerateFromActivityAsync(ProjectMeta project, CancellationToken cancellationToken = default)
    {
        await using var db = _databaseService.CreateDbContext();
        var since = DateTime.UtcNow.AddDays(-7);
        var units = await db.WorkUnits
            .Where(x => x.ProjectId == project.Id && x.WorkItemId == null && x.CreatedAt >= since)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var existingPaths = await db.WorkItems
            .Where(x => x.ProjectId == project.Id && x.LinkedPath != string.Empty)
            .Select(x => x.LinkedPath)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var existingSuggestions = await db.TaskSuggestions
            .Where(x => x.ProjectId == project.Id && x.Status == TaskSuggestionStatus.New)
            .Select(x => x.SuggestedLinkedPath)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var byPath = units
            .Select(x => TryGetPath(x.MetadataJson, out var path) ? path : string.Empty)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Where(x => !existingPaths.Contains(x, StringComparer.OrdinalIgnoreCase))
            .Where(x => !existingSuggestions.Contains(x, StringComparer.OrdinalIgnoreCase))
            .GroupBy(x => x, StringComparer.OrdinalIgnoreCase)
            .Where(x => x.Count() >= 2)
            .Take(10);

        foreach (var group in byPath)
        {
            var fileName = Path.GetFileName(group.Key);
            db.TaskSuggestions.Add(new TaskSuggestion
            {
                ProjectId = project.Id,
                SuggestedTitle = $"Work on {fileName}",
                SuggestedDescription = $"ReachIT noticed repeated activity in {fileName}. Create a linked work item to track this work.",
                SuggestedType = GuessType(group.Key),
                SuggestedLinkedPath = group.Key,
                Confidence = Math.Clamp(group.Count() / 5d, 0.35, 0.95),
                Reason = $"Detected {group.Count()} recent activity events without a linked task.",
                CreatedAt = DateTime.UtcNow,
                Status = TaskSuggestionStatus.New
            });
        }

        var activeWorkItems = await db.WorkItems
            .Where(x => x.ProjectId == project.Id && x.Status != WorkItemStatus.Done && x.Status != WorkItemStatus.Cancelled)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var existingCompletionSuggestionTitles = await db.TaskSuggestions
            .Where(x => x.ProjectId == project.Id && x.Status == TaskSuggestionStatus.New && x.Reason.Contains("may be completed"))
            .Select(x => x.SuggestedTitle)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var activityByWorkItem = units
            .Where(x => x.WorkItemId.HasValue)
            .GroupBy(x => x.WorkItemId!.Value)
            .Where(x => x.Count() >= 3)
            .ToDictionary(x => x.Key, x => x.Count());

        foreach (var item in activeWorkItems.Where(x => activityByWorkItem.ContainsKey(x.Id)).Take(10))
        {
            var title = $"Review completion: {item.Title}";
            if (existingCompletionSuggestionTitles.Contains(title, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            db.TaskSuggestions.Add(new TaskSuggestion
            {
                ProjectId = project.Id,
                SuggestedTitle = title,
                SuggestedDescription = $"ReachIT detected sustained activity on '{item.Title}'. Ask before marking it done.",
                SuggestedType = item.Type,
                SuggestedLinkedPath = item.LinkedPath,
                Confidence = Math.Clamp(activityByWorkItem[item.Id] / 6d, 0.45, 0.9),
                Reason = $"Work item may be completed after {activityByWorkItem[item.Id]} recent activity events.",
                CreatedAt = DateTime.UtcNow,
                Status = TaskSuggestionStatus.New
            });
        }

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<WorkItem?> AcceptAsync(Guid suggestionId, CancellationToken cancellationToken = default)
    {
        await using var db = _databaseService.CreateDbContext();
        var suggestion = await db.TaskSuggestions.FirstOrDefaultAsync(x => x.Id == suggestionId, cancellationToken).ConfigureAwait(false);
        if (suggestion is null)
        {
            return null;
        }

        suggestion.Status = TaskSuggestionStatus.Accepted;
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return await _workItemService.CreateAsync(new WorkItem
        {
            ProjectId = suggestion.ProjectId,
            Title = suggestion.SuggestedTitle,
            Description = suggestion.SuggestedDescription,
            Type = suggestion.SuggestedType,
            Status = WorkItemStatus.Planned,
            LinkedPath = suggestion.SuggestedLinkedPath,
            EstimatedWorkUnits = 3
        }, cancellationToken).ConfigureAwait(false);
    }

    public async Task IgnoreAsync(Guid suggestionId, CancellationToken cancellationToken = default)
    {
        await using var db = _databaseService.CreateDbContext();
        var suggestion = await db.TaskSuggestions.FirstOrDefaultAsync(x => x.Id == suggestionId, cancellationToken).ConfigureAwait(false);
        if (suggestion is null)
        {
            return;
        }

        suggestion.Status = TaskSuggestionStatus.Ignored;
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private static WorkItemType GuessType(string path)
    {
        var extension = Path.GetExtension(path).ToLowerInvariant();
        return extension switch
        {
            ".cs" or ".xaml" or ".js" or ".ts" or ".py" => WorkItemType.Coding,
            ".md" or ".txt" or ".docx" => WorkItemType.Writing,
            ".png" or ".jpg" or ".jpeg" or ".psd" or ".fig" => WorkItemType.Design,
            _ => WorkItemType.Task
        };
    }

    private static bool TryGetPath(string metadataJson, out string path)
    {
        path = string.Empty;
        try
        {
            using var doc = JsonDocument.Parse(metadataJson);
            if (doc.RootElement.TryGetProperty("path", out var value))
            {
                path = value.GetString() ?? string.Empty;
            }
        }
        catch (JsonException)
        {
            return false;
        }

        return !string.IsNullOrWhiteSpace(path);
    }
}
