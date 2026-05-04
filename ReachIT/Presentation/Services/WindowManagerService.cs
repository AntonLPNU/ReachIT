using System.Windows;
using ReachIT.Application.Contracts;
using ReachIT.Domain.Models;
using ReachIT.Infrastructure.Logging;
using ReachIT.Presentation.ViewModels;

namespace ReachIT.Presentation.Services;

public sealed class WindowManagerService : IWindowManagerService
{
    private readonly IAppSettingsService _settingsService;
    private readonly IRecentFilesService _recentFilesService;
    private readonly IProjectService _projectService;
    private readonly ITrayIconService _trayIconService;
    private readonly IGlobalHotkeyService _hotkeyService;
    private readonly ILocalLogger _logger;
    private readonly MainViewModel _mainViewModel;
    private readonly FloatingLogoViewModel _floatingLogoViewModel;
    private readonly QuickMenuViewModel _quickMenuViewModel;
    private readonly QuickAddTaskViewModel _quickAddTaskViewModel;

    private AppSettings? _settings;
    private FloatingLogoWindow? _floatingLogoWindow;
    private QuickMenuWindow? _quickMenuWindow;
    private QuickAddTaskWindow? _quickAddTaskWindow;
    private MainWindow? _mainWindow;
    private SidePanelWindow? _projectExplorerWindow;
    private bool _isExitRequested;
    private bool _viewModelsWired;
    private bool _infrastructureWired;

    public WindowManagerService(
        IAppSettingsService settingsService,
        IRecentFilesService recentFilesService,
        IProjectService projectService,
        ITrayIconService trayIconService,
        IGlobalHotkeyService hotkeyService,
        ILocalLogger logger,
        MainViewModel mainViewModel,
        FloatingLogoViewModel floatingLogoViewModel,
        QuickMenuViewModel quickMenuViewModel,
        QuickAddTaskViewModel quickAddTaskViewModel)
    {
        _settingsService = settingsService;
        _recentFilesService = recentFilesService;
        _projectService = projectService;
        _trayIconService = trayIconService;
        _hotkeyService = hotkeyService;
        _logger = logger;
        _mainViewModel = mainViewModel;
        _floatingLogoViewModel = floatingLogoViewModel;
        _quickMenuViewModel = quickMenuViewModel;
        _quickAddTaskViewModel = quickAddTaskViewModel;
    }

    public async Task StartAsync(string? projectFolderPath = null, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation($"Workspace startup requested. Project path: {projectFolderPath ?? "(none)"}");
        _settings = await _settingsService.GetAsync(cancellationToken).ConfigureAwait(true);
        await OpenStartupProjectAsync(projectFolderPath, cancellationToken).ConfigureAwait(true);
        await _mainViewModel.InitializeAsync(cancellationToken).ConfigureAwait(true);

        WireViewModels();
        WireInfrastructure();

        _trayIconService.Initialize();
        _hotkeyService.Register();

        ActivateWorkspaceShell();
        _logger.LogInformation("Workspace shell activated.");
    }

    public void ToggleFloatingLogo()
    {
        if (_floatingLogoWindow?.IsVisible == true)
        {
            HideFloatingLogo();
        }
        else
        {
            ShowFloatingLogo();
        }
    }

    public void ShowFloatingLogo()
    {
        EnsureFloatingLogoWindow();
        _floatingLogoWindow!.Show();
        _floatingLogoWindow.Topmost = true;
        _floatingLogoWindow.Activate();
    }

    public void HideFloatingLogo()
    {
        _quickMenuWindow?.Hide();
        _floatingLogoWindow?.Hide();
    }

    public void ToggleQuickMenu()
    {
        EnsureFloatingLogoWindow();
        EnsureQuickMenuWindow();

        if (_quickMenuWindow!.IsVisible)
        {
            _quickMenuWindow.Hide();
            return;
        }

        _quickMenuWindow.Left = _floatingLogoWindow!.Left + _floatingLogoWindow.Width + 10;
        _quickMenuWindow.Top = _floatingLogoWindow.Top;
        _quickMenuWindow.Show();
        _quickMenuWindow.Activate();
    }

