using System;
using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;
using WinRT.Interop;

namespace BetterSettings.App;

public sealed class HotkeyService : IDisposable
{
    private const int HotkeyId = 0xBEEF;
    private IntPtr _windowHandle;
    private Win32.WndProc? _newWndProc;
    private IntPtr _oldWndProc;
    private Action? _onToggle;
    private Action<uint>? _onTrayMessage;

    public uint TrayMessageId { get; } = Win32.WM_APP + 1;
    public IntPtr WindowHandle => _windowHandle;

    public void RegisterToggleHotkey(Window window, Action onToggle)
    {
        _windowHandle = WindowNative.GetWindowHandle(window);
        _onToggle = onToggle;

        _newWndProc = WindowProc;
        var newWndProcPtr = Marshal.GetFunctionPointerForDelegate(_newWndProc);
        _oldWndProc = Win32.SetWindowLongPtr(_windowHandle, Win32.GWLP_WNDPROC, newWndProcPtr);

        Win32.RegisterHotKey(_windowHandle, HotkeyId, Win32.MOD_CONTROL, Win32.VK_SPACE);
    }

    public void RegisterTrayCallback(Action<uint> onTrayMessage)
    {
        _onTrayMessage = onTrayMessage;
    }

    private IntPtr WindowProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        if (msg == Win32.WM_HOTKEY && wParam.ToInt32() == HotkeyId)
        {
            _onToggle?.Invoke();
            return IntPtr.Zero;
        }

        if (msg == TrayMessageId)
        {
            _onTrayMessage?.Invoke((uint)lParam.ToInt64());
            return IntPtr.Zero;
        }

        return Win32.CallWindowProc(_oldWndProc, hWnd, msg, wParam, lParam);
    }

    public void Dispose()
    {
        if (_windowHandle != IntPtr.Zero)
        {
            Win32.UnregisterHotKey(_windowHandle, HotkeyId);
        }
    }
}
