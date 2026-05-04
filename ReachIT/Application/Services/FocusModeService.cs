// Manages focus mode state without unsafe OS blocking behavior.
using ReachIT.Application.Contracts;
using ReachIT.Domain.Enums;
using ReachIT.Domain.Models;
using ReachIT.Infrastructure.Persistence;

namespace ReachIT.Application.Services;

public sealed class FocusModeService : IFocusModeService, IDisposable
{
    private readonly IProcessMonitorService _processMonitorService;
    private readonly IDatabaseService _databaseService;
    private FocusSession? _currentSession;
    private CancellationTokenSource? _monitoringCts;
    private bool _isPaused;
    private DateTime _sessionStartTime;
    private TimeSpan _totalPausedTime = TimeSpan.Zero;
    private DateTime? _pauseStartTime;

    private readonly HashSet<string> _distractingApps = new(StringComparer.OrdinalIgnoreCase) 
    {
        "chrome", "msedge", "firefox", "discord", "spotify", "slack", "telegram", "telegram.exe"
    };

    public event Action? StateChanged;
    public event Action<string>? DistractionDetected;

    public FocusModeService(IProcessMonitorService processMonitorService, IDatabaseService databaseService)
    {
        _processMonitorService = processMonitorService;
        _databaseService = databaseService;
    }

    public bool IsActive => _currentSession?.IsActive == true && !_isPaused;

    public FocusModeType CurrentMode => _currentSession?.ModeType ?? FocusModeType.Light;

    public TimeSpan SessionDuration
    {
        get
        {
            if (_currentSession == null) return TimeSpan.Zero;
            var end = _pauseStartTime ?? DateTime.UtcNow;
            return end - _sessionStartTime - _totalPausedTime;
        }
    }

    public async Task StartAsync(FocusModeType mode = FocusModeType.Light, CancellationToken cancellationToken = default)
    {
        if (_currentSession != null && _isPaused)
        {
            _isPaused = false;
            _totalPausedTime += DateTime.UtcNow - _pauseStartTime!.Value;
            _pauseStartTime = null;
            StartMonitoring();
            StateChanged?.Invoke();
            return;
        }

        if (IsActive) return;

        _currentSession = new FocusSession
        {
            IsActive = true,
            ModeType = mode,
            StartedAtUtc = DateTime.UtcNow,
            AllowedApplications = ["ReachIT", "devenv", "Code"]
        };

        using var db = _databaseService.CreateDbContext();
        db.FocusSessions.Add(_currentSession);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _sessionStartTime = DateTime.UtcNow;
        _totalPausedTime = TimeSpan.Zero;
        _isPaused = false;

        StartMonitoring();
        StateChanged?.Invoke();
    }

    public Task PauseAsync(CancellationToken cancellationToken = default)
    {
        if (!IsActive) return Task.CompletedTask;

        _isPaused = true;
        _pauseStartTime = DateTime.UtcNow;
        StopMonitoring();
        StateChanged?.Invoke();

        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        StopMonitoring();

        if (_currentSession is not null)
        {
            _currentSession.EndedAtUtc = DateTime.UtcNow;
            _currentSession.IsActive = false;

            using var db = _databaseService.CreateDbContext();
            db.FocusSessions.Update(_currentSession);
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            _currentSession = null;
        }

        _isPaused = false;
        StateChanged?.Invoke();
    }

    private void StartMonitoring()
    {
        _monitoringCts = new CancellationTokenSource();
        _ = MonitoringLoopAsync(_monitoringCts.Token);
    }

    private void StopMonitoring()
    {
        _monitoringCts?.Cancel();
        _monitoringCts?.Dispose();
        _monitoringCts = null;
    }

    private async Task MonitoringLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(5000, ct);

                var processes = await _processMonitorService.GetRunningProcessNamesAsync(ct);
                var violations = processes.Where(p => _distractingApps.Contains(p)).ToList();

                foreach(var v in violations)
                {
                    DistractionDetected?.Invoke(v);
                }
            }
            catch (OperationCanceledException) { }
            catch { /* Ignore */ }
        }
    }

    public Task<FocusSession?> GetCurrentSessionAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_currentSession);
    }

    public void Dispose()
    {
        StopMonitoring();
    }
}
