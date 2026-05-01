using System.Windows;
using System.Windows.Interop;
using System.Runtime.InteropServices;
using System.Windows.Threading;
using System.Drawing;
using Forms = System.Windows.Forms;
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
        private Forms.NotifyIcon? _trayIcon;
        private HwndSource? _hotkeySource;
        private IntPtr _hotkeyWindowHandle = IntPtr.Zero;
        private bool _isExitRequested;
        private bool _isAppBarModeEnabled = true;

        private const int GlobalHotkeyId = 0x5254;
        private const uint WmHotkey = 0x0312;
        private const uint ModAlt = 0x0001;
        private const uint ModControl = 0x0002;
        private const uint ModNoRepeat = 0x4000;
        private const uint VkR = 0x52;
        private const double WindowAttachGap = 10;
        private const double MinMainWindowWidth = 700;

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
            _mainViewModel.RequestHideSidePanel += (_, _) => HideSidePanelOnly();
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
            ArrangeMainWorkspaceNearSidePanel();

            InitializeTrayIcon();
            RegisterGlobalHotkey();
        }

        private void HideSidePanelOnly()
        {
            if (_sidePanelWindow is null || !_sidePanelWindow.IsVisible)
            {
                return;
            }

            _sidePanelWindow.Hide();
        }

        private void ToggleSidePanel()
        {
            if (_sidePanelWindow is null)
            {
                return;
            }

            if (_sidePanelWindow.IsVisible)
            {
                _sidePanelWindow.Hide();
            }
            else
            {
                _sidePanelWindow.Show();
                _sidePanelWindow.SetAppBarMode(_isAppBarModeEnabled);
                ArrangeMainWorkspaceNearSidePanel();
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

            ArrangeMainWorkspaceNearSidePanel();

            _mainWorkspaceWindow.Activate();
        }

        private void ArrangeMainWorkspaceNearSidePanel()
        {
            if (_mainWorkspaceWindow is null || _sidePanelWindow is null || !_sidePanelWindow.IsVisible)
            {
                return;
            }

            var workArea = SystemParameters.WorkArea;
            var targetLeft = _sidePanelWindow.Left + _sidePanelWindow.Width + WindowAttachGap;
            if (targetLeft < workArea.Left)
            {
                targetLeft = workArea.Left;
            }

            var availableWidth = workArea.Right - targetLeft;
            if (availableWidth < MinMainWindowWidth)
            {
                targetLeft = Math.Max(workArea.Left, workArea.Right - MinMainWindowWidth);
                availableWidth = workArea.Right - targetLeft;
            }

            _mainWorkspaceWindow.Left = targetLeft;
            _mainWorkspaceWindow.Top = Math.Max(workArea.Top, _sidePanelWindow.Top);
            _mainWorkspaceWindow.Height = Math.Min(_sidePanelWindow.Height, workArea.Bottom - _mainWorkspaceWindow.Top);
            _mainWorkspaceWindow.Width = Math.Min(workArea.Width, Math.Max(MinMainWindowWidth, availableWidth));
        }

        protected override void OnExit(ExitEventArgs e)
        {
            CleanupSidePanelArtifacts();
            RemoveTrayIcon();
            UnregisterGlobalHotkey();
            base.OnExit(e);
        }

        private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            CleanupSidePanelArtifacts();
            RemoveTrayIcon();
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
                _sidePanelWindow.ForceClose();
            }
            catch
            {
                // best effort cleanup
            }
        }

        private void InitializeTrayIcon()
        {
            if (_trayIcon is not null)
            {
                return;
            }

            var contextMenu = new Forms.ContextMenuStrip();
            contextMenu.Items.Add("Open / Show ReachIT", null, (_, _) => Dispatcher.Invoke(ShowReachIt));
            contextMenu.Items.Add("Hide ReachIT", null, (_, _) => Dispatcher.Invoke(HideReachIt));
            contextMenu.Items.Add(new Forms.ToolStripSeparator());
            contextMenu.Items.Add("Exit", null, (_, _) => Dispatcher.Invoke(ExitFromTray));

            _trayIcon = new Forms.NotifyIcon
            {
                Text = "ReachIT",
                Icon = SystemIcons.Application,
                Visible = true,
                ContextMenuStrip = contextMenu
            };

            _trayIcon.DoubleClick += (_, _) => Dispatcher.Invoke(ShowReachIt);
        }

        private void RemoveTrayIcon()
        {
            if (_trayIcon is null)
            {
                return;
            }

            try
            {
                _trayIcon.Visible = false;
                _trayIcon.Dispose();
            }
            catch
            {
                // best effort cleanup
            }

            _trayIcon = null;
        }

        private void ShowReachIt()
        {
            if (_sidePanelWindow is null)
            {
                return;
            }

            if (!_sidePanelWindow.IsVisible)
            {
                _sidePanelWindow.Show();
            }

            if (_isAppBarModeEnabled && !_sidePanelWindow.IsAppBarModeEnabled)
            {
                _sidePanelWindow.SetAppBarMode(true);
            }

            _sidePanelWindow.Activate();
        }

        private void HideReachIt()
        {
            if (_mainWorkspaceWindow is not null && _mainWorkspaceWindow.IsVisible)
            {
                _mainWorkspaceWindow.Hide();
            }

            if (_sidePanelWindow is null)
            {
                return;
            }

            if (_sidePanelWindow.IsVisible)
            {
                _sidePanelWindow.Hide();
            }
        }

        private void ExitFromTray()
        {
            _isExitRequested = true;

            if (_mainWorkspaceWindow is not null)
            {
                _mainWorkspaceWindow.Close();
            }

            if (_sidePanelWindow is not null)
            {
                _sidePanelWindow.ForceClose();
            }

            Shutdown();
        }

        private void RegisterGlobalHotkey()
        {
            if (_mainWorkspaceWindow is null)
            {
                return;
            }

            _hotkeyWindowHandle = new WindowInteropHelper(_mainWorkspaceWindow).EnsureHandle();
            _hotkeySource = HwndSource.FromHwnd(_hotkeyWindowHandle);
            _hotkeySource?.AddHook(OnHotkeyWindowMessage);

            var success = RegisterHotKey(
                _hotkeyWindowHandle,
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
            if (_hotkeyWindowHandle != IntPtr.Zero)
            {
                UnregisterHotKey(_hotkeyWindowHandle, GlobalHotkeyId);
            }

            if (_hotkeySource is not null)
            {
                _hotkeySource.RemoveHook(OnHotkeyWindowMessage);
            }

            _hotkeySource = null;
            _hotkeyWindowHandle = IntPtr.Zero;
        }

        private IntPtr OnHotkeyWindowMessage(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if ((uint)msg == WmHotkey && wParam.ToInt32() == GlobalHotkeyId)
            {
                ToggleSidePanel();
                handled = true;
            }

            return IntPtr.Zero;
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
    }
}
