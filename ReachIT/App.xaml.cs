using System.Windows;
using System.Windows.Threading;
using ReachIT.Application.Contracts;
using ReachIT.Bootstrap;
using ReachIT.Presentation.Services;
using ReachIT.Presentation.ViewModels;

namespace ReachIT;

public partial class App : System.Windows.Application
{
    private AppHost? _appHost;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        DispatcherUnhandledException += OnDispatcherUnhandledException;
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        LoadingWindow? loadingWindow = null;
        ReachIT.Infrastructure.Logging.ILocalLogger? logger = null;

        try
        {
            loadingWindow = await ShowLoadingWindowAsync("Готуємо ReachIT...");

            _appHost = new AppHost();
            _appHost.Initialize();

            logger = _appHost.GetRequiredService<ReachIT.Infrastructure.Logging.ILocalLogger>();
            logger.LogInformation("ReachIT hub is starting...");

            loadingWindow.ShowStatus("Перевіряємо базу даних...");
            await _appHost.GetRequiredService<IDatabaseService>().InitializeAsync();

            loadingWindow.ShowStatus("Завантажуємо налаштування...");
            var settings = await _appHost.GetRequiredService<IAppSettingsService>().GetAsync();
            LocalizationService.ApplyLanguage(settings.Language);

            CloseLoadingWindow(loadingWindow);
            loadingWindow = null;

            var projectFolderPath = ShowStartupHub();
            if (string.IsNullOrWhiteSpace(projectFolderPath))
            {
                Shutdown();
                return;
            }

            loadingWindow = await ShowLoadingWindowAsync("Відкриваємо проєкт...");
            await _appHost.GetRequiredService<IWindowManagerService>().StartAsync(projectFolderPath);
            CloseLoadingWindow(loadingWindow);
        }
        catch (Exception ex)
        {
            CloseLoadingWindow(loadingWindow);
            logger?.LogError("Failed to initialize ReachIT", ex);
            MessageBox.Show("Critical initialization error. See logs for details.", "ReachIT", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown();
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _appHost?.GetRequiredService<IGlobalHotkeyService>().Dispose();
        _appHost?.GetRequiredService<IActivityMonitorService>().Dispose();
        _appHost?.GetRequiredService<ITrayIconService>().Dispose();
        base.OnExit(e);
    }

    private string? ShowStartupHub()
    {
        if (_appHost is null)
        {
            return null;
        }

        var startViewModel = _appHost.GetRequiredService<StartViewModel>();
        var startWindow = new StartWindow { DataContext = startViewModel };

        EventHandler? createHandler = null;
        EventHandler? accountHandler = null;
        EventHandler? settingsHandler = null;

        createHandler = (_, _) => OpenCreateProjectWindow(startViewModel, startWindow);
        accountHandler = async (_, _) =>
        {
            OpenAccountWindow(startWindow);
            await startViewModel.LoadAsync().ConfigureAwait(true);
        };
        settingsHandler = (_, _) => OpenStartupSettingsWindow(startWindow);

        startViewModel.RequestCreateProject += createHandler;
        startViewModel.RequestOpenAccount += accountHandler;
        startViewModel.RequestOpenSettings += settingsHandler;

        var result = startWindow.ShowDialog();
        startViewModel.RequestCreateProject -= createHandler;
        startViewModel.RequestOpenAccount -= accountHandler;
        startViewModel.RequestOpenSettings -= settingsHandler;
        return result == true ? startViewModel.OpenedProjectFolderPath : null;
    }

    private static async Task<LoadingWindow> ShowLoadingWindowAsync(string status)
    {
        var loadingWindow = new LoadingWindow();
        loadingWindow.ShowStatus(status);
        loadingWindow.Show();

        await loadingWindow.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
        return loadingWindow;
    }

    private static void CloseLoadingWindow(LoadingWindow? loadingWindow)
    {
        if (loadingWindow?.IsVisible == true)
        {
            loadingWindow.Close();
        }
    }

    private void OpenCreateProjectWindow(StartViewModel startViewModel, Window owner)
    {
        if (_appHost is null)
        {
            return;
        }

        var createViewModel = _appHost.GetRequiredService<CreateProjectViewModel>();
        var createWindow = new CreateProjectWindow
        {
            Owner = owner,
            DataContext = createViewModel
        };

        var created = createWindow.ShowDialog();
        if (created == true && !string.IsNullOrWhiteSpace(createViewModel.CreatedProjectFolderPath))
        {
            startViewModel.CompleteOpen(createViewModel.CreatedProjectFolderPath);
        }
    }

    private void OpenAccountWindow(Window owner)
    {
        if (_appHost is null)
        {
            return;
        }

        var accountWindow = new AccountLoginWindow(_appHost.GetRequiredService<IAccountService>())
        {
            Owner = owner
        };
        accountWindow.ShowDialog();
    }

    private async void OpenStartupSettingsWindow(Window owner)
    {
        if (_appHost is null)
        {
            return;
        }

        var settingsViewModel = _appHost.GetRequiredService<SettingsViewModel>();
        await settingsViewModel.LoadAsync();

        var settingsWindow = new Window
        {
            Title = FindResource("App.SettingsTitle") as string ?? "ReachIT Settings",
            Owner = owner,
            Width = 680,
            Height = 620,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = FindResource("ReachItWindowBackgroundBrush") as System.Windows.Media.Brush,
            Content = new ReachIT.Presentation.Views.SettingsView
            {
                DataContext = settingsViewModel
            }
        };

        settingsWindow.ShowDialog();
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        _appHost?.GetRequiredService<ReachIT.Infrastructure.Logging.ILocalLogger>()?.LogError("Unhandled exception in dispatcher", e.Exception);
        e.Handled = false;
    }
}
