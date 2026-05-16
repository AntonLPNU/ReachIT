// Exposes editable app settings placeholders.
using System.Windows.Input;
using ReachIT.Application.Contracts;
using ReachIT.Application;
using ReachIT.Domain.Models;
using ReachIT.Presentation.Commands;
using ReachIT.Presentation.Services;
using Microsoft.EntityFrameworkCore;
using ReachIT.Domain.Enums;
using System.Linq;

namespace ReachIT.Presentation.ViewModels;

public sealed class SettingsViewModel : ViewModelBase
{
    private readonly IDatabaseService _databaseService;
    private readonly IProjectService _projectService;
    private readonly IActivityMonitorService _activityMonitorService;
    private readonly IAccountService _accountService;
    private readonly IEntitlementService _entitlementService;
    private readonly IDeveloperProjectGeneratorService _developerProjectGeneratorService;

    private bool _showRelatedTasksInTree;
    private bool _hideSidePanelAfterExternalFileOpen = true;
    private string _theme = "Light";
    private string _language = "en";
    private bool _enableNotifications = true;
    private string _sidePanelHotkey = "Ctrl+Shift+R";
    private FocusModeType _defaultFocusMode = FocusModeType.Strict;
    private string _backupLocationPath = string.Empty;
    private string _allowedApplications = "ReachIT;explorer;SearchHost;ShellExperienceHost;StartMenuExperienceHost;ApplicationFrameHost;Code;Cursor;devenv;rider64;idea64;pycharm64;webstorm64;clion64;datagrip64;phpstorm64;eclipse;notepad;notepad++;Notepad;WINWORD;EXCEL;POWERPNT;OUTLOOK;OneNote;Acrobat;FoxitPDFEditor;chrome;msedge;firefox;brave;FreeCAD;blender;Blockbench;Aseprite;Resolve;fusion360;acad;SketchUp;3dsmax;Maya;Photoshop;Illustrator;figma;inkscape;gimp;paintdotnet;PaintStudio.View;WindowsTerminal;wt;powershell;cmd;SnippingTool;ScreenClippingHost;Snipaste;ShareX;Lightshot;Greenshot;git-bash;putty;winscp;postman;insomnia;docker desktop;Docker Desktop;Spotify;Music.UI;iTunes;slack;Teams;Zoom";
    private string _focusDistractingApplications = string.Empty;
    private bool _enableActivityTracking = true;
    private bool _trackActiveWindow = true;
    private bool _trackFileChanges = true;
    private bool _trackGitChanges = true;
    private bool _trackTextStatistics = true;
    private bool _ignorePrivateApps = true;
    private bool _pauseActivityTracking;
    private string _ignoredFolders = "bin;obj;.git;.vs;node_modules;packages;build;dist";
    private bool _useProjectActivitySettings;
    private bool _projectEnableActivityTracking = true;
    private bool _projectTrackActiveWindow = true;
    private bool _projectTrackFileChanges = true;
    private bool _projectTrackGitChanges = true;
    private bool _projectTrackTextStatistics = true;
    private bool _projectPauseActivityTracking;
    private string _projectIgnoredFolders = "bin;obj;.git;.vs;node_modules;packages;build;dist";
    private string _currentProjectName = "No project opened";
    private string _accountDisplayName = "Local User";
    private string _accountEmail = string.Empty;
    private string _developerLogin = string.Empty;
    private string _developerPassword = string.Empty;
    private SubscriptionPlanType _subscriptionPlan = SubscriptionPlanType.Free;
    private string _subscriptionStatusText = "Free local account";
    private string _accountFeatureSummary = string.Empty;
    private string _developerToolStatus = string.Empty;
    private bool _isDeveloperAccount;
    private AppSettings? _model;

