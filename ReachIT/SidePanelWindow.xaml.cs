// Hosts the floating side panel with project explorer and quick actions.
using Microsoft.Win32;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using ReachIT.Domain.Models;
using ReachIT.Presentation.ViewModels;

namespace ReachIT;

public partial class SidePanelWindow : Window
{
    private bool _systemEventsSubscribed;
    private bool _closeRequested;

    private const double PinnedWidth = 420;
    private const int WmDisplayChange = 0x007E;
    private const int WmSettingChange = 0x001A;
    private const int WmDpiChanged = 0x02E0;
    private const uint MonitorDefaultToNearest = 2;

    public SidePanelWindow()
    {
        InitializeComponent();

        SourceInitialized += (_, _) =>
        {
            SubscribeToSystemEvents();
            if (PresentationSource.FromVisual(this) is HwndSource source)
            {
                source.AddHook(WndProc);
            }
        };

        Loaded += (_, _) =>
        {
            if (IsAppBarModeEnabled)
            {
                ApplyPinnedLayout();
            }
        };
    }

    public bool IsAppBarModeEnabled { get; private set; }

    public void SetAppBarMode(bool enabled)
    {
        IsAppBarModeEnabled = enabled;

        if (enabled)
        {
            ResizeMode = ResizeMode.NoResize;
            ApplyPinnedLayout();
            return;
        }

        ResizeMode = ResizeMode.CanResizeWithGrip;
    }

    public void ForceClose()
    {
        _closeRequested = true;
        Close();
    }

    private void TreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (DataContext is MainViewModel mainViewModel && e.NewValue is ProjectTreeNode node)
        {
            mainViewModel.SelectTreeNodeCommand.Execute(node);
        }
    }

    private void TreeView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is not MainViewModel mainViewModel)
        {
            return;
        }

        var node = mainViewModel.ProjectTreeViewModel.SelectedNode;
        if (node is null || node.IsDirectory)
        {
            return;
        }

        if (mainViewModel.SelectTreeNodeCommand.CanExecute(node))
        {
            mainViewModel.SelectTreeNodeCommand.Execute(node);
        }

        if (mainViewModel.OpenMainWorkspaceCommand.CanExecute(null))
        {
            mainViewModel.OpenMainWorkspaceCommand.Execute(null);
        }
    }

    private void TitleArea_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (IsAppBarModeEnabled)
        {
            return;
        }

        try
        {
            DragMove();
        }
        catch (InvalidOperationException)
        {
            // WPF can throw if the mouse capture is interrupted while dragging.
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Hide();
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        if (_closeRequested)
        {
            base.OnClosing(e);
            return;
        }

        e.Cancel = true;
        Hide();
    }

    protected override void OnClosed(EventArgs e)
    {
        UnsubscribeFromSystemEvents();
        base.OnClosed(e);
    }

    private void OnSystemLayoutChanged(object? sender, EventArgs e)
    {
        if (!IsAppBarModeEnabled || !IsVisible)
        {
            return;
        }

        Dispatcher.InvokeAsync(ApplyPinnedLayout);
    }

    private void OnUserPreferenceChanged(object? sender, UserPreferenceChangedEventArgs e)
    {
        OnSystemLayoutChanged(sender, EventArgs.Empty);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (!IsAppBarModeEnabled || !IsVisible)
        {
            return IntPtr.Zero;
        }

        if (msg is WmDisplayChange or WmDpiChanged or WmSettingChange)
        {
            Dispatcher.InvokeAsync(ApplyPinnedLayout);
        }

        return IntPtr.Zero;
    }

    private void ApplyPinnedLayout()
    {
        if (!IsAppBarModeEnabled)
        {
            return;
        }

        var hwnd = new WindowInteropHelper(this).Handle;
        var monitorArea = GetMonitorArea(hwnd);

        if (PresentationSource.FromVisual(this) is HwndSource source && source.CompositionTarget is not null)
        {
            var transform = source.CompositionTarget.TransformFromDevice;
            var topLeft = transform.Transform(new Point(monitorArea.left, monitorArea.top));
            var bottomRight = transform.Transform(new Point(monitorArea.right, monitorArea.bottom));

            Left = topLeft.X;
            Top = topLeft.Y;
            Height = Math.Max(0, bottomRight.Y - topLeft.Y);
        }
        else
        {
            Left = monitorArea.left;
            Top = monitorArea.top;
            Height = monitorArea.bottom - monitorArea.top;
        }

        Width = PinnedWidth;
    }

    private void SubscribeToSystemEvents()
    {
        if (_systemEventsSubscribed)
        {
            return;
        }

        SystemEvents.DisplaySettingsChanged += OnSystemLayoutChanged;
        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
        _systemEventsSubscribed = true;
    }

    private void UnsubscribeFromSystemEvents()
    {
        if (!_systemEventsSubscribed)
        {
            return;
        }

        SystemEvents.DisplaySettingsChanged -= OnSystemLayoutChanged;
        SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
        _systemEventsSubscribed = false;
    }

    private static Rect GetMonitorArea(IntPtr hwnd)
    {
        var monitorHandle = MonitorFromWindow(hwnd, MonitorDefaultToNearest);
        if (monitorHandle == IntPtr.Zero)
        {
            return new Rect
            {
                left = (int)SystemParameters.WorkArea.Left,
                top = (int)SystemParameters.WorkArea.Top,
                right = (int)SystemParameters.WorkArea.Right,
                bottom = (int)SystemParameters.WorkArea.Bottom
            };
        }

        var monitorInfo = new MonitorInfo
        {
            cbSize = (uint)Marshal.SizeOf<MonitorInfo>()
        };

        if (!GetMonitorInfo(monitorHandle, ref monitorInfo))
        {
            return new Rect
            {
                left = (int)SystemParameters.WorkArea.Left,
                top = (int)SystemParameters.WorkArea.Top,
                right = (int)SystemParameters.WorkArea.Right,
                bottom = (int)SystemParameters.WorkArea.Bottom
            };
        }

        return monitorInfo.rcWork;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public int left;
        public int top;
        public int right;
        public int bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MonitorInfo
    {
        public uint cbSize;
        public Rect rcMonitor;
        public Rect rcWork;
        public uint dwFlags;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MonitorInfo lpmi);
}
