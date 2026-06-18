using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace ShadowPilot.Services;

// Global hotkeys using Win32 RegisterHotKey — doesn't steal focus from active app
public sealed class HotkeyManager : IDisposable
{
    public static readonly HotkeyManager Shared = new();

    public Action? OnMicToggle;       // Ctrl+Shift+L
    public Action? OnGetAnswer;       // Ctrl+Shift+A
    public Action? OnScreenshot;      // Ctrl+Shift+D
    public Action? OnClear;           // Ctrl+Shift+X
    public Action? OnWritingToggle;   // Ctrl+Shift+W

    private HwndSource? _hwndSource;
    private const int WM_HOTKEY = 0x0312;

    [DllImport("user32.dll")]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    private const uint MOD_CONTROL = 0x0002;
    private const uint MOD_SHIFT   = 0x0004;
    private const uint MOD_NOREPEAT = 0x4000;

    private HotkeyManager() { }

    public void Register(Window window)
    {
        var handle = new WindowInteropHelper(window).Handle;
        _hwndSource = HwndSource.FromHwnd(handle);
        _hwndSource?.AddHook(WndProc);

        var mods = MOD_CONTROL | MOD_SHIFT | MOD_NOREPEAT;

        RegisterHotKey(handle, 1, mods, (uint)System.Windows.Forms.Keys.L);
        RegisterHotKey(handle, 2, mods, (uint)System.Windows.Forms.Keys.A);
        RegisterHotKey(handle, 3, mods, (uint)System.Windows.Forms.Keys.D);
        RegisterHotKey(handle, 4, mods, (uint)System.Windows.Forms.Keys.X);
        RegisterHotKey(handle, 5, mods, (uint)System.Windows.Forms.Keys.W);
    }

    public void Unregister(Window window)
    {
        var handle = new WindowInteropHelper(window).Handle;
        for (int i = 1; i <= 5; i++) UnregisterHotKey(handle, i);
        _hwndSource?.RemoveHook(WndProc);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_HOTKEY)
        {
            switch (wParam.ToInt32())
            {
                case 1: Application.Current.Dispatcher.Invoke(() => OnMicToggle?.Invoke());     break;
                case 2: Application.Current.Dispatcher.Invoke(() => OnGetAnswer?.Invoke());     break;
                case 3: Application.Current.Dispatcher.Invoke(() => OnScreenshot?.Invoke());    break;
                case 4: Application.Current.Dispatcher.Invoke(() => OnClear?.Invoke());         break;
                case 5: Application.Current.Dispatcher.Invoke(() => OnWritingToggle?.Invoke()); break;
            }
            handled = true;
        }
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        _hwndSource?.Dispose();
    }
}
