using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using ReachIT.Application.Contracts;
using ReachIT.Domain.Models;

namespace ReachIT.Application.Services;

public sealed class ForegroundWindowService : IForegroundWindowService
{
    public ForegroundWindowSnapshot GetCurrent()
    {
        var handle = GetForegroundWindow();
        if (handle == IntPtr.Zero)
        {
            return new ForegroundWindowSnapshot(string.Empty, string.Empty, string.Empty, 0);
        }

        _ = GetWindowThreadProcessId(handle, out var processId);
        var title = GetWindowTitle(handle);

        try
        {
            using var process = Process.GetProcessById((int)processId);
            return new ForegroundWindowSnapshot(
                process.ProcessName,
                process.ProcessName,
                title,
                process.Id,
                GetExecutablePath(process));
        }
        catch
        {
            return new ForegroundWindowSnapshot(string.Empty, string.Empty, title, (int)processId);
        }
    }

    private static string GetExecutablePath(Process process)
    {
        try
        {
            return process.MainModule?.FileName ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string GetWindowTitle(IntPtr handle)
    {
        var builder = new StringBuilder(512);
        var length = GetWindowText(handle, builder, builder.Capacity);
        return length <= 0 ? string.Empty : builder.ToString();
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);
}
