using System.IO;
using System.Text.Json;
using ReachIT.Application.Contracts;
using ReachIT.Domain.Enums;
using ReachIT.Domain.Models;

namespace ReachIT.Application.Services;

public sealed class GitActivityService : IGitActivityService
{
    private readonly IGitService _gitService;
    private readonly Dictionary<Guid, string> _lastFingerprintByProject = new();

    public GitActivityService(IGitService gitService)
    {
        _gitService = gitService;
    }

    public async Task<IReadOnlyList<ActivityEvent>> ScanAsync(ProjectMeta project, CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(Path.Combine(project.ProjectDirectoryPath, ".git")))
        {
            return [];
        }

        var status = await _gitService.RunAsync(project.ProjectDirectoryPath, ["status", "--short"], cancellationToken).ConfigureAwait(false);
        if (status.ExitCode != 0)
        {
            return [];
        }

        var fingerprint = status.Output.Trim();
        if (string.IsNullOrWhiteSpace(fingerprint) ||
            (_lastFingerprintByProject.TryGetValue(project.Id, out var previous) && string.Equals(previous, fingerprint, StringComparison.Ordinal)))
        {
            return [];
        }

        _lastFingerprintByProject[project.Id] = fingerprint;

        var diffStat = await _gitService.RunAsync(project.ProjectDirectoryPath, ["diff", "--stat"], cancellationToken).ConfigureAwait(false);
        var files = fingerprint
            .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(line => line.Length > 3 ? line[3..].Trim() : line.Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return
        [
            new ActivityEvent
            {
                ProjectId = project.Id,
                Timestamp = DateTime.UtcNow,
                EventType = ActivityEventType.GitChanged,
                Value = files.Count,
                MetadataJson = JsonSerializer.Serialize(new
                {
                    changedFiles = files,
                    status = fingerprint,
                    diffStat = diffStat.CombinedOutput
                })
            }
        ];
    }
}
