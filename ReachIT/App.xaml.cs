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

        _appHost = new AppHost();
        _appHost.Initialize();

        var logger = _appHost.GetRequiredService<ReachIT.Infrastructure.Logging.ILocalLogger>();
        logger.LogInformation("ReachIT hub is starting...");

        try
        {
            await _appHost.GetRequiredService<IDatabaseService>().InitializeAsync();
            var settings = await _appHost.GetRequiredService<IAppSettingsService>().GetAsync();
            LocalizationService.ApplyLanguage(settings.Language);

            var projectFolderPath = ShowStartupHub();
            if (string.IsNullOrWhiteSpace(projectFolderPath))
            {
                Shutdown();
                return;
            }

            await _appHost.GetRequiredService<IWindowManagerService>().StartAsync(projectFolderPath);
        }
        catch (Exception ex)
        {
            logger.LogError("Failed to initialize ReachIT", ex);
            MessageBox.Show("Critical initialization error. See logs for details.", "ReachIT", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown();
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _appHost?.GetRequiredService<IGlobalHotkeyService>().Dispose();
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

        startViewModel.RequestCreateProject += (_, _) => OpenCreateProjectWindow(startViewModel, startWindow);
        startViewModel.RequestOpenSettings += (_, _) => OpenStartupSettingsWindow(startWindow);

        var result = startWindow.ShowDialog();
        return result == true ? startViewModel.OpenedProjectFolderPath : null;
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