    public SettingsViewModel(
        IDatabaseService databaseService,
        IProjectService projectService,
        IActivityMonitorService activityMonitorService,
        IAccountService accountService,
        IEntitlementService entitlementService,
        IDeveloperProjectGeneratorService developerProjectGeneratorService)
    {
        _databaseService = databaseService;
        _projectService = projectService;
        _activityMonitorService = activityMonitorService;
        _accountService = accountService;
        _entitlementService = entitlementService;
        _developerProjectGeneratorService = developerProjectGeneratorService;
        SaveCommand = new AsyncCommand(_ => SaveSettingsAsync());
        GenerateDeveloperProjectCommand = new AsyncCommand(_ => GenerateDeveloperProjectAsync(), _ => IsDeveloperAccount);
    }

    public ICommand SaveCommand { get; }
    public ICommand GenerateDeveloperProjectCommand { get; }
    public bool IsDeveloperToolsAvailable => DeveloperTools.IsEnabled;

    public IReadOnlyList<string> AvailableThemes { get; } = ["Light", "Dark", "System"];

    public IReadOnlyList<string> AvailableLanguages { get; } = ["en", "uk"];

    public IReadOnlyList<SubscriptionPlanType> AvailableSubscriptionPlans { get; } =
    [
        SubscriptionPlanType.Free,
        SubscriptionPlanType.Pro,
        SubscriptionPlanType.Team
    ];

    public bool ShowRelatedTasksInTree
    {
        get => _showRelatedTasksInTree;
        set => SetProperty(ref _showRelatedTasksInTree, value);
    }

    public bool HideSidePanelAfterExternalFileOpen
    {
        get => _hideSidePanelAfterExternalFileOpen;
        set => SetProperty(ref _hideSidePanelAfterExternalFileOpen, value);
    }

    public string Theme
    {
        get => _theme;
        set => SetProperty(ref _theme, value);
    }

    public Task SaveAsync()
    {
        return SaveSettingsAsync();
    }

    public string Language
    {
        get => _language;
        set => SetProperty(ref _language, value);
    }

    public bool EnableNotifications
    {
        get => _enableNotifications;
        set => SetProperty(ref _enableNotifications, value);
    }

    public string SidePanelHotkey
    {
        get => _sidePanelHotkey;
        set => SetProperty(ref _sidePanelHotkey, value);
    }

    public FocusModeType DefaultFocusMode
    {
        get => _defaultFocusMode;
        set => SetProperty(ref _defaultFocusMode, value);
    }

    public string BackupLocationPath
    {
        get => _backupLocationPath;
        set => SetProperty(ref _backupLocationPath, value);
    }

    public string AppDataLocation => _databaseService.DatabasePath;

    public bool EnableActivityTracking
    {
        get => _enableActivityTracking;
        set => SetProperty(ref _enableActivityTracking, value);
    }

    public string AllowedApplications
    {
        get => _allowedApplications;
        set => SetProperty(ref _allowedApplications, value);
    }

    public string FocusDistractingApplications
    {
        get => _focusDistractingApplications;
        set => SetProperty(ref _focusDistractingApplications, value);
    }

    public bool TrackActiveWindow
    {
        get => _trackActiveWindow;
        set => SetProperty(ref _trackActiveWindow, value);
    }

    public bool TrackFileChanges
    {
        get => _trackFileChanges;
        set => SetProperty(ref _trackFileChanges, value);
    }

    public bool TrackGitChanges
    {
        get => _trackGitChanges;
        set => SetProperty(ref _trackGitChanges, value);
    }

    public bool TrackTextStatistics
    {
        get => _trackTextStatistics;
        set => SetProperty(ref _trackTextStatistics, value);
    }

    public bool IgnorePrivateApps
    {
        get => _ignorePrivateApps;
        set => SetProperty(ref _ignorePrivateApps, value);
    }

    public bool PauseActivityTracking
    {
        get => _pauseActivityTracking;
        set => SetProperty(ref _pauseActivityTracking, value);
    }

    public string IgnoredFolders
    {
        get => _ignoredFolders;
        set => SetProperty(ref _ignoredFolders, value);
    }

    public string CurrentProjectName
    {
        get => _currentProjectName;
        private set => SetProperty(ref _currentProjectName, value);
    }

    public string AccountDisplayName
    {
        get => _accountDisplayName;
        set => SetProperty(ref _accountDisplayName, value);
    }