    public async void OpenQuickAddTask()
    {
        EnsureQuickAddTaskWindow();
        await _quickAddTaskViewModel.LoadAsync().ConfigureAwait(true);
        _quickAddTaskWindow!.Show();
        _quickAddTaskWindow.Activate();
    }

    public void OpenProjectExplorer()
    {
        EnsureProjectExplorerWindow();
        _projectExplorerWindow!.Show();
        _projectExplorerWindow.Activate();
    }

    public void OpenMainWindow()
    {
        EnsureMainWindow();
        _mainWindow!.Show();
        if (_mainWindow.WindowState == WindowState.Minimized)
        {
            _mainWindow.WindowState = WindowState.Normal;
        }

        _mainWindow.Activate();
    }

    public void OpenSettings()
    {
        _mainViewModel.OpenSettingsCommand.Execute(null);
        OpenMainWindow();
    }

    public void OpenStatistics()
    {
        _mainViewModel.OpenStatisticsCommand.Execute(null);
        OpenMainWindow();
    }

    public void ToggleFocusMode()
    {
        if (_mainViewModel.FocusModeViewModel.IsActive)
        {
            _mainViewModel.FocusModeViewModel.StopCommand.Execute(null);
        }
        else
        {
            _mainViewModel.FocusModeViewModel.StartCommand.Execute(null);
        }
    }

    public void ExitApplication()
    {
        _isExitRequested = true;
        _hotkeyService.Dispose();
        _trayIconService.Dispose();

        _quickMenuWindow?.Close();
        _quickAddTaskWindow?.Close();
        _projectExplorerWindow?.ForceClose();
        _floatingLogoWindow?.Close();
        _mainWindow?.Close();

        System.Windows.Application.Current.Shutdown();
    }

    private async Task OpenStartupProjectAsync(string? projectFolderPath, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(projectFolderPath))
        {
            return;
        }

