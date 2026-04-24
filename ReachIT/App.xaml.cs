using System.Windows;
using System.Windows.Interop;
using System.Runtime.InteropServices;
using System.Windows.Threading;
using ReachIT.Bootstrap;
using ReachIT.Application.Contracts;
using ReachIT.Presentation.ViewModels;

namespace ReachIT
{
    // Bootstraps services and controls startup flow: Start -> Create/Open -> Workspace.
    public partial class App : System.Windows.Application
    {
        private AppHost? _appHost;
        private SidePanelWindow? _sidePanelWindow;
        private MainWindow? _mainWorkspaceWindow;
        private MainViewModel? _mainViewModel;
        private bool _isAppBarModeEnabled = true;

        private const int GlobalHotkeyId = 0x5254;
        private const uint WmHotkey = 0x0312;
        private const uint ModAlt = 0x0001;
        private const uint ModControl = 0x0002;
        private const uint ModNoRepeat = 0x4000;
        private const uint VkR = 0x52;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            DispatcherUnhandledException += OnDispatcherUnhandledException;

            ShutdownMode = ShutdownMode.OnExplicitShutdown;

            _appHost = new AppHost();
            _appHost.Initialize();
            _appHost.GetRequiredService<IDatabaseService>().InitializeAsync().GetAwaiter().GetResult();

            var startViewModel = _appHost.GetRequiredService<StartViewModel>();
            var startWindow = new StartWindow { DataContext = startViewModel };

            startViewModel.RequestCreateProject += (_, _) => OpenCreateProjectWindow(startViewModel, startWindow);

            var startResult = startWindow.ShowDialog();
            if (startResult == true && !string.IsNullOrWhiteSpace(startViewModel.OpenedProjectFolderPath))
            {
                OpenWorkspace(startViewModel.OpenedProjectFolderPath);
                return;
            }

            Shutdown();
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

        private void OpenWorkspace(string projectFolderPath)
        {
            if (_appHost is null)
            {
                Shutdown();
                return;
            }

            var projectService = _appHost.GetRequiredService<IProjectService>();
            projectService.OpenProjectAsync(projectFolderPath).GetAwaiter().GetResult();

            _mainViewModel = _appHost.GetRequiredService<MainViewModel>();
            _mainViewModel.InitializeAsync().GetAwaiter().GetResult();

            _mainViewModel.RequestToggleSidePanel += (_, _) => ToggleSidePanel();
            _mainViewModel.RequestOpenMainWorkspace += (_, _) => OpenMainWorkspaceWindow();
            _mainViewModel.RequestToggleAppBarMode += (_, _) => ToggleAppBarMode();

            _mainWorkspaceWindow = new MainWindow
            {
                DataContext = _mainViewModel
            };

            _sidePanelWindow = new SidePanelWindow
            {
                DataContext = _mainViewModel,
                Owner = null,
                Left = 0,
                Top = 0
            };

            _mainWorkspaceWindow.Closed += (_, _) =>
            {
                CleanupSidePanelArtifacts();
            };

            ShutdownMode = ShutdownMode.OnMainWindowClose;
            MainWindow = _mainWorkspaceWindow;
            _mainWorkspaceWindow.Show();
            _sidePanelWindow.Show();

            _sidePanelWindow.SetAppBarMode(_isAppBarModeEnabled);
            _mainViewModel.SetAppBarMode(_isAppBarModeEnabled);

            RegisterGlobalHotkey();
        }

        private void ToggleSidePanel()
        {
            if (_sidePanelWindow is null)
            {
                return;
            }

            if (_sidePanelWindow.IsVisible)
            {
                if (_isAppBarModeEnabled)
                {
                    _sidePanelWindow.SetAppBarMode(false);
                }

                _sidePanelWindow.Hide();
            }
            else
            {
                _sidePanelWindow.Show();

                if (_isAppBarModeEnabled)
                {
                    _sidePanelWindow.SetAppBarMode(true);
                }

                _sidePanelWindow.Activate();
            }
        }

        private void ToggleAppBarMode()
        {
            _isAppBarModeEnabled = !_isAppBarModeEnabled;

            if (_sidePanelWindow is not null)
            {
                _sidePanelWindow.SetAppBarMode(_isAppBarModeEnabled);
            }

            _mainViewModel?.SetAppBarMode(_isAppBarModeEnabled);
        }

        private void OpenMainWorkspaceWindow()
        {
            if (_mainWorkspaceWindow is null)
            {
                return;
            }

            if (!_mainWorkspaceWindow.IsVisible)
            {
                _mainWorkspaceWindow.Show();
            }

            if (_mainWorkspaceWindow.WindowState == WindowState.Minimized)
            {
                _mainWorkspaceWindow.WindowState = WindowState.Normal;
            }

            _mainWorkspaceWindow.Activate();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            CleanupSidePanelArtifacts();
            UnregisterGlobalHotkey();
            base.OnExit(e);
        }

        private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            CleanupSidePanelArtifacts();
            UnregisterGlobalHotkey();
            // Let default crash behavior continue so exception is visible in debug.
            e.Handled = false;
        }

        private void CleanupSidePanelArtifacts()
        {
            if (_sidePanelWindow is null)
            {
                return;
            }

            try
            {
                _sidePanelWindow.SetAppBarMode(false);
            }
            catch
            {
                // best effort cleanup
            }

            try
            {
                if (_sidePanelWindow.IsVisible)
                {
                    _sidePanelWindow.Close();
                }
            }
            catch
            {
                // best effort cleanup
            }
        }

        private void RegisterGlobalHotkey()
        {
            ComponentDispatcher.ThreadPreprocessMessage += OnThreadPreprocessMessage;

            var success = RegisterHotKey(
                IntPtr.Zero,
                GlobalHotkeyId,
                ModControl | ModAlt | ModNoRepeat,
                VkR);

            if (!success)
            {
                // TODO: Add user-facing notification and configurable fallback hotkey.
            }
        }

        private void UnregisterGlobalHotkey()
        {
            ComponentDispatcher.ThreadPreprocessMessage -= OnThreadPreprocessMessage;
            UnregisterHotKey(IntPtr.Zero, GlobalHotkeyId);
        }

        private void OnThreadPreprocessMessage(ref MSG msg, ref bool handled)
        {
            if (msg.message == WmHotkey && (int)msg.wParam == GlobalHotkeyId)
            {
                ToggleSidePanel();
                handled = true;
            }
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
    }
}