    public string AccountEmail
    {
        get => _accountEmail;
        set => SetProperty(ref _accountEmail, value);
    }

    public string DeveloperLogin
    {
        get => _developerLogin;
        private set => SetProperty(ref _developerLogin, value);
    }

    public string DeveloperPassword
    {
        get => _developerPassword;
        private set => SetProperty(ref _developerPassword, value);
    }

    public bool IsDeveloperAccount
    {
        get => _isDeveloperAccount;
        private set
        {
            if (SetProperty(ref _isDeveloperAccount, value) && GenerateDeveloperProjectCommand is AsyncCommand command)
            {
                command.RaiseCanExecuteChanged();
                OnPropertyChanged(nameof(IsDeveloperPanelVisible));
            }
        }
    }

    public bool IsDeveloperPanelVisible => DeveloperTools.IsEnabled && IsDeveloperAccount;

    public string DeveloperToolStatus
    {
        get => _developerToolStatus;
        private set => SetProperty(ref _developerToolStatus, value);
    }

    public SubscriptionPlanType SubscriptionPlan
    {
        get => _subscriptionPlan;
        set => SetProperty(ref _subscriptionPlan, value);
    }

    public string SubscriptionStatusText
    {
        get => _subscriptionStatusText;
        private set => SetProperty(ref _subscriptionStatusText, value);
    }

    public string AccountFeatureSummary
    {
        get => _accountFeatureSummary;
        private set => SetProperty(ref _accountFeatureSummary, value);
    }

    public bool HasCurrentProject => _projectService.CurrentProject is not null;

    public bool UseProjectActivitySettings
    {
        get => _useProjectActivitySettings;
        set => SetProperty(ref _useProjectActivitySettings, value);
    }

    public bool ProjectEnableActivityTracking
    {
        get => _projectEnableActivityTracking;
        set => SetProperty(ref _projectEnableActivityTracking, value);
    }

    public bool ProjectTrackActiveWindow
    {
        get => _projectTrackActiveWindow;
        set => SetProperty(ref _projectTrackActiveWindow, value);
    }

    public bool ProjectTrackFileChanges
    {
        get => _projectTrackFileChanges;
        set => SetProperty(ref _projectTrackFileChanges, value);
    }

    public bool ProjectTrackGitChanges
    {
        get => _projectTrackGitChanges;
        set => SetProperty(ref _projectTrackGitChanges, value);
    }

    public bool ProjectTrackTextStatistics
    {
        get => _projectTrackTextStatistics;
        set => SetProperty(ref _projectTrackTextStatistics, value);
    }

    public bool ProjectPauseActivityTracking
    {
        get => _projectPauseActivityTracking;
        set => SetProperty(ref _projectPauseActivityTracking, value);
    }

    public string ProjectIgnoredFolders
    {
        get => _projectIgnoredFolders;
        set => SetProperty(ref _projectIgnoredFolders, value);
    }

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        using var db = _databaseService.CreateDbContext();
        _model = await db.AppSettings.FirstOrDefaultAsync(cancellationToken).ConfigureAwait(true);
        if (_model == null)
        {
            _model = new AppSettings();
            db.AppSettings.Add(_model);
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(true);
        }

