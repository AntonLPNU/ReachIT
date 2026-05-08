// Represents focus mode controls and status for the UI.
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Win32;
using ReachIT.Application.Contracts;
using ReachIT.Domain.Enums;
using ReachIT.Presentation.Commands;

namespace ReachIT.Presentation.ViewModels;

public sealed class FocusModeViewModel : ViewModelBase, IDisposable
{
    private readonly IFocusModeService _focusModeService;
    private readonly IOverlayService _overlayService;
    private readonly IAppSettingsService _settingsService;
    private readonly IForegroundWindowService _foregroundWindowService;
    private FocusModeType _selectedMode = FocusModeType.Strict;
    private bool _isActive;
    private string _sessionDurationText = "00:00:00";
    private string _allowedApplicationsText = string.Empty;
    private string _manualApplicationName = string.Empty;
    private string _applicationSearchText = string.Empty;
    private string _selectedApplicationDetails = "Select an app to inspect its process details.";
    private string _focusStatusText = "Focus mode is off";
    private readonly DispatcherTimer _timer;

    public FocusModeViewModel(
        IFocusModeService focusModeService,
        IOverlayService overlayService,
        IAppSettingsService settingsService,
        IForegroundWindowService foregroundWindowService)
    {
        _focusModeService = focusModeService;
        _overlayService = overlayService;
        _settingsService = settingsService;
        _foregroundWindowService = foregroundWindowService;

        AllowedApplications = new ObservableCollection<string>();
        AllowedApplicationRules = new ObservableCollection<FocusApplicationRule>();
        InstalledApplications = new ObservableCollection<InstalledApplicationViewModel>();
        FilteredInstalledApplications = new ObservableCollection<InstalledApplicationViewModel>();
        DistractionLog = new ObservableCollection<string>();

        StartCommand = new AsyncCommand(_ => StartAsync(), _ => !_focusModeService.IsActive);
        PauseCommand = new AsyncCommand(_ => PauseAsync(), _ => _focusModeService.IsActive);
        StopCommand = new AsyncCommand(_ => StopAsync(), _ => _focusModeService.IsActive || _sessionDurationText != "00:00:00");
        SaveFocusRulesCommand = new AsyncCommand(_ => SaveFocusRulesAsync());
        AddActiveAppCommand = new AsyncCommand(_ => AddActiveAppAsync());
        AddManualApplicationCommand = new RelayCommand(_ => AddManualApplication());
        RemoveApplicationCommand = new RelayCommand(p => RemoveApplication(p as FocusApplicationRule));
        AddInstalledApplicationCommand = new RelayCommand(p => AddInstalledApplication(p as InstalledApplicationViewModel));
        RemoveInstalledApplicationCommand = new RelayCommand(p => RemoveInstalledApplication(p as InstalledApplicationViewModel));
        RefreshInstalledApplicationsCommand = new RelayCommand(_ => LoadInstalledApplications());
        ShowApplicationDetailsCommand = new RelayCommand(p => ShowApplicationDetails(p as FocusApplicationRule));

        _focusModeService.StateChanged += OnFocusStateChanged;
        _focusModeService.DistractionDetected += OnDistractionDetected;

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += (s, e) => UpdateTimerText();
    }

    public ObservableCollection<string> AllowedApplications { get; }
    public ObservableCollection<FocusApplicationRule> AllowedApplicationRules { get; }
    public ObservableCollection<InstalledApplicationViewModel> InstalledApplications { get; }
    public ObservableCollection<InstalledApplicationViewModel> FilteredInstalledApplications { get; }
    public ObservableCollection<string> DistractionLog { get; }

    public event EventHandler<string>? FocusWarningRequested;

    public FocusModeType SelectedMode
    {
        get => _selectedMode;
        set => SetProperty(ref _selectedMode, FocusModeType.Strict);
    }

    public bool IsActive
    {
        get => _isActive;
        private set 
        {
            if (SetProperty(ref _isActive, value))
            {
                if (_isActive) _timer.Start();
                else _timer.Stop();

                ((AsyncCommand)StartCommand).RaiseCanExecuteChanged();
                ((AsyncCommand)PauseCommand).RaiseCanExecuteChanged();
                ((AsyncCommand)StopCommand).RaiseCanExecuteChanged();
            }
        }
    }

    public string SessionDurationText
    {
        get => _sessionDurationText;
        private set => SetProperty(ref _sessionDurationText, value);
    }

