using Microsoft.EntityFrameworkCore;
using ReachIT.Application.Contracts;
using ReachIT.Domain.Enums;
using ReachIT.Domain.Models;

namespace ReachIT.Application.Services;

public sealed class ProjectProgressService : IProjectProgressService
{
    private readonly IDatabaseService _databaseService;
    private readonly IWorkItemService _workItemService;
    private readonly IProgressCalculationService _progressCalculationService;
    private readonly ITaskSuggestionService _taskSuggestionService;

    public ProjectProgressService(
        IDatabaseService databaseService,
        IWorkItemService workItemService,
        IProgressCalculationService progressCalculationService,
        ITaskSuggestionService taskSuggestionService)
    {
        _databaseService = databaseService;
        _workItemService = workItemService;
        _progressCalculationService = progressCalculationService;
        _taskSuggestionService = taskSuggestionService;
    }

    public async Task<ProjectProgressSnapshot> GetSnapshotAsync(ProjectMeta project, CancellationToken cancellationToken = default)
    {
        await _workItemService.SyncLegacyTasksAsync(project, cancellationToken).ConfigureAwait(false);
        await _progressCalculationService.RecalculateProjectAsync(project.Id, cancellationToken).ConfigureAwait(false);
        await _taskSuggestionService.GenerateFromActivityAsync(project, cancellationToken).ConfigureAwait(false);

        await using var db = _databaseService.CreateDbContext();
        var items = await db.WorkItems
            .Where(x => x.ProjectId == project.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var units = await db.WorkUnits
            .Where(x => x.ProjectId == project.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var milestones = await db.Milestones
            .Where(x => x.ProjectId == project.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var suggestions = await db.TaskSuggestions
            .Where(x => x.ProjectId == project.Id && x.Status == TaskSuggestionStatus.New)
            .OrderByDescending(x => x.Confidence)
            .Take(5)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var trackable = items.Where(x => x.Type is not WorkItemType.Note).ToList();
        var progress = trackable.Count == 0
            ? 0
            : Math.Round(trackable.Average(x => x.ProgressPercent), 1);
        var today = DateTime.UtcNow.Date;
        var changedToday = units
            .Where(x => x.CreatedAt.Date == today && x.Type is WorkUnitType.FileChanged or WorkUnitType.DocumentEdited or WorkUnitType.LinesAdded or WorkUnitType.LinesModified)
            .Select(x => x.MetadataJson)
            .Distinct()
            .Count();
        var focusMinutes = units
            .Where(x => x.CreatedAt.Date == today && x.Type == WorkUnitType.FocusMinutes)
            .Sum(x => x.Value);
        var staleThreshold = DateTime.UtcNow.AddDays(-14);

        return new ProjectProgressSnapshot
        {
            ProjectId = project.Id,
            ProjectProgressPercent = progress,
            ActiveMilestone = milestones
                .Where(x => x.Status is WorkItemStatus.InProgress or WorkItemStatus.Planned)
                .OrderBy(x => x.Deadline ?? DateTime.MaxValue)
                .FirstOrDefault(),
            TasksDone = items.Count(x => x.Status == WorkItemStatus.Done),
            TasksAll = items.Count(x => x.Type is WorkItemType.Task or WorkItemType.Subtask or WorkItemType.Bug or WorkItemType.Coding or WorkItemType.Design or WorkItemType.Writing),
            WorkItemsInProgress = items.Count(x => x.Status == WorkItemStatus.InProgress),
            FilesChangedToday = changedToday,
            FocusMinutesToday = focusMinutes,
            SuggestedTasks = suggestions,
            StaleTasks = items
                .Where(x => x.Status is not WorkItemStatus.Done and not WorkItemStatus.Cancelled && x.UpdatedAt < staleThreshold)
                .OrderBy(x => x.UpdatedAt)
                .Take(5)
                .ToList(),
            RecentlyCompletedTasks = items
                .Where(x => x.CompletedAt.HasValue)
                .OrderByDescending(x => x.CompletedAt)
                .Take(5)
                .ToList(),
            FilesWithActivityWithoutTasks = [],
            PossiblyCompletedTasks = items
                .Where(x => x.Status != WorkItemStatus.Done && x.ProgressPercent >= 90)
                .OrderByDescending(x => x.ProgressPercent)
                .Take(5)
                .ToList(),
            CurrentWorkContext = suggestions.FirstOrDefault()?.SuggestedLinkedPath
                ?? items.OrderByDescending(x => x.UpdatedAt).FirstOrDefault()?.Title
                ?? project.ProjectName
        };
    }
}
