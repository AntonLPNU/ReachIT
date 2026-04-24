// Hosts the floating side panel with project explorer and quick actions.
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using ReachIT.Domain.Models;
using ReachIT.Presentation.ViewModels;

namespace ReachIT;

public partial class SidePanelWindow : Window
{
    private uint _appBarCallbackMessageId;
    private bool _isAppBarRegistered;

    public SidePanelWindow()
    {
        InitializeComponent();

        Loaded += (_, _) =>
        {
            if (IsAppBarModeEnabled)
            {
                UpdateAppBarPosition();
            }
        };

        SizeChanged += (_, _) =>
        {
            if (IsAppBarModeEnabled)
            {
                UpdateAppBarPosition();
            }
        };
    }

    public bool IsAppBarModeEnabled { get; private set; }

    private void TreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (DataContext is MainViewModel mainViewModel && e.NewValue is ProjectTreeNode node)
        {
            mainViewModel.SelectTreeNodeCommand.Execute(node);
        }
    }

    public void SetAppBarMode(bool enabled)
    {
        if (enabled == IsAppBarModeEnabled)
        {
            return;
        }

        if (enabled)
        {
            RegisterAppBar();
            UpdateAppBarPosition();
        }
        else
        {
            UnregisterAppBar();
        }

        IsAppBarModeEnabled = enabled;
    }

    protected override void OnClosed(EventArgs e)
    {
        UnregisterAppBar();
        base.OnClosed(e);
    }

    private void RegisterAppBar()
    {
        if (_isAppBarRegistered)
        {
            return;
        }

        var hwnd = new WindowInteropHelper(this).EnsureHandle();
        _appBarCallbackMessageId = RegisterWindowMessage($"ReachIT.AppBar.{hwnd}");

        var appBarData = CreateAppBarData(hwnd);
        appBarData.uCallbackMessage = _appBarCallbackMessageId;
        SHAppBarMessage(AbmNew, ref appBarData);

        _isAppBarRegistered = true;
    }

    private void UnregisterAppBar()
    {
        if (!_isAppBarRegistered)
        {
            return;
        }

        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd != IntPtr.Zero)
        {
            var appBarData = CreateAppBarData(hwnd);
            SHAppBarMessage(AbmRemove, ref appBarData);
        }

        _isAppBarRegistered = false;
    }

    private void UpdateAppBarPosition()
    {
        if (!_isAppBarRegistered)
        {
            return;
        }

        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero)
        {
            return;
        }

        var workArea = SystemParameters.WorkArea;
        var requestedWidth = (int)Math.Max(280, Width);

        var appBarData = CreateAppBarData(hwnd);
        appBarData.uEdge = AbeLeft;
        appBarData.rc.left = (int)workArea.Left;
        appBarData.rc.top = (int)workArea.Top;
        appBarData.rc.right = appBarData.rc.left + requestedWidth;
        appBarData.rc.bottom = (int)workArea.Bottom;

        SHAppBarMessage(AbmQueryPos, ref appBarData);
        appBarData.rc.right = appBarData.rc.left + requestedWidth;
        SHAppBarMessage(AbmSetPos, ref appBarData);

        Left = appBarData.rc.left;
        Top = appBarData.rc.top;
        Width = requestedWidth;
        Height = appBarData.rc.bottom - appBarData.rc.top;
    }

    private static AppBarData CreateAppBarData(IntPtr hwnd)
    {
        return new AppBarData
        {
            cbSize = (uint)Marshal.SizeOf<AppBarData>(),
            hWnd = hwnd
        };
    }

    private const int AbmNew = 0;
    private const int AbmRemove = 1;
    private const int AbmQueryPos = 2;
    private const int AbmSetPos = 3;
    private const int AbeLeft = 0;

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public int left;
        public int top;
        public int right;
        public int bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct AppBarData
    {
        public uint cbSize;
        public IntPtr hWnd;
        public uint uCallbackMessage;
        public uint uEdge;
        public Rect rc;
        public int lParam;
    }

    [DllImport("shell32.dll", CallingConvention = CallingConvention.StdCall)]
    private static extern uint SHAppBarMessage(int dwMessage, ref AppBarData pData);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern uint RegisterWindowMessage(string lpString);
}