        var opened = await _projectService.OpenProjectAsync(projectFolderPath, cancellationToken).ConfigureAwait(true);
        if (opened is not null && _settings is not null)
        {
            _logger.LogInformation($"Opened project: {opened.ProjectDirectoryPath}");
            _settings.LastOpenedProjectPath = opened.ProjectDirectoryPath;
            await _settingsService.SaveAsync(_settings, cancellationToken).ConfigureAwait(true);
        }
        else
        {
            _logger.LogWarning($"Project open returned no project for path: {projectFolderPath}");
        }
    }

    private void WireViewModels()
    {
        if (_viewModelsWired)
        {
            return;
        }

        _floatingLogoViewModel.ToggleMenuRequested += (_, _) => ToggleQuickMenu();
        _floatingLogoViewModel.HideRequested += (_, _) => HideFloatingLogo();

        _quickMenuViewModel.NewTaskRequested += (_, _) => OpenQuickAddTask();
        _quickMenuViewModel.ProjectExplorerRequested += (_, _) => OpenProjectExplorer();
        _quickMenuViewModel.FocusModeRequested += (_, _) => ToggleFocusMode();
        _quickMenuViewModel.StatisticsRequested += (_, _) => OpenStatistics();
        _quickMenuViewModel.MainWindowRequested += (_, _) => OpenMainWindow();
        _quickMenuViewModel.SettingsRequested += (_, _) => OpenSettings();
        _quickMenuViewModel.HideRequested += (_, _) => HideFloatingLogo();
        _quickMenuViewModel.ExitRequested += (_, _) => ExitApplication();

        _quickAddTaskViewModel.Saved += async (_, _) =>
        {
            _quickAddTaskWindow?.Hide();
            await _mainViewModel.InitializeAsync().ConfigureAwait(true);
        };
        _quickAddTaskViewModel.Cancelled += (_, _) => _quickAddTaskWindow?.Hide();

        _mainViewModel.RequestToggleSidePanel += (_, _) => OpenProjectExplorer();
        _mainViewModel.RequestHideSidePanel += (_, _) => _projectExplorerWindow?.Hide();
        _mainViewModel.RequestOpenMainWorkspace += (_, _) => OpenMainWindow();
        _mainViewModel.RequestExitApplication += (_, _) => ExitApplication();

        _viewModelsWired = true;
    }

    private void WireInfrastructure()
    {
        if (_infrastructureWired)
        {
            return;
        }

        _trayIconService.ShowHideFloatingRequested += (_, _) => ToggleFloatingLogo();
        _trayIconService.OpenMainWindowRequested += (_, _) => OpenMainWindow();
        _trayIconService.QuickAddTaskRequested += (_, _) => OpenQuickAddTask();
        _trayIconService.ExitRequested += (_, _) => ExitApplication();

        _hotkeyService.ToggleFloatingRequested += (_, _) => ToggleFloatingLogo();
        _hotkeyService.QuickAddTaskRequested += (_, _) => OpenQuickAddTask();
        _hotkeyService.ToggleFocusRequested += (_, _) => ToggleFocusMode();
        _hotkeyService.OpenMainWindowRequested += (_, _) => OpenMainWindow();

        _infrastructureWired = true;
    }

    private void ActivateWorkspaceShell()
    {
        EnsureMainWindow();
        EnsureFloatingLogoWindow();
        EnsureQuickMenuWindow();
        EnsureQuickAddTaskWindow();
        EnsureProjectExplorerWindow();

        _quickMenuWindow?.Hide();
        _quickAddTaskWindow?.Hide();
        _projectExplorerWindow?.Hide();

        if (_settings?.ShowFloatingLogoOnStartup != false)
        {
            ShowFloatingLogo();
        }

        OpenMainWindow();
    }

    private void EnsureFloatingLogoWindow()
    {
        if (_floatingLogoWindow is not null)
        {
            return;
        }

        _floatingLogoWindow = new FloatingLogoWindow
        {
            DataContext = _floatingLogoViewModel,
            Left = _settings?.FloatingLogoLeft ?? 24,
            Top = _settings?.FloatingLogoTop ?? 160
        };

        _floatingLogoWindow.PositionChangedByUser += async (_, _) =>
        {
            if (_settings is null)
            {
                return;
            }

            _settings.FloatingLogoLeft = _floatingLogoWindow.Left;
            _settings.FloatingLogoTop = _floatingLogoWindow.Top;
            await _settingsService.SaveAsync(_settings).ConfigureAwait(true);
        };
    }

    private void EnsureQuickMenuWindow()
    {
        _quickMenuWindow ??= new QuickMenuWindow { DataContext = _quickMenuViewModel };
    }

    private void EnsureQuickAddTaskWindow()
    {
        _quickAddTaskWindow ??= new QuickAddTaskWindow { DataContext = _quickAddTaskViewModel };
    }

    private void EnsureMainWindow()
    {
        if (_mainWindow is not null)
        {
            return;
        }

        _mainWindow = new MainWindow { DataContext = _mainViewModel };
        _mainWindow.Closing += (_, e) =>
        {
            if (_isExitRequested)
            {
                return;
            }

            e.Cancel = true;
            _mainWindow.Hide();
        };
    }

    private void EnsureProjectExplorerWindow()
    {
        if (_projectExplorerWindow is not null)
        {
            return;
        }

        _projectExplorerWindow = new SidePanelWindow
        {
            DataContext = _mainViewModel,
            Width = 360,
            Height = 620,
            Left = 24,
            Top = 240
        };
        _projectExplorerWindow.SetAppBarMode(false);
    }
}
