using ReachIT.Domain.Enums;
using ReachIT.Domain.Models;

namespace ReachIT.Application.Contracts;

public interface IActivityMonitorService : IDisposable
{
    bool IsRunning { get; }
    Task StartAsync(ProjectMeta project, CancellationToken cancellationToken = default);
    Task StopAsync(CancellationToken cancellationToken = default);
    Task ReloadSettingsAsync(CancellationToken cancellationToken = default);
    Task RecordManualEventAsync(ProjectMeta project, ActivityEventType eventType, string metadataJson = "{}", CancellationToken cancellationToken = default);
}
