using ReachIT.Application.Contracts;
using ReachIT.Domain.Enums;
using ReachIT.Domain.Models;
using ReachIT.Infrastructure.Persistence;
using Microsoft.Win32;
using System.IO;
using System.Text.RegularExpressions;

namespace ReachIT.Application.Services;

public sealed class FocusModeService : IFocusModeService, IDisposable
{
    private readonly IDatabaseService _databaseService;
    private readonly IForegroundWindowService _foregroundWindowService;
    private readonly IAppSettingsService _settingsService;
    private FocusSession? _currentSession;
    private CancellationTokenSource? _monitoringCts;
    private AppSettings _settings = new();
    private bool _isPaused;
    private DateTime _sessionStartTime;
    private TimeSpan _totalPausedTime = TimeSpan.Zero;
    private DateTime? _pauseStartTime;
    private string _lastViolationKey = string.Empty;
    private DateTime _lastViolationUtc = DateTime.MinValue;

    private static readonly string[] DefaultAllowedApps =
    [
        "ReachIT",
        "explorer",
        "SearchHost",
        "ShellExperienceHost",
        "StartMenuExperienceHost",
        "ApplicationFrameHost",
        "Code",
        "Cursor",
        "devenv",
        "rider64",
        "idea64",
        "pycharm64",
        "webstorm64",
        "clion64",
        "datagrip64",
        "phpstorm64",
        "eclipse",
        "notepad",
        "notepad++",
        "Notepad",
        "WINWORD",
        "EXCEL",
        "POWERPNT",
        "OUTLOOK",
        "OneNote",
        "Acrobat",
        "FoxitPDFEditor",
        "chrome",
        "msedge",
        "firefox",
        "brave",
        "steam",
        "steamwebhelper",
        "GameOverlayUI",
        "FreeCAD",
        "blender",
        "Blockbench",
        "Aseprite",
        "Resolve",
        "fusion360",
        "acad",
        "SketchUp",
        "3dsmax",
        "Maya",
        "Photoshop",
        "Illustrator",
        "figma",
        "inkscape",
        "gimp",
        "paintdotnet",
        "PaintStudio.View",
        "WindowsTerminal",
        "wt",
        "powershell",
        "cmd",
        "git-bash",
        "putty",
        "winscp",
        "postman",
        "insomnia",
        "docker desktop",
        "Docker Desktop",
        "slack",
        "Teams",
        "Zoom"
    ];

    public event Action? StateChanged;
    public event Action<string>? DistractionDetected;

    public FocusModeService(
        IDatabaseService databaseService,
        IForegroundWindowService foregroundWindowService,
        IAppSettingsService settingsService)
    {
        _databaseService = databaseService;
        _foregroundWindowService = foregroundWindowService;
        _settingsService = settingsService;
    }

    public bool IsActive => _currentSession?.IsActive == true && !_isPaused;

    public FocusModeType CurrentMode => FocusModeType.Strict;

    public TimeSpan SessionDuration
    {
        get
        {
            if (_currentSession == null) return TimeSpan.Zero;
            var end = _pauseStartTime ?? DateTime.UtcNow;
            return end - _sessionStartTime - _totalPausedTime;
        }
    }

    public async Task StartAsync(FocusModeType mode = FocusModeType.Strict, CancellationToken cancellationToken = default)
    {
        _settings = await _settingsService.GetAsync(cancellationToken).ConfigureAwait(false);

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
            ModeType = FocusModeType.Strict,
            StartedAtUtc = DateTime.UtcNow,
            AllowedApplications = GetAllowedApps(_settings)
        };

