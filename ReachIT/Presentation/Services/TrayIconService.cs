using System.Drawing;
using System.Diagnostics;
using Forms = System.Windows.Forms;
using ReachIT.Application.Contracts;

namespace ReachIT.Presentation.Services;

public sealed class TrayIconService : ITrayIconService
{
    private Forms.NotifyIcon? _notifyIcon;

    public event EventHandler? ShowHideFloatingRequested;
    public event EventHandler? OpenMainWindowRequested;
    public event EventHandler? QuickAddTaskRequested;
    public event EventHandler? ExitRequested;

    public void Initialize()
    {
        if (_notifyIcon is not null)
        {
            return;
        }

        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Show / Hide floating logo", null, (_, _) => ShowHideFloatingRequested?.Invoke(this, EventArgs.Empty));
        menu.Items.Add("Open Main Window", null, (_, _) => OpenMainWindowRequested?.Invoke(this, EventArgs.Empty));
        menu.Items.Add("Quick Add Task", null, (_, _) => QuickAddTaskRequested?.Invoke(this, EventArgs.Empty));
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => ExitRequested?.Invoke(this, EventArgs.Empty));

        _notifyIcon = new Forms.NotifyIcon
        {
            Text = "ReachIT",
            Icon = LoadIcon(),
            Visible = true,
            ContextMenuStrip = menu
        };

        _notifyIcon.DoubleClick += (_, _) => ShowHideFloatingRequested?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {
        if (_notifyIcon is null)
        {
            return;
        }

        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _notifyIcon = null;
    }

    private static Icon LoadIcon()
    {
        var processPath = Process.GetCurrentProcess().MainModule?.FileName;
        if (!string.IsNullOrWhiteSpace(processPath))
        {
            return Icon.ExtractAssociatedIcon(processPath) ?? SystemIcons.Application;
        }

        return SystemIcons.Application;
    }
}
