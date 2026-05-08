// Defines focus mode start/stop and status behavior.
using ReachIT.Domain.Enums;
using ReachIT.Domain.Models;

namespace ReachIT.Application.Contracts;

public interface IFocusModeService
{
    bool IsActive { get; }
    FocusModeType CurrentMode { get; }
    TimeSpan SessionDuration { get; }

    event Action StateChanged;
    event Action<string> DistractionDetected;

    Task StartAsync(FocusModeType mode = FocusModeType.Strict, CancellationToken cancellationToken = default);
    Task PauseAsync(CancellationToken cancellationToken = default);
    Task StopAsync(CancellationToken cancellationToken = default);
    Task<FocusSession?> GetCurrentSessionAsync(CancellationToken cancellationToken = default);
}
