// Manages focus mode state without unsafe OS blocking behavior.
using ReachIT.Application.Contracts;
using ReachIT.Domain.Enums;
using ReachIT.Domain.Models;

namespace ReachIT.Application.Services;

public sealed class FocusModeService : IFocusModeService
{
    private readonly IProcessMonitorService _processMonitorService;
    private FocusSession? _currentSession;

    public FocusModeService(IProcessMonitorService processMonitorService)
    {
        _processMonitorService = processMonitorService;
    }

    public bool IsActive => _currentSession?.IsActive == true;

    public FocusModeType CurrentMode => _currentSession?.ModeType ?? FocusModeType.Light;

    public async Task StartAsync(FocusModeType mode = FocusModeType.Light, CancellationToken cancellationToken = default)
    {
        if (IsActive)
        {
            return;
        }

        _currentSession = new FocusSession
        {
            IsActive = true,
            ModeType = mode,
            StartedAtUtc = DateTime.UtcNow,
            AllowedApplications = ["ReachIT"]
        };

        _ = await _processMonitorService.GetRunningProcessNamesAsync(cancellationToken).ConfigureAwait(false);
        // TODO: Add safe process evaluation rules for Light/Strict/Aggressive modes.
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (_currentSession is not null)
        {
            _currentSession.EndedAtUtc = DateTime.UtcNow;
            _currentSession.IsActive = false;
        }

        return Task.CompletedTask;
    }

    public Task<FocusSession?> GetCurrentSessionAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_currentSession);
    }
}
