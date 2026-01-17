using Microsoft.UI.Xaml;
using WinRT.Interop;
using Windows.System;

namespace BetterSettings;

public sealed class HotkeyService : IDisposable
{
    private const int HotkeyId = 1;
    private IntPtr _hwnd;
    private IntPtr _oldWndProc;
    private Win32.WndProc? _newWndProc;
    private Action? _callback;
    private bool _registered;

    public void RegisterToggleHotkey(Window window, Action callback)
    {
        _callback = callback;
        _hwnd = WindowNative.GetWindowHandle(window);
        _newWndProc = WindowProc;
        _oldWndProc = Win32.SetWindowLongPtr(_hwnd, Win32.GWLP_WNDPROC, _newWndProc);

        _registered = Win32.RegisterHotKey(_hwnd, HotkeyId, Win32.MOD_CONTROL, (uint)VirtualKey.Space);
    }

    private IntPtr WindowProc(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam)
    {
        if (msg == Win32.WM_HOTKEY && wParam == (IntPtr)HotkeyId)
        {
            _callback?.Invoke();
            return IntPtr.Zero;
        }

        return Win32.CallWindowProc(_oldWndProc, hWnd, msg, wParam, lParam);
    }

    public void Dispose()
    {
        if (_hwnd != IntPtr.Zero)
        {
            if (_registered)
            {
                Win32.UnregisterHotKey(_hwnd, HotkeyId);
                _registered = false;
            }

            if (_oldWndProc != IntPtr.Zero)
            {
                Win32.SetWindowLongPtr(_hwnd, Win32.GWLP_WNDPROC, _oldWndProc);
            }
        }
    }
}
