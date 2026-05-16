// Reads the active browser address bar through Windows UI Automation when available.
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Automation;
using ReachIT.Application.Contracts;
using ReachIT.Application.Security;

namespace ReachIT.Infrastructure.OS;

public sealed class ActiveBrowserUrlService : IActiveBrowserUrlService
{
    private static readonly HashSet<string> BrowserProcesses = new(StringComparer.OrdinalIgnoreCase)
    {
        "chrome",
        "msedge",
        "firefox",
        "brave",
        "opera",
        "vivaldi"
    };

    public string? TryGetActiveBrowserUrl()
    {
        try
        {
            var handle = GetForegroundWindow();
            if (handle == IntPtr.Zero)
            {
                return null;
            }

            _ = GetWindowThreadProcessId(handle, out var processId);
            using var process = Process.GetProcessById((int)processId);
            if (!BrowserProcesses.Contains(process.ProcessName))
            {
                return null;
            }

            var root = AutomationElement.FromHandle(handle);
            if (root is null)
            {
                return null;
            }

            foreach (var edit in FindEditElements(root))
            {
                var value = TryReadValue(edit);
                if (string.IsNullOrWhiteSpace(value))
                {
                    continue;
                }

                value = NormalizeLikelyAddress(value);
                if (WebResourceSecurity.IsSafeWebUrl(value))
                {
                    return WebResourceSecurity.NormalizeAndValidateUrl(value);
                }
            }
        }
        catch
        {
            return null;
        }

        return null;
    }

    private static IEnumerable<AutomationElement> FindEditElements(AutomationElement root)
    {
        var condition = new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Edit);
        var elements = root.FindAll(TreeScope.Descendants, condition);
        for (var i = 0; i < elements.Count; i++)
        {
            if (elements[i] is AutomationElement element)
            {
                yield return element;
            }
        }
    }

    private static string TryReadValue(AutomationElement element)
    {
        try
        {
            if (element.TryGetCurrentPattern(ValuePattern.Pattern, out var pattern)
                && pattern is ValuePattern valuePattern)
            {
                return valuePattern.Current.Value ?? string.Empty;
            }
        }
        catch
        {
            return string.Empty;
        }

        return string.Empty;
    }

    private static string NormalizeLikelyAddress(string value)
    {
        value = value.Trim();
        if (value.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return value;
        }

        return value.Contains('.') && !value.Contains(' ')
            ? $"https://{value}"
            : value;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);
}
