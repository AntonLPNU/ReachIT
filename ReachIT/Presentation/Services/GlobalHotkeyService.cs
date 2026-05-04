using System.Runtime.InteropServices;
using System.Windows.Input;
using System.Windows.Interop;
using ReachIT.Application.Contracts;
using ReachIT.Infrastructure.Logging;

namespace ReachIT.Presentation.Services;

public sealed class GlobalHotkeyService : IGlobalHotkeyService
{
    private const int ToggleFloatingId = 0x5201;
    private const int QuickAddId = 0x5202;
    private const int ToggleFocusId = 0x5203;
    private const int MainWindowId = 0x5204;
    private const uint WmHotkey = 0x0312;
    private const uint ModAlt = 0x0001;
    private const uint ModControl = 0x0002;
    private const uint ModNoRepeat = 0x4000;

    private HwndSource? _source;
    private IntPtr _handle;
    private readonly ILocalLogger _logger;

    public GlobalHotkeyService(ILocalLogger logger)
    {
        _logger = logger;
    }

    public event EventHandler? ToggleFloatingRequested;
    public event EventHandler? QuickAddTaskRequested;
    public event EventHandler? ToggleFocusRequested;
    public event EventHandler? OpenMainWindowRequested;

    public void Register()
    {
        if (_source is not null)
        {
            return;
        }

        var parameters = new HwndSourceParameters("ReachITHotkeys")
        {
            Width = 0,
            Height = 0,
            WindowStyle = 0x800000
        };

        _source = new HwndSource(parameters);
        _source.AddHook(OnMessage);
        _handle = _source.Handle;

        RegisterHotkeyOrLog(ToggleFloatingId, "Ctrl+Alt+R", System.Windows.Input.Key.R);
        RegisterHotkeyOrLog(QuickAddId, "Ctrl+Alt+T", System.Windows.Input.Key.T);
        RegisterHotkeyOrLog(ToggleFocusId, "Ctrl+Alt+F", System.Windows.Input.Key.F);
        RegisterHotkeyOrLog(MainWindowId, "Ctrl+Alt+M", System.Windows.Input.Key.M);
    }

    public void Unregister()
    {
        if (_handle != IntPtr.Zero)
        {
            UnregisterHotKey(_handle, ToggleFloatingId);
            UnregisterHotKey(_handle, QuickAddId);
            UnregisterHotKey(_handle, ToggleFocusId);
            UnregisterHotKey(_handle, MainWindowId);
        }

        if (_source is not null)
        {
            _source.RemoveHook(OnMessage);
            _source.Dispose();
        }

        _source = null;
        _handle = IntPtr.Zero;
    }

    public void Dispose()
    {
        Unregister();
    }

    private void RegisterHotkeyOrLog(int id, string name, System.Windows.Input.Key key)
    {
        var virtualKey = (uint)KeyInterop.VirtualKeyFromKey(key);
        var registered = RegisterHotKey(_handle, id, ModControl | ModAlt | ModNoRepeat, virtualKey);
        if (registered)
        {
            _logger.LogInformation($"Registered global hotkey: {name}");
            return;
        }

        var errorCode = Marshal.GetLastWin32Error();
        _logger.LogWarning($"Failed to register global hotkey ({name}). Win32 error: {errorCode}. It may be in use by another application.");
    }

    private IntPtr OnMessage(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if ((uint)msg != WmHotkey)
        {
            return IntPtr.Zero;
        }

        handled = true;
        switch (wParam.ToInt32())
        {
            case ToggleFloatingId:
                ToggleFloatingRequested?.Invoke(this, EventArgs.Empty);
                break;
            case QuickAddId:
                QuickAddTaskRequested?.Invoke(this, EventArgs.Empty);
                break;
            case ToggleFocusId:
                ToggleFocusRequested?.Invoke(this, EventArgs.Empty);
                break;
            case MainWindowId:
                OpenMainWindowRequested?.Invoke(this, EventArgs.Empty);
                break;
        }

        return IntPtr.Zero;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}
