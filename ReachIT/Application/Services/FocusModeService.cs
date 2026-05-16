using ReachIT.Application.Contracts;
using ReachIT.Application.Security;
using ReachIT.Domain.Enums;
using ReachIT.Domain.Models;
using ReachIT.Infrastructure.Persistence;
using Microsoft.Win32;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ReachIT.Application.Services;

public sealed class FocusModeService : IFocusModeService, IDisposable
{
    private enum BrowserWindowClassification
    {
        Neutral,
        AllowedMusic,
        DistractingMedia
    }

    private readonly IDatabaseService _databaseService;
    private readonly IForegroundWindowService _foregroundWindowService;
    private readonly IAppSettingsService _settingsService;
    private readonly IProjectService _projectService;
    private readonly IActiveBrowserUrlService _activeBrowserUrlService;
    private FocusSession? _currentSession;
    private CancellationTokenSource? _monitoringCts;
    private AppSettings _settings = new();
    private bool _isPaused;
    private DateTime _sessionStartTime;
    private TimeSpan _totalPausedTime = TimeSpan.Zero;
    private DateTime? _pauseStartTime;
    private string _lastViolationKey = string.Empty;
    private DateTime _lastViolationUtc = DateTime.MinValue;
    private readonly object _temporaryBrowserUrlGate = new();
    private readonly Dictionary<string, DateTime> _temporaryAllowedBrowserUrls = new(StringComparer.OrdinalIgnoreCase);

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
        "SnippingTool",
        "ScreenClippingHost",
        "Snipaste",
        "ShareX",
        "Lightshot",
        "Greenshot",
        "git-bash",
        "putty",
        "winscp",
        "postman",
        "insomnia",
        "docker desktop",
        "Docker Desktop",
        "Spotify",
        "Music.UI",
        "iTunes",
        "slack",
        "Teams",
        "Zoom"
    ];

    private static readonly string[] AllowedMusicWindowTerms =
    [
        "YouTube Music",
        "music.youtube.com",
        "Spotify",
        "SoundCloud",
        "Apple Music",
        "Deezer",
        "TIDAL",
        "Bandcamp"
    ];

    private static readonly string[] DistractingBrowserWindowTerms =
    [
        "YouTube Shorts",
        "Shorts - YouTube",
        "#shorts",
        "TikTok",
        "Instagram Reels",
        "Reels",
        "Facebook Watch",
        "Netflix",
        "Disney+",
        "Hulu",
        "Prime Video",
        "Reddit",
        "9GAG"
    ];

    public event Action? StateChanged;
    public event Action<string>? DistractionDetected;

    public FocusModeService(
        IDatabaseService databaseService,
        IForegroundWindowService foregroundWindowService,
        IAppSettingsService settingsService,
        IProjectService projectService,
        IActiveBrowserUrlService activeBrowserUrlService)
    {
        _databaseService = databaseService;
        _foregroundWindowService = foregroundWindowService;
        _settingsService = settingsService;
        _projectService = projectService;
        _activeBrowserUrlService = activeBrowserUrlService;
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
        ClearTemporaryBrowserUrlAllowances();

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
        ClearTemporaryBrowserUrlAllowances();
        StateChanged?.Invoke();
    }

    public void AllowBrowserUrlOnce(string url)
    {
        var normalizedUrl = NormalizeBrowserUrlForTemporaryAllowance(url);
        if (string.IsNullOrWhiteSpace(normalizedUrl))
        {
            return;
        }

        lock (_temporaryBrowserUrlGate)
        {
            _temporaryAllowedBrowserUrls[normalizedUrl] = DateTime.UtcNow.AddMinutes(30);
        }
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
        if (IsBrowserProcess(current.ProcessName))
        {
            var activeBrowserUrl = _activeBrowserUrlService.TryGetActiveBrowserUrl() ?? string.Empty;
            if (IsTemporarilyAllowedBrowserUrl(activeBrowserUrl))
            {
                return;
            }

            if (IsAllowedMusicUrl(activeBrowserUrl))
            {
                return;
            }

            if (IsDistractingBrowserUrl(activeBrowserUrl))
            {
                RaiseViolation(current, "distracting browser media");
                return;
            }

            var browserClassification = ClassifyBrowserWindow(current.WindowTitle);
            if (browserClassification == BrowserWindowClassification.AllowedMusic)
            {
                return;
            }

            if (browserClassification == BrowserWindowClassification.DistractingMedia)
            {
                RaiseViolation(current, "distracting browser media");
                return;
            }

            var browserProfile = GetProjectBrowserFocusProfile();
            if (browserProfile.Count > 0)
            {
                if (MatchesAny(current.WindowTitle, browserProfile) || MatchesAny(activeBrowserUrl, browserProfile))
                {
                    return;
                }

                RaiseViolation(current, "browser page outside project web focus");
                return;
            }
        }

        var isAllowed = MatchesAny(current.ProcessName, allowedApps) || MatchesAny(current.AppName, allowedApps);
        if (isAllowed)
        {
            return;
        }

        if (IsSteamShellProcess(current.ProcessName))
        {
            return;
        }

        RaiseViolation(current, SteamGameCatalog.IsSteamGameExecutable(current.ExecutablePath)
            ? "Steam game"
            : "not allowed");
    }

    private static BrowserWindowClassification ClassifyBrowserWindow(string windowTitle)
    {
        if (string.IsNullOrWhiteSpace(windowTitle))
        {
            return BrowserWindowClassification.Neutral;
        }

        if (MatchesAny(windowTitle, AllowedMusicWindowTerms))
        {
            return BrowserWindowClassification.AllowedMusic;
        }

        if (MatchesAny(windowTitle, DistractingBrowserWindowTerms))
        {
            return BrowserWindowClassification.DistractingMedia;
        }

        return BrowserWindowClassification.Neutral;
    }

    private static bool IsAllowedMusicUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return false;
        }

        var host = NormalizeHost(uri.Host);
        return host is "music.youtube.com"
            or "open.spotify.com"
            or "soundcloud.com"
            or "music.apple.com"
            or "deezer.com"
            or "tidal.com"
            or "bandcamp.com";
    }

    private static bool IsDistractingBrowserUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return false;
        }

        var host = NormalizeHost(uri.Host);
        var path = Uri.UnescapeDataString(uri.AbsolutePath).Trim('/').ToLowerInvariant();

        if (host is "tiktok.com" or "vm.tiktok.com" or "netflix.com" or "disneyplus.com" or "hulu.com")
        {
            return true;
        }

        if (host.EndsWith("youtube.com", StringComparison.OrdinalIgnoreCase))
        {
            return path.StartsWith("shorts", StringComparison.OrdinalIgnoreCase);
        }

        if (host.EndsWith("instagram.com", StringComparison.OrdinalIgnoreCase))
        {
            return path.StartsWith("reels", StringComparison.OrdinalIgnoreCase)
                || path.StartsWith("explore", StringComparison.OrdinalIgnoreCase);
        }

        if (host.EndsWith("facebook.com", StringComparison.OrdinalIgnoreCase))
        {
            return path.StartsWith("watch", StringComparison.OrdinalIgnoreCase)
                || path.StartsWith("reel", StringComparison.OrdinalIgnoreCase)
                || path.Contains("/reels/", StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }

    private static string NormalizeHost(string host)
    {
        host = host.Trim().TrimEnd('/').ToLowerInvariant();
        return host.StartsWith("www.", StringComparison.OrdinalIgnoreCase)
            ? host[4..]
            : host;
    }

    private void RaiseViolation(ForegroundWindowSnapshot current, string rule)
    {
        var key = $"{current.ProcessName}|{rule}|{current.WindowTitle}";
        var now = DateTime.UtcNow;
        if (string.Equals(_lastViolationKey, key, StringComparison.OrdinalIgnoreCase) &&
            (now - _lastViolationUtc).TotalSeconds < 6)
        {
            return;
        }

        _lastViolationKey = key;
        _lastViolationUtc = now;

        var title = string.IsNullOrWhiteSpace(current.WindowTitle) ? current.ProcessName : current.WindowTitle;
        DistractionDetected?.Invoke($"{current.ProcessName} ({rule}) - {title}");
    }

    private bool IsTemporarilyAllowedBrowserUrl(string url)
    {
        var normalizedUrl = NormalizeBrowserUrlForTemporaryAllowance(url);
        if (string.IsNullOrWhiteSpace(normalizedUrl))
        {
            return false;
        }

        lock (_temporaryBrowserUrlGate)
        {
            RemoveExpiredTemporaryBrowserUrlAllowances();
            return _temporaryAllowedBrowserUrls.ContainsKey(normalizedUrl);
        }
    }

    private void ClearTemporaryBrowserUrlAllowances()
    {
        lock (_temporaryBrowserUrlGate)
        {
            _temporaryAllowedBrowserUrls.Clear();
        }
    }

    private void RemoveExpiredTemporaryBrowserUrlAllowances()
    {
        var now = DateTime.UtcNow;
        foreach (var expiredUrl in _temporaryAllowedBrowserUrls
                     .Where(pair => pair.Value <= now)
                     .Select(pair => pair.Key)
                     .ToList())
        {
            _temporaryAllowedBrowserUrls.Remove(expiredUrl);
        }
    }

    private static string NormalizeBrowserUrlForTemporaryAllowance(string url)
    {
        if (string.IsNullOrWhiteSpace(url) || !WebResourceSecurity.IsSafeWebUrl(url))
        {
            return string.Empty;
        }

        var normalizedUrl = WebResourceSecurity.NormalizeAndValidateUrl(url);
        if (!Uri.TryCreate(normalizedUrl, UriKind.Absolute, out var uri))
        {
            return string.Empty;
        }

        var builder = new UriBuilder(uri)
        {
            Fragment = string.Empty,
            Query = string.Empty
        };

        return builder.Uri.AbsoluteUri.TrimEnd('/');
    }

    private static List<string> GetAllowedApps(AppSettings settings)
    {
        return DefaultAllowedApps
            .Concat(settings.AllowedApplications)
            .Where(app => !string.IsNullOrWhiteSpace(app))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private List<string> GetProjectBrowserFocusProfile()
    {
        var projectDirectory = _projectService.CurrentProject?.ProjectDirectoryPath;
        if (string.IsNullOrWhiteSpace(projectDirectory) || !Directory.Exists(projectDirectory))
        {
            return [];
        }

        var webResourcesDirectory = Path.Combine(projectDirectory, "Web Resources");
        if (!Directory.Exists(webResourcesDirectory))
        {
            return [];
        }

        var terms = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var linkFile in Directory.EnumerateFiles(webResourcesDirectory, "*.reachit-link.json", SearchOption.TopDirectoryOnly))
        {
            try
            {
                var json = File.ReadAllText(linkFile);
                var metadata = JsonSerializer.Deserialize<WebResourceLinkMetadata>(
                    json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (metadata is null)
                {
                    continue;
                }

                if (!WebResourceSecurity.IsSafeWebUrl(metadata.PrimaryUrl))
                {
                    continue;
                }

                AddBrowserTerm(terms, metadata.Title);
                AddUrlTerms(terms, metadata.PrimaryUrl);
                foreach (var url in WebResourceSecurity.NormalizeAndValidateUrls(metadata.AlternateUrls))
                {
                    AddUrlTerms(terms, url);
                }

                foreach (var host in WebResourceSecurity.NormalizeAndValidateHosts(metadata.AllowedFocusHosts))
                {
                    AddHostTerms(terms, host);
                }
            }
            catch
            {
                // Malformed sidecars are ignored so focus mode remains usable.
            }
        }

        return terms.Where(term => term.Length >= 3).ToList();
    }

    private static void AddUrlTerms(ISet<string> terms, string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return;
        }

        AddHostTerms(terms, uri.Host);
        foreach (var segment in uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            AddBrowserTerm(terms, Uri.UnescapeDataString(segment).Replace('-', ' ').Replace('_', ' '));
        }
    }

    private static void AddHostTerms(ISet<string> terms, string host)
    {
        host = host.Trim().TrimEnd('/').ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(host))
        {
            return;
        }

        if (Uri.TryCreate(host, UriKind.Absolute, out var uri))
        {
            host = uri.Host.ToLowerInvariant();
        }

        if (host.StartsWith("www.", StringComparison.OrdinalIgnoreCase))
        {
            host = host[4..];
        }

        AddBrowserTerm(terms, host);
        foreach (var part in host.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            AddBrowserTerm(terms, part);
        }
    }

    private static void AddBrowserTerm(ISet<string> terms, string term)
    {
        term = term.Trim();
        if (term.Length >= 3)
        {
            terms.Add(term);
        }
    }

    private static bool MatchesAny(string value, IEnumerable<string> patterns)
    {
        return !string.IsNullOrWhiteSpace(value) &&
               patterns.Any(pattern => !string.IsNullOrWhiteSpace(pattern) &&
                                       value.Contains(pattern, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsBrowserProcess(string processName)
    {
        return processName.Equals("chrome", StringComparison.OrdinalIgnoreCase)
               || processName.Equals("msedge", StringComparison.OrdinalIgnoreCase)
               || processName.Equals("firefox", StringComparison.OrdinalIgnoreCase)
               || processName.Equals("brave", StringComparison.OrdinalIgnoreCase)
               || processName.Equals("opera", StringComparison.OrdinalIgnoreCase)
               || processName.Equals("vivaldi", StringComparison.OrdinalIgnoreCase);
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