        ShowRelatedTasksInTree = _model.ShowRelatedTasksInTree;
        HideSidePanelAfterExternalFileOpen = _model.HideSidePanelAfterExternalFileOpen;
        Theme = _model.Theme;
        Language = string.IsNullOrWhiteSpace(_model.Language) ? "en" : _model.Language;
        EnableNotifications = _model.EnableNotifications;
        SidePanelHotkey = _model.SidePanelHotkey;
        DefaultFocusMode = FocusModeType.Strict;
        BackupLocationPath = _model.BackupLocationPath;
        AllowedApplications = string.IsNullOrWhiteSpace(_model.AllowedApplicationsSerialized)
            ? _allowedApplications
            : _model.AllowedApplicationsSerialized;
        FocusDistractingApplications = string.Empty;
        EnableActivityTracking = _model.EnableActivityTracking;
        TrackActiveWindow = _model.TrackActiveWindow;
        TrackFileChanges = _model.TrackFileChanges;
        TrackGitChanges = _model.TrackGitChanges;
        TrackTextStatistics = _model.TrackTextStatistics;
        IgnorePrivateApps = _model.IgnorePrivateApps;
        PauseActivityTracking = _model.PauseActivityTracking;
        IgnoredFolders = _model.IgnoredFoldersSerialized;
        LoadProjectSettings();
        await LoadAccountSettingsAsync(cancellationToken).ConfigureAwait(true);
        OnPropertyChanged(nameof(AppDataLocation));
        OnPropertyChanged(nameof(HasCurrentProject));
    }

    private async Task SaveSettingsAsync()
    {
        if (_model == null) return;

        _model.ShowRelatedTasksInTree = ShowRelatedTasksInTree;
        _model.HideSidePanelAfterExternalFileOpen = HideSidePanelAfterExternalFileOpen;
        _model.Theme = Theme;
        _model.Language = Language;
        _model.EnableNotifications = EnableNotifications;
        _model.SidePanelHotkey = SidePanelHotkey;
        _model.DefaultFocusMode = FocusModeType.Strict;
        _model.BackupLocationPath = BackupLocationPath;
        _model.AllowedApplicationsSerialized = AllowedApplications;
        _model.FocusDistractingApplicationsSerialized = string.Empty;
        _model.EnableActivityTracking = EnableActivityTracking;
        _model.TrackActiveWindow = TrackActiveWindow;
        _model.TrackFileChanges = TrackFileChanges;
        _model.TrackGitChanges = TrackGitChanges;
        _model.TrackTextStatistics = TrackTextStatistics;
        _model.IgnorePrivateApps = IgnorePrivateApps;
        _model.PauseActivityTracking = PauseActivityTracking;
        _model.IgnoredFoldersSerialized = IgnoredFolders;

        SaveProjectSettings();

        using var db = _databaseService.CreateDbContext();
        db.AppSettings.Update(_model);
        await db.SaveChangesAsync().ConfigureAwait(true);
        await SaveAccountSettingsAsync().ConfigureAwait(true);
        await _projectService.SaveProjectAsync().ConfigureAwait(true);
        await _activityMonitorService.ReloadSettingsAsync().ConfigureAwait(true);
        LocalizationService.ApplyLanguage(Language);
    }

    private async Task LoadAccountSettingsAsync(CancellationToken cancellationToken)
    {
        await _accountService.EnsureDeveloperAccountAsync(cancellationToken).ConfigureAwait(true);
        var account = await _accountService.GetAccountStateAsync(cancellationToken).ConfigureAwait(true);
        AccountDisplayName = account.User.DisplayName;
        AccountEmail = account.User.Email;
        SubscriptionPlan = account.Subscription.PlanType;
        IsDeveloperAccount = account.User.IsDeveloperAccount || account.Subscription.PlanType == SubscriptionPlanType.Internal;
        OnPropertyChanged(nameof(IsDeveloperPanelVisible));
        DeveloperLogin = ReachIT.Application.Services.AccountService.DeveloperLogin;
        DeveloperPassword = ReachIT.Application.Services.AccountService.DeveloperPassword;
        DeveloperToolStatus = IsDeveloperAccount
            ? "Developer tools are enabled for this local account."
            : "Developer tools are disabled for this account.";
        SubscriptionStatusText = FormatSubscriptionStatus(account.Subscription);
        AccountFeatureSummary = FormatFeatureSummary(account.Features);
    }

    private async Task SaveAccountSettingsAsync()
    {
        await _accountService.UpdateLocalProfileAsync(AccountDisplayName, AccountEmail).ConfigureAwait(true);
        await _accountService.SetLocalSubscriptionAsync(
            SubscriptionPlan,
            SubscriptionPlan == SubscriptionPlanType.Free ? SubscriptionStatus.Inactive : SubscriptionStatus.Active)
            .ConfigureAwait(true);

        var account = await _accountService.GetAccountStateAsync().ConfigureAwait(true);
        IsDeveloperAccount = account.User.IsDeveloperAccount || account.Subscription.PlanType == SubscriptionPlanType.Internal;
        OnPropertyChanged(nameof(IsDeveloperPanelVisible));
        SubscriptionStatusText = FormatSubscriptionStatus(account.Subscription);
        AccountFeatureSummary = FormatFeatureSummary(account.Features);
    }

    private async Task GenerateDeveloperProjectAsync()
    {
        if (!IsDeveloperAccount)
        {
            DeveloperToolStatus = "Only the developer account can generate demo projects.";
            return;
        }

        DeveloperToolStatus = "Choose a folder for the generated demo project...";
        var project = await _developerProjectGeneratorService.GenerateDemoProjectAsync().ConfigureAwait(true);
        DeveloperToolStatus = project is null
            ? "Demo project generation was cancelled."
            : $"Demo project generated: {project.ProjectName}";
        OnPropertyChanged(nameof(HasCurrentProject));
        LoadProjectSettings();
    }

    private static string FormatSubscriptionStatus(AccountSubscription subscription)
    {
        if (subscription.PlanType == SubscriptionPlanType.Free)
        {
            return "Free local account";
        }

        var period = subscription.CurrentPeriodEndsAt is null
            ? "no period end"
            : $"valid until {subscription.CurrentPeriodEndsAt:yyyy-MM-dd}";

        return $"{subscription.PlanType} - {subscription.Status}, {period}";
    }

    private static string FormatFeatureSummary(IEnumerable<FeatureAccess> features)
    {
        return string.Join(
            Environment.NewLine,
            features.Select(x => $"{(x.IsEnabled ? "ON " : "OFF")} {x.Feature}: {x.Reason}"));
    }

    private void LoadProjectSettings()
    {
        var project = _projectService.CurrentProject;
        if (project is null)
        {
            CurrentProjectName = "No project opened";
            UseProjectActivitySettings = false;
            ProjectEnableActivityTracking = EnableActivityTracking;
            ProjectTrackActiveWindow = TrackActiveWindow;
            ProjectTrackFileChanges = TrackFileChanges;
            ProjectTrackGitChanges = TrackGitChanges;
            ProjectTrackTextStatistics = TrackTextStatistics;
            ProjectPauseActivityTracking = PauseActivityTracking;
            ProjectIgnoredFolders = "bin;obj;.git;.vs;node_modules;packages;build;dist";
            return;
        }

        CurrentProjectName = project.ProjectName;
        UseProjectActivitySettings = project.UseProjectActivitySettings;
        ProjectEnableActivityTracking = project.ProjectEnableActivityTracking;
        ProjectTrackActiveWindow = project.ProjectTrackActiveWindow;
        ProjectTrackFileChanges = project.ProjectTrackFileChanges;
        ProjectTrackGitChanges = project.ProjectTrackGitChanges;
        ProjectTrackTextStatistics = project.ProjectTrackTextStatistics;
        ProjectPauseActivityTracking = project.ProjectPauseActivityTracking;
        ProjectIgnoredFolders = string.IsNullOrWhiteSpace(project.ProjectIgnoredFoldersSerialized)
            ? IgnoredFolders
            : project.ProjectIgnoredFoldersSerialized;
    }

    private void SaveProjectSettings()
    {
        var project = _projectService.CurrentProject;
        if (project is null)
        {
            return;
        }

        project.UseProjectActivitySettings = UseProjectActivitySettings;
        project.ProjectEnableActivityTracking = ProjectEnableActivityTracking;
        project.ProjectTrackActiveWindow = ProjectTrackActiveWindow;
        project.ProjectTrackFileChanges = ProjectTrackFileChanges;
        project.ProjectTrackGitChanges = ProjectTrackGitChanges;
        project.ProjectTrackTextStatistics = ProjectTrackTextStatistics;
        project.ProjectPauseActivityTracking = ProjectPauseActivityTracking;
        project.ProjectIgnoredFoldersSerialized = ProjectIgnoredFolders;
    }
}
