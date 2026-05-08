using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace ReachIT;

public partial class FocusWarningWindow : Window
{
    public event EventHandler? StopFocusRequested;
    public event EventHandler<string>? AllowAppRequested;
    private string _processName = string.Empty;

    public FocusWarningWindow()
    {
        InitializeComponent();
    }

    public void ShowWarning(string message, string processName)
    {
        _processName = processName;
        MessageText.Text = message;
        Show();
        WindowState = WindowState.Normal;
        Topmost = false;
        Topmost = true;
        Activate();

        var handle = new WindowInteropHelper(this).Handle;
        if (handle != IntPtr.Zero)
        {
            SetForegroundWindow(handle);
        }
    }

    private void BackButton_Click(object sender, RoutedEventArgs e)
    {
        Hide();
    }

    private void WhitelistButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_processName))
        {
            return;
        }

        var first = MessageBox.Show(
            $"Add '{_processName}' to the Focus Mode whitelist?",
            "ReachIT",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);
        if (first != MessageBoxResult.Yes)
        {
            return;
        }

        var second = MessageBox.Show(
            "Adding distracting apps or games to the whitelist can weaken Focus Mode and make it easier to drift away from your work. Add it only if this app is genuinely needed for the current project.\n\nAdd it anyway?",
            "ReachIT Focus",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        if (second != MessageBoxResult.Yes)
        {
            return;
        }

        Hide();
        AllowAppRequested?.Invoke(this, _processName);
    }

    private void StopFocusButton_Click(object sender, RoutedEventArgs e)
    {
        Hide();
        StopFocusRequested?.Invoke(this, EventArgs.Empty);
    }

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);
}
