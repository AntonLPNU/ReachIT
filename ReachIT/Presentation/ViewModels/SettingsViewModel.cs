// Exposes editable app settings placeholders.
using System.Windows.Input;
using ReachIT.Application.Contracts;
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

    private bool _showRelatedTasksInTree;
    private bool _hideSidePanelAfterExternalFileOpen = true;
    private string _theme = "Light";
    private string _language = "en";
    private bool _enableNotifications = true;
    private string _sidePanelHotkey = "Ctrl+Shift+R";
    private FocusModeType _defaultFocusMode = FocusModeType.Light;
    private string _backupLocationPath = string.Empty;
    private AppSettings? _model;

    public SettingsViewModel(IDatabaseService databaseService)
    {
        _databaseService = databaseService;
        SaveCommand = new AsyncCommand(_ => SaveSettingsAsync());
    }

    public ICommand SaveCommand { get; }

    public IReadOnlyList<string> AvailableThemes { get; } = ["Light", "Dark", "System"];

    public IReadOnlyList<string> AvailableLanguages { get; } = ["en", "uk"];

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
        DefaultFocusMode = _model.DefaultFocusMode;
        BackupLocationPath = _model.BackupLocationPath;
        OnPropertyChanged(nameof(AppDataLocation));
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
        _model.DefaultFocusMode = DefaultFocusMode;
        _model.BackupLocationPath = BackupLocationPath;

        using var db = _databaseService.CreateDbContext();
        db.AppSettings.Update(_model);
        await db.SaveChangesAsync().ConfigureAwait(true);
        LocalizationService.ApplyLanguage(Language);
    }
}