        using var db = _databaseService.CreateDbContext();
        db.FocusSessions.Add(_currentSession);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _sessionStartTime = DateTime.UtcNow;
        _totalPausedTime = TimeSpan.Zero;
        _isPaused = false;
        _lastViolationKey = string.Empty;
        _lastViolationUtc = DateTime.MinValue;

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
                await Task.Delay(TimeSpan.FromSeconds(2), ct).ConfigureAwait(false);
                CheckForegroundWindow();
            }
            catch (OperationCanceledException) { }
            catch { /* Ignore */ }
        }
    }

    private void CheckForegroundWindow()
    {
        if (_currentSession is null || _isPaused)
        {
            return;
        }

        var current = _foregroundWindowService.GetCurrent();
        if (string.IsNullOrWhiteSpace(current.ProcessName))
        {
            return;
        }

        var allowedApps = _currentSession.AllowedApplications;
        var isAllowed = MatchesAny(current.ProcessName, allowedApps) || MatchesAny(current.AppName, allowedApps);
        if (isAllowed)
        {
            return;
        }

        if (IsSteamShellProcess(current.ProcessName))
        {
            return;
        }

        var key = $"{current.ProcessName}|{current.WindowTitle}";
        var now = DateTime.UtcNow;
        if (string.Equals(_lastViolationKey, key, StringComparison.OrdinalIgnoreCase) &&
            (now - _lastViolationUtc).TotalSeconds < 6)
        {
            return;
        }

        _lastViolationKey = key;
        _lastViolationUtc = now;

        var title = string.IsNullOrWhiteSpace(current.WindowTitle) ? current.ProcessName : current.WindowTitle;
        var rule = SteamGameCatalog.IsSteamGameExecutable(current.ExecutablePath)
            ? "Steam game"
            : "not allowed";
        DistractionDetected?.Invoke($"{current.ProcessName} ({rule}) - {title}");
    }

    private static List<string> GetAllowedApps(AppSettings settings)
    {
        return DefaultAllowedApps
            .Concat(settings.AllowedApplications)
            .Where(app => !string.IsNullOrWhiteSpace(app))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool MatchesAny(string value, IEnumerable<string> patterns)
    {
        return !string.IsNullOrWhiteSpace(value) &&
               patterns.Any(pattern => !string.IsNullOrWhiteSpace(pattern) &&
                                       value.Contains(pattern, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsSteamShellProcess(string processName)
    {
        return processName.Equals("steam", StringComparison.OrdinalIgnoreCase)
               || processName.Equals("steamwebhelper", StringComparison.OrdinalIgnoreCase)
               || processName.Equals("GameOverlayUI", StringComparison.OrdinalIgnoreCase);
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

file static class SteamGameCatalog
{
    private static readonly object Gate = new();
    private static DateTime _lastRefreshUtc = DateTime.MinValue;
    private static List<string> _gameDirectories = [];

    public static bool IsSteamGameExecutable(string executablePath)
    {
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            return false;
        }

        var gameDirectories = GetGameDirectories();
        return gameDirectories.Any(directory =>
            executablePath.StartsWith(directory + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase));
    }

    private static List<string> GetGameDirectories()
    {
        lock (Gate)
        {
            if ((DateTime.UtcNow - _lastRefreshUtc).TotalMinutes < 5)
            {
                return _gameDirectories;
            }

            _gameDirectories = ScanGameDirectories().Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            _lastRefreshUtc = DateTime.UtcNow;
            return _gameDirectories;
        }
    }

    private static IEnumerable<string> ScanGameDirectories()
    {
        foreach (var libraryPath in GetSteamLibraryPaths())
        {
            var steamApps = Path.Combine(libraryPath, "steamapps");
            if (!Directory.Exists(steamApps))
            {
                continue;
            }

            foreach (var manifestPath in Directory.EnumerateFiles(steamApps, "appmanifest_*.acf"))
            {
                string text;
                try
                {
                    text = File.ReadAllText(manifestPath);
                }
                catch
                {
                    continue;
                }

                var installDir = MatchVdfValue(text, "installdir");
                if (string.IsNullOrWhiteSpace(installDir))
                {
                    continue;
                }

                var gameDirectory = Path.Combine(steamApps, "common", installDir);
                if (Directory.Exists(gameDirectory))
                {
                    yield return Path.GetFullPath(gameDirectory);
                }
            }
        }
    }

    private static IEnumerable<string> GetSteamLibraryPaths()
    {
        var steamPath = GetSteamInstallPath();
        if (string.IsNullOrWhiteSpace(steamPath) || !Directory.Exists(steamPath))
        {
            yield break;
        }

        yield return steamPath;

        var libraryFoldersPath = Path.Combine(steamPath, "steamapps", "libraryfolders.vdf");
        if (!File.Exists(libraryFoldersPath))
        {
            yield break;
        }

        string text;
        try
        {
            text = File.ReadAllText(libraryFoldersPath);
        }
        catch
        {
            yield break;
        }

        foreach (Match match in Regex.Matches(text, "\"path\"\\s+\"(?<path>[^\"]+)\"", RegexOptions.IgnoreCase))
        {
            var path = match.Groups["path"].Value.Replace(@"\\", @"\");
            if (Directory.Exists(path))
            {
                yield return path;
            }
        }
    }

    private static string GetSteamInstallPath()
    {
        return ReadSteamPath(RegistryHive.LocalMachine, RegistryView.Registry64)
               ?? ReadSteamPath(RegistryHive.LocalMachine, RegistryView.Registry32)
               ?? ReadSteamPath(RegistryHive.CurrentUser, RegistryView.Registry64)
               ?? ReadSteamPath(RegistryHive.CurrentUser, RegistryView.Registry32)
               ?? string.Empty;
    }

    private static string? ReadSteamPath(RegistryHive hive, RegistryView view)
    {
        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(hive, view);
            using var key = baseKey.OpenSubKey(@"SOFTWARE\Valve\Steam");
            return key?.GetValue("InstallPath") as string;
        }
        catch
        {
            return null;
        }
    }

    private static string MatchVdfValue(string text, string key)
    {
        var match = Regex.Match(text, $"\"{Regex.Escape(key)}\"\\s+\"(?<value>[^\"]+)\"", RegexOptions.IgnoreCase);
        return match.Success ? match.Groups["value"].Value : string.Empty;
    }
}