    public string AllowedApplicationsText
    {
        get => _allowedApplicationsText;
        set
        {
            if (SetProperty(ref _allowedApplicationsText, value))
            {
                SyncList(AllowedApplications, ParseList(value));
            }
        }
    }

    public string ManualApplicationName
    {
        get => _manualApplicationName;
        set => SetProperty(ref _manualApplicationName, value);
    }

    public string ApplicationSearchText
    {
        get => _applicationSearchText;
        set
        {
            if (SetProperty(ref _applicationSearchText, value))
            {
                ApplyInstalledApplicationFilter();
            }
        }
    }

    public string SelectedApplicationDetails
    {
        get => _selectedApplicationDetails;
        private set => SetProperty(ref _selectedApplicationDetails, value);
    }

    public string FocusStatusText
    {
        get => _focusStatusText;
        private set => SetProperty(ref _focusStatusText, value);
    }

    public ICommand StartCommand { get; }
    public ICommand PauseCommand { get; }
    public ICommand StopCommand { get; }
    public ICommand SaveFocusRulesCommand { get; }
    public ICommand AddActiveAppCommand { get; }
    public ICommand AddManualApplicationCommand { get; }
    public ICommand RemoveApplicationCommand { get; }
    public ICommand AddInstalledApplicationCommand { get; }
    public ICommand RemoveInstalledApplicationCommand { get; }
    public ICommand RefreshInstalledApplicationsCommand { get; }
    public ICommand ShowApplicationDetailsCommand { get; }

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        var settings = await _settingsService.GetAsync(cancellationToken).ConfigureAwait(true);
        SelectedMode = FocusModeType.Strict;
        ApplyAllowedApplications(MergeWithDefaultAllowedApplications(settings.AllowedApplicationsSerialized));
        LoadInstalledApplications();
    }

    public async Task AddRecommendedApplicationsForPathAsync(string path, CancellationToken cancellationToken = default)
    {
        var recommendations = GetRecommendedApplicationsForPath(path)
            .Concat(GetWindowsDefaultApplicationForPath(path))
            .GroupBy(x => x.ProcessName, StringComparer.OrdinalIgnoreCase)
            .Select(x => x.First())
            .ToList();
        if (recommendations.Count == 0)
        {
            return;
        }

        var changed = false;
        foreach (var recommendation in recommendations)
        {
            var existing = AllowedApplicationRules.FirstOrDefault(x =>
                string.Equals(x.ProcessName, recommendation.ProcessName, StringComparison.OrdinalIgnoreCase));
            if (existing is not null)
            {
                if (!existing.IsEnabled)
                {
                    existing.IsEnabled = true;
                    changed = true;
                }

                continue;
            }

            AllowedApplicationRules.Add(CreateRule(
                recommendation.ProcessName,
                TryFindExecutablePath(recommendation.ProcessName),
                isEnabled: true,
                recommendation.DisplayName));
            changed = true;
        }

        if (!changed)
        {
            return;
        }

        AllowedApplicationsText = string.Join(';', AllowedApplicationRules.Where(x => x.IsEnabled).Select(x => x.ProcessName));
        SyncInstalledApplicationAccess();
        await SaveFocusRulesAsync().ConfigureAwait(true);
        _overlayService.ShowMessage($"Focus whitelist updated for {Path.GetFileName(path)}");
    }

    public async Task AddApplicationToWhitelistAsync(string processName, CancellationToken cancellationToken = default)
    {
        processName = NormalizeProcessName(processName);
        if (string.IsNullOrWhiteSpace(processName))
        {
            return;
        }

        var existing = AllowedApplicationRules.FirstOrDefault(x =>
            string.Equals(x.ProcessName, processName, StringComparison.OrdinalIgnoreCase));
        if (existing is null)
        {
            AllowedApplicationRules.Add(CreateRule(processName, TryFindExecutablePath(processName), isEnabled: true));
        }
        else
        {
            existing.IsEnabled = true;
        }

        AllowedApplicationsText = string.Join(';', AllowedApplicationRules.Where(x => x.IsEnabled).Select(x => x.ProcessName));
        SyncInstalledApplicationAccess();
        await SaveFocusRulesAsync().ConfigureAwait(true);
        _overlayService.ShowMessage($"{processName} added to Focus whitelist.");
    }

    private async Task StartAsync()
    {
        await SaveFocusRulesAsync().ConfigureAwait(true);
        await _focusModeService.StartAsync(FocusModeType.Strict).ConfigureAwait(true);
        _overlayService.ShowMessage("Focus mode is active. Whitelist rules are on.");
    }

    private async Task PauseAsync()
    {
        await _focusModeService.PauseAsync().ConfigureAwait(true);
    }

    private async Task StopAsync()
    {
        await _focusModeService.StopAsync().ConfigureAwait(true);
        SessionDurationText = "00:00:00";
        DistractionLog.Clear();
        _overlayService.ShowMessage("Focus mode stopped.");
    }

    private void OnFocusStateChanged()
    {
        // Must marshal to UI thread implicitly using standard async command flow or directly set property from UI Context
        App.Current.Dispatcher.Invoke(() =>
        {
            IsActive = _focusModeService.IsActive;
            FocusStatusText = IsActive ? "Focus mode active - strict whitelist" : "Focus mode is off";
            UpdateTimerText();
        });
    }

    private void OnDistractionDetected(string appName)
    {
        App.Current.Dispatcher.Invoke(() =>
        {
            var msg = $"{DateTime.Now:HH:mm:ss} - Blocked during focus: {appName}";
            if (!DistractionLog.Contains(msg))
            {
                DistractionLog.Add(msg);
            }

            _overlayService.ShowMessage($"Focus warning: {appName}");
            FocusWarningRequested?.Invoke(this, appName);
        });
    }

    private async Task SaveFocusRulesAsync()
    {
        var settings = await _settingsService.GetAsync().ConfigureAwait(true);
        settings.DefaultFocusMode = FocusModeType.Strict;
        settings.AllowedApplicationsSerialized = NormalizeList(string.Join(';', AllowedApplicationRules.Where(x => x.IsEnabled).Select(x => x.ProcessName)));
        settings.FocusDistractingApplicationsSerialized = string.Empty;
        await _settingsService.SaveAsync(settings).ConfigureAwait(true);
        ApplyAllowedApplications(settings.AllowedApplicationsSerialized);
    }

    private Task AddActiveAppAsync()
    {
        var active = _foregroundWindowService.GetCurrent();
        if (string.IsNullOrWhiteSpace(active.ProcessName))
        {
            SelectedApplicationDetails = "No active application could be detected right now.";
            return Task.CompletedTask;
        }

        var existing = AllowedApplicationRules.FirstOrDefault(x =>
            string.Equals(x.ProcessName, active.ProcessName, StringComparison.OrdinalIgnoreCase));
        if (existing is null)
        {
            AllowedApplicationRules.Add(CreateRule(active.ProcessName, active.ExecutablePath, isEnabled: true));
        }
        else
        {
            existing.IsEnabled = true;
            if (string.IsNullOrWhiteSpace(existing.ExecutablePath))
            {
                existing.ExecutablePath = active.ExecutablePath;
            }
        }

        AllowedApplicationsText = string.Join(';', AllowedApplicationRules.Where(x => x.IsEnabled).Select(x => x.ProcessName));
        SyncInstalledApplicationAccess();
        ShowApplicationDetails(existing ?? AllowedApplicationRules.LastOrDefault());
        return Task.CompletedTask;
    }

    private void AddManualApplication()
    {
        var processName = ManualApplicationName.Trim();
        if (string.IsNullOrWhiteSpace(processName))
        {
            return;
        }

        if (processName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            processName = Path.GetFileNameWithoutExtension(processName);
        }

        var existing = AllowedApplicationRules.FirstOrDefault(x =>
            string.Equals(x.ProcessName, processName, StringComparison.OrdinalIgnoreCase));
        if (existing is null)
        {
            AllowedApplicationRules.Add(CreateRule(processName, TryFindExecutablePath(processName), isEnabled: true));
        }
        else
        {
            existing.IsEnabled = true;
        }

        ManualApplicationName = string.Empty;
        AllowedApplicationsText = string.Join(';', AllowedApplicationRules.Where(x => x.IsEnabled).Select(x => x.ProcessName));
        SyncInstalledApplicationAccess();
        ShowApplicationDetails(existing ?? AllowedApplicationRules.LastOrDefault());
    }

    private void RemoveApplication(FocusApplicationRule? rule)
    {
        if (rule is null)
        {
            return;
        }

        AllowedApplicationRules.Remove(rule);
        AllowedApplicationsText = string.Join(';', AllowedApplicationRules.Where(x => x.IsEnabled).Select(x => x.ProcessName));
        SyncInstalledApplicationAccess();
    }

    private void AddInstalledApplication(InstalledApplicationViewModel? application)
    {
        if (application is null || string.IsNullOrWhiteSpace(application.ProcessName))
        {
            return;
        }

        var existing = AllowedApplicationRules.FirstOrDefault(x =>
            string.Equals(x.ProcessName, application.ProcessName, StringComparison.OrdinalIgnoreCase));
        if (existing is null)
        {
            AllowedApplicationRules.Add(CreateRule(application.ProcessName, application.ExecutablePath, isEnabled: true, application.DisplayName));
        }
        else
        {
            existing.IsEnabled = true;
        }

        application.IsAllowed = true;
        AllowedApplicationsText = string.Join(';', AllowedApplicationRules.Where(x => x.IsEnabled).Select(x => x.ProcessName));
        ShowApplicationDetails(existing ?? AllowedApplicationRules.LastOrDefault());
    }

    private void RemoveInstalledApplication(InstalledApplicationViewModel? application)
    {
        if (application is null)
        {
            return;
        }

        var existing = AllowedApplicationRules.FirstOrDefault(x =>
            string.Equals(x.ProcessName, application.ProcessName, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            AllowedApplicationRules.Remove(existing);
        }

        application.IsAllowed = false;
        AllowedApplicationsText = string.Join(';', AllowedApplicationRules.Where(x => x.IsEnabled).Select(x => x.ProcessName));
    }

    private void ShowApplicationDetails(FocusApplicationRule? rule)
    {
        if (rule is null)
        {
            return;
        }

        var path = string.IsNullOrWhiteSpace(rule.ExecutablePath)
            ? TryFindExecutablePath(rule.ProcessName)
            : rule.ExecutablePath;
        if (!string.IsNullOrWhiteSpace(path))
        {
            rule.ExecutablePath = path;
        }

        SelectedApplicationDetails = string.IsNullOrWhiteSpace(path)
            ? $"{rule.DisplayName}\nProcess: {rule.ProcessName}\nLocation: not running now or Windows denied access."
            : $"{rule.DisplayName}\nProcess: {rule.ProcessName}\nLocation: {path}";
    }

    private void UpdateTimerText()
    {
        var duration = _focusModeService.SessionDuration;
        SessionDurationText = duration.ToString(@"hh\:mm\:ss");
    }

    public void Dispose()
    {
        _focusModeService.StateChanged -= OnFocusStateChanged;
        _focusModeService.DistractionDetected -= OnDistractionDetected;
        _timer.Stop();
    }

    private static IReadOnlyList<string> ParseList(string value)
    {
        return value
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string NormalizeList(string value)
    {
        return string.Join(';', ParseList(value));
    }

    private static string MergeWithDefaultAllowedApplications(string value)
    {
        return NormalizeList(string.Join(';', ParseList(FocusDefaults.AllowedApplicationsText).Concat(ParseList(value))));
    }

    private static void SyncList(ObservableCollection<string> target, IEnumerable<string> source)
    {
        target.Clear();
        foreach (var item in source)
        {
            target.Add(item);
        }
    }

    private void ApplyAllowedApplications(string value)
    {
        AllowedApplicationsText = NormalizeList(value);
        SyncRules(AllowedApplicationRules, ParseList(AllowedApplicationsText), enabledByDefault: true);
        SyncList(AllowedApplications, AllowedApplicationRules.Where(x => x.IsEnabled).Select(x => x.ProcessName));
        SyncInstalledApplicationAccess();
    }

    private static void SyncRules(ObservableCollection<FocusApplicationRule> target, IEnumerable<string> processNames, bool enabledByDefault)
    {
        target.Clear();
        foreach (var processName in processNames)
        {
            target.Add(CreateRule(processName, TryFindExecutablePath(processName), enabledByDefault));
        }
    }

    private static FocusApplicationRule CreateRule(string processName, string executablePath, bool isEnabled, string? displayName = null)
    {
        return new FocusApplicationRule
        {
            ProcessName = processName,
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? processName : displayName,
            ExecutablePath = executablePath,
            IsEnabled = isEnabled,
            IconSource = TryLoadIcon(executablePath)
        };
    }

    private static string TryFindExecutablePath(string processName)
    {
        try
        {
            return Process.GetProcessesByName(processName)
                .Select(process =>
                {
                    using (process)
                    {
                        try
                        {
                            return process.MainModule?.FileName ?? string.Empty;
                        }
                        catch
                        {
                            return string.Empty;
                        }
                    }
                })
                .FirstOrDefault(path => !string.IsNullOrWhiteSpace(path)) ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static ImageSource? TryLoadIcon(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return null;
        }

        try
        {
            using var icon = Icon.ExtractAssociatedIcon(path);
            if (icon is null)
            {
                return null;
            }

            var source = Imaging.CreateBitmapSourceFromHIcon(
                icon.Handle,
                System.Windows.Int32Rect.Empty,
                BitmapSizeOptions.FromWidthAndHeight(24, 24));
            source.Freeze();
            return source;
        }
        catch
        {
            return null;
        }
    }

    private void LoadInstalledApplications()
    {
        var apps = GetInstalledApplications()
            .Where(app => !IsNoiseApplication(app))
            .GroupBy(x => x.ProcessName, StringComparer.OrdinalIgnoreCase)
            .Select(group => group
                .OrderByDescending(x => x.SourceRank)
                .ThenByDescending(x => !string.IsNullOrWhiteSpace(x.ExecutablePath))
                .First())
            .OrderByDescending(x => x.IsSuggestedWorkApp)
            .ThenBy(x => x.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        InstalledApplications.Clear();
        foreach (var app in apps)
        {
            InstalledApplications.Add(app);
        }

        SyncInstalledApplicationAccess();
        ApplyInstalledApplicationFilter();
    }

    private void SyncInstalledApplicationAccess()
    {
        var allowed = AllowedApplicationRules
            .Where(x => x.IsEnabled)
            .Select(x => x.ProcessName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var app in InstalledApplications)
        {
            app.IsAllowed = allowed.Contains(app.ProcessName);
        }
    }

    private void ApplyInstalledApplicationFilter()
    {
        var query = ApplicationSearchText.Trim();
        var source = string.IsNullOrWhiteSpace(query)
            ? InstalledApplications
            : InstalledApplications.Where(app =>
                app.DisplayName.Contains(query, StringComparison.CurrentCultureIgnoreCase) ||
                app.Publisher.Contains(query, StringComparison.CurrentCultureIgnoreCase) ||
                app.ProcessName.Contains(query, StringComparison.OrdinalIgnoreCase));

        FilteredInstalledApplications.Clear();
        foreach (var app in source.Take(160))
        {
            FilteredInstalledApplications.Add(app);
        }
    }

    private static IEnumerable<InstalledApplicationViewModel> GetInstalledApplications()
    {
        foreach (var app in GetStartMenuApplications())
        {
            yield return app;
        }

        foreach (var app in GetKnownProgramFilesApplications())
        {
            yield return app;
        }

        RegistryView[] registryViews = Environment.Is64BitOperatingSystem
            ? [RegistryView.Registry64, RegistryView.Registry32]
            : [RegistryView.Registry32];

        foreach (var view in registryViews)
        {
            foreach (var app in GetInstalledApplications(RegistryHive.LocalMachine, view))
            {
                yield return app;
            }

            foreach (var app in GetInstalledApplications(RegistryHive.CurrentUser, view))
            {
                yield return app;
            }
        }
    }

    private static IEnumerable<InstalledApplicationViewModel> GetStartMenuApplications()
    {
        var startMenuFolders = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu),
            Environment.GetFolderPath(Environment.SpecialFolder.StartMenu)
        };

        foreach (var folder in startMenuFolders.Where(Directory.Exists).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            IEnumerable<string> shortcuts;
            try
            {
                shortcuts = Directory.EnumerateFiles(folder, "*.lnk", SearchOption.AllDirectories).ToList();
            }
            catch
            {
                continue;
            }

            foreach (var shortcutPath in shortcuts)
            {
                var targetPath = TryResolveShortcutTarget(shortcutPath);
                if (string.IsNullOrWhiteSpace(targetPath) || !File.Exists(targetPath))
                {
                    continue;
                }

                var displayName = Path.GetFileNameWithoutExtension(shortcutPath);
                if (string.IsNullOrWhiteSpace(displayName) || IsNoiseName(displayName))
                {
                    continue;
                }

                yield return new InstalledApplicationViewModel
                {
                    DisplayName = displayName,
                    ProcessName = Path.GetFileNameWithoutExtension(targetPath),
                    Publisher = "Start Menu",
                    Version = string.Empty,
                    ExecutablePath = targetPath,
                    IconSource = TryLoadIcon(targetPath),
                    SourceLabel = "Shortcut",
                    SourceRank = 3
                };
            }
        }
    }

    private static IEnumerable<InstalledApplicationViewModel> GetKnownProgramFilesApplications()
    {
        var roots = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs")
        };

        var knownNames = new[]
        {
            "blender", "blockbench", "aseprite", "davinci", "resolve", "freecad", "figma",
            "dbeaver", "code", "cursor", "rider", "idea", "pycharm", "webstorm", "clion",
            "godot", "unity", "unreal", "krita", "inkscape", "gimp"
        };

        foreach (var root in roots.Where(Directory.Exists).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            IEnumerable<string> executablePaths;
            try
            {
                executablePaths = Directory.EnumerateFiles(root, "*.exe", SearchOption.AllDirectories)
                    .Where(path =>
                    {
                        var name = Path.GetFileNameWithoutExtension(path);
                        return knownNames.Any(known => name.Contains(known, StringComparison.OrdinalIgnoreCase) ||
                                                       path.Contains(known, StringComparison.OrdinalIgnoreCase));
                    })
                    .Take(120)
                    .ToList();
            }
            catch
            {
                continue;
            }

            foreach (var executablePath in executablePaths)
            {
                var name = Path.GetFileNameWithoutExtension(executablePath);
                if (string.IsNullOrWhiteSpace(name) || IsNoiseName(name))
                {
                    continue;
                }

                yield return new InstalledApplicationViewModel
                {
                    DisplayName = MakeDisplayName(name),
                    ProcessName = name,
                    Publisher = "Program Files",
                    Version = string.Empty,
                    ExecutablePath = executablePath,
                    IconSource = TryLoadIcon(executablePath),
                    SourceLabel = "Program Files",
                    SourceRank = 2
                };
            }
        }
    }

    private static IEnumerable<InstalledApplicationViewModel> GetInstalledApplications(RegistryHive hive, RegistryView view)
    {
        const string uninstallPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";

        using var baseKey = RegistryKey.OpenBaseKey(hive, view);
        using var uninstallKey = baseKey.OpenSubKey(uninstallPath);
        if (uninstallKey is null)
        {
            yield break;
        }

        foreach (var subKeyName in uninstallKey.GetSubKeyNames())
        {
            using var appKey = uninstallKey.OpenSubKey(subKeyName);
            if (appKey is null)
            {
                continue;
            }

            var displayName = (appKey.GetValue("DisplayName") as string)?.Trim();
            if (string.IsNullOrWhiteSpace(displayName))
            {
                continue;
            }

            if (IsNoiseName(displayName))
            {
                continue;
            }

            var displayIcon = CleanExecutablePath(appKey.GetValue("DisplayIcon") as string);
            var installLocation = (appKey.GetValue("InstallLocation") as string)?.Trim('"', ' ');
            var executablePath = ResolveExecutablePath(displayIcon, installLocation);
            var processName = string.IsNullOrWhiteSpace(executablePath)
                ? NormalizeProcessName(displayName)
                : Path.GetFileNameWithoutExtension(executablePath);

            if (string.IsNullOrWhiteSpace(processName))
            {
                continue;
            }

            yield return new InstalledApplicationViewModel
            {
                DisplayName = displayName,
                ProcessName = processName,
                Publisher = (appKey.GetValue("Publisher") as string)?.Trim() ?? string.Empty,
                Version = (appKey.GetValue("DisplayVersion") as string)?.Trim() ?? string.Empty,
                ExecutablePath = executablePath,
                IconSource = TryLoadIcon(executablePath),
                SourceLabel = "Installed",
                SourceRank = 1
            };
        }
    }

    private static string TryResolveShortcutTarget(string shortcutPath)
    {
        try
        {
            var shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType is null)
            {
                return string.Empty;
            }

            var shell = Activator.CreateInstance(shellType);
            if (shell is null)
            {
                return string.Empty;
            }

            var shortcut = shellType.InvokeMember("CreateShortcut", System.Reflection.BindingFlags.InvokeMethod, null, shell, [shortcutPath]);
            var targetPath = shortcut?.GetType().InvokeMember("TargetPath", System.Reflection.BindingFlags.GetProperty, null, shortcut, null) as string;
            return CleanExecutablePath(targetPath);
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string ResolveExecutablePath(string displayIcon, string? installLocation)
    {
        if (!string.IsNullOrWhiteSpace(displayIcon) && File.Exists(displayIcon))
        {
            return displayIcon;
        }

        if (!string.IsNullOrWhiteSpace(installLocation) && Directory.Exists(installLocation))
        {
            try
            {
                return Directory
                    .EnumerateFiles(installLocation, "*.exe", SearchOption.TopDirectoryOnly)
                    .OrderBy(path => Path.GetFileName(path).Length)
                    .FirstOrDefault() ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        return string.Empty;
    }

    private static string CleanExecutablePath(string? rawPath)
    {
        if (string.IsNullOrWhiteSpace(rawPath))
        {
            return string.Empty;
        }

        var value = rawPath.Trim().Trim('"');
        var exeIndex = value.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
        if (exeIndex >= 0)
        {
            return value[..(exeIndex + 4)].Trim('"', ' ');
        }

        return value.Split(',')[0].Trim('"', ' ');
    }

    private static string NormalizeProcessName(string displayName)
    {
        var cleaned = new string(displayName
            .TakeWhile(ch => ch != '(')
            .Where(ch => char.IsLetterOrDigit(ch) || ch is ' ' or '-' or '_' or '.')
            .ToArray())
            .Trim();

        return cleaned.Replace(" ", string.Empty, StringComparison.Ordinal);
    }

    private static IReadOnlyList<RecommendedApplication> GetRecommendedApplicationsForPath(string path)
    {
        var extension = Path.GetExtension(path).ToLowerInvariant();
        return extension switch
        {
            ".cs" or ".xaml" or ".csproj" or ".sln" =>
                IdeRecommendations(["devenv", "Code", "Cursor", "rider64"]),

            ".cpp" or ".c" or ".h" or ".hpp" or ".ino" =>
                IdeRecommendations(["Code", "Cursor", "devenv", "clion64", "notepad++"]),

            ".py" =>
                IdeRecommendations(["Code", "Cursor", "pycharm64", "notepad++"]),

            ".js" or ".jsx" or ".ts" or ".tsx" or ".html" or ".css" or ".scss" or ".vue" or ".svelte" =>
                IdeRecommendations(["Code", "Cursor", "webstorm64", "notepad++"]),

            ".java" or ".kt" or ".kts" =>
                IdeRecommendations(["idea64", "Code", "Cursor", "eclipse"]),

            ".php" =>
                IdeRecommendations(["phpstorm64", "Code", "Cursor", "notepad++"]),

            ".go" or ".rs" or ".json" or ".xml" or ".yml" or ".yaml" or ".md" =>
                IdeRecommendations(["Code", "Cursor", "notepad++"]),

            ".blend" =>
                [new("blender", "Blender")],

            ".bbmodel" or ".bbentity" =>
                [new("Blockbench", "Blockbench")],

            ".fcstd" =>
                [new("FreeCAD", "FreeCAD")],

            ".psd" or ".ai" or ".svg" or ".kra" or ".xcf" or ".png" or ".jpg" or ".jpeg" =>
                IdeRecommendations(["Photoshop", "Illustrator", "figma", "inkscape", "gimp", "krita"]),

            ".doc" or ".docx" or ".rtf" =>
                IdeRecommendations(["WINWORD", "notepad++"]),

            ".xls" or ".xlsx" or ".csv" =>
                IdeRecommendations(["EXCEL", "Code", "Cursor"]),

            _ => []
        };
    }

    private static IEnumerable<RecommendedApplication> GetWindowsDefaultApplicationForPath(string path)
    {
        var extension = Path.GetExtension(path);
        if (string.IsNullOrWhiteSpace(extension))
        {
            yield break;
        }

        var executablePath = TryGetAssociatedExecutable(extension);
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            yield break;
        }

        var processName = Path.GetFileNameWithoutExtension(executablePath);
        if (string.IsNullOrWhiteSpace(processName) || IsNoiseName(processName))
        {
            yield break;
        }

        yield return new RecommendedApplication(processName, MakeDisplayName(processName));
    }

    private static string TryGetAssociatedExecutable(string extension)
    {
        try
        {
            var length = 0u;
            _ = AssocQueryString(AssocF.None, AssocStr.Executable, extension, null, null, ref length);
            if (length == 0)
            {
                return string.Empty;
            }

            var builder = new System.Text.StringBuilder((int)length);
            var result = AssocQueryString(AssocF.None, AssocStr.Executable, extension, null, builder, ref length);
            return result == 0 ? builder.ToString() : string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static RecommendedApplication[] IdeRecommendations(IEnumerable<string> processNames)
    {
        return processNames
            .Select(processName => new RecommendedApplication(processName, processName))
            .ToArray();
    }

    private static bool IsNoiseApplication(InstalledApplicationViewModel app)
    {
        return IsNoiseName(app.DisplayName) || IsNoiseName(app.ProcessName);
    }

    private static bool IsNoiseName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        var noise = new[]
        {
            "runtime", "redistributable", "visual c++", "microsoft visual c", "sdk",
            "driver", "update", "hotfix", "security update", "language pack", "help",
            "documentation", "common components", "component", "shared", "framework",
            "webview2", "edge update", "vulkan runtime", "openal", "directx",
            "windows software development kit"
        };

        return noise.Any(item => value.Contains(item, StringComparison.OrdinalIgnoreCase));
    }

    private static string MakeDisplayName(string processName)
    {
        return processName switch
        {
            var x when x.Equals("blender", StringComparison.OrdinalIgnoreCase) => "Blender",
            var x when x.Equals("Blockbench", StringComparison.OrdinalIgnoreCase) => "Blockbench",
            var x when x.Equals("Code", StringComparison.OrdinalIgnoreCase) => "Visual Studio Code",
            var x when x.Equals("chrome", StringComparison.OrdinalIgnoreCase) => "Google Chrome",
            var x when x.Equals("msedge", StringComparison.OrdinalIgnoreCase) => "Microsoft Edge",
            var x when x.Equals("Cursor", StringComparison.OrdinalIgnoreCase) => "Cursor",
            _ => processName
        };
    }

    [DllImport("Shlwapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint AssocQueryString(
        AssocF flags,
        AssocStr str,
        string pszAssoc,
        string? pszExtra,
        System.Text.StringBuilder? pszOut,
        ref uint pcchOut);

    private enum AssocF
    {
        None = 0
    }

    private enum AssocStr
    {
        Executable = 2
    }
}

file static class FocusDefaults
{
    public const string AllowedApplicationsText =
        "ReachIT;explorer;SearchHost;ShellExperienceHost;StartMenuExperienceHost;ApplicationFrameHost;Code;Cursor;devenv;rider64;idea64;pycharm64;webstorm64;clion64;datagrip64;phpstorm64;eclipse;notepad;notepad++;Notepad;WINWORD;EXCEL;POWERPNT;OUTLOOK;OneNote;Acrobat;FoxitPDFEditor;chrome;msedge;firefox;brave;steam;steamwebhelper;GameOverlayUI;FreeCAD;blender;Blockbench;Aseprite;Resolve;fusion360;acad;SketchUp;3dsmax;Maya;Photoshop;Illustrator;figma;inkscape;gimp;paintdotnet;PaintStudio.View;WindowsTerminal;wt;powershell;cmd;git-bash;putty;winscp;postman;insomnia;docker desktop;Docker Desktop;slack;Teams;Zoom";
}

public sealed record RecommendedApplication(string ProcessName, string DisplayName);

public sealed class FocusApplicationRule : ViewModelBase
{
    private bool _isEnabled = true;
    private string _executablePath = string.Empty;

    public bool IsEnabled
    {
        get => _isEnabled;
        set => SetProperty(ref _isEnabled, value);
    }

    public string ProcessName { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;

    public string ExecutablePath
    {
        get => _executablePath;
        set => SetProperty(ref _executablePath, value);
    }

    public ImageSource? IconSource { get; init; }
}

public sealed class InstalledApplicationViewModel : ViewModelBase
{
    private bool _isAllowed;

    public string DisplayName { get; init; } = string.Empty;
    public string ProcessName { get; init; } = string.Empty;
    public string Publisher { get; init; } = string.Empty;
    public string Version { get; init; } = string.Empty;
    public string ExecutablePath { get; init; } = string.Empty;
    public string SourceLabel { get; init; } = string.Empty;
    public int SourceRank { get; init; }
    public ImageSource? IconSource { get; init; }

    public bool IsAllowed
    {
        get => _isAllowed;
        set
        {
            if (SetProperty(ref _isAllowed, value))
            {
                OnPropertyChanged(nameof(IsNotAllowed));
            }
        }
    }

    public bool IsNotAllowed => !IsAllowed;

    public bool IsSuggestedWorkApp
    {
        get
        {
            var combined = $"{DisplayName} {ProcessName}";
            var suggested = new[]
            {
                "blender", "blockbench", "aseprite", "davinci", "resolve", "freecad",
                "code", "cursor", "visual studio", "rider", "pycharm", "webstorm",
                "dbeaver", "figma", "krita", "inkscape", "gimp", "godot", "unity"
            };

            return suggested.Any(item => combined.Contains(item, StringComparison.OrdinalIgnoreCase));
        }
    }
}
