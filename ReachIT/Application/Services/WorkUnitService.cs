using System.Text.Json;
using System.IO;
using ReachIT.Application.Contracts;
using ReachIT.Domain.Enums;
using ReachIT.Domain.Models;

namespace ReachIT.Application.Services;

public sealed class WorkUnitService : IWorkUnitService
{
    private readonly IWorkUnitRepository _workUnitRepository;
    private readonly IWorkItemRepository _workItemRepository;

    public WorkUnitService(IWorkUnitRepository workUnitRepository, IWorkItemRepository workItemRepository)
    {
        _workUnitRepository = workUnitRepository;
        _workItemRepository = workItemRepository;
    }

    public async Task<WorkUnit> AddAsync(Guid projectId, Guid? workItemId, WorkUnitType type, double value, string source, string metadataJson = "{}", CancellationToken cancellationToken = default)
    {
        var unit = new WorkUnit
        {
            ProjectId = projectId,
            WorkItemId = workItemId,
            Type = type,
            Value = value,
            Source = source,
            CreatedAt = DateTime.UtcNow,
            MetadataJson = string.IsNullOrWhiteSpace(metadataJson) ? "{}" : metadataJson
        };

        await _workUnitRepository.AddAsync(unit, cancellationToken).ConfigureAwait(false);
        return unit;
    }

    public async Task RecordFileActivityAsync(ProjectMeta project, string path, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        var item = await _workItemRepository.GetByLinkedPathAsync(project.Id, path, cancellationToken).ConfigureAwait(false);
        var extension = Path.GetExtension(path).ToLowerInvariant();
        var type = extension is ".txt" or ".md" or ".docx" or ".rtf"
            ? WorkUnitType.DocumentEdited
            : WorkUnitType.FileChanged;

        var metadata = JsonSerializer.Serialize(new
        {
            path,
            extension,
            fileName = Path.GetFileName(path)
        });

        await AddAsync(project.Id, item?.Id, type, 1, "file_system", metadata, cancellationToken).ConfigureAwait(false);
    }
}
