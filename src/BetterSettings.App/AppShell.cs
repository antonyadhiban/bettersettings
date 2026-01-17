using System;
using System.Runtime.InteropServices;

namespace BetterSettings.App;

public sealed class AppShell : IDisposable
{
    private readonly HotkeyService _hotkeyService = new();
    private MainWindow? _window;
    private Win32.NOTIFYICONDATA _trayData;
    private bool _trayAdded;
    private bool _isVisible;

    // Services
    private CatalogLoader? _catalogLoader;
    private PlatformService? _platformService;
    private IndexStore? _indexStore;
    private SearchService? _searchService;
    private LaunchService? _launchService;
    private MainViewModel? _viewModel;

    public void Initialize()
    {
        if (_window != null)
        {
            return;
        }

        // Initialize services
        _catalogLoader = new CatalogLoader();
        _platformService = new PlatformService();
        _indexStore = new IndexStore(_catalogLoader, _platformService);
        _searchService = new SearchService(_indexStore);
        _launchService = new LaunchService();
        _viewModel = new MainViewModel(_searchService);

        // Create window with services
        _window = new MainWindow(_viewModel, _launchService);
        _window.SetHideCallback(HideWindow);
        _window.HideWindow();
        _isVisible = false;

        _hotkeyService.RegisterToggleHotkey(_window, ToggleVisibility);
        _hotkeyService.RegisterTrayCallback(HandleTrayMessage);
        InitializeTray();
    }

    private void InitializeTray()
    {
        var hwnd = _hotkeyService.WindowHandle;
        if (hwnd == IntPtr.Zero)
        {
            return;
        }

        _trayData = new Win32.NOTIFYICONDATA
        {
            cbSize = (uint)Marshal.SizeOf<Win32.NOTIFYICONDATA>(),
            hWnd = hwnd,
            uID = 1,
            uFlags = Win32.NIF_MESSAGE | Win32.NIF_ICON | Win32.NIF_TIP,
            uCallbackMessage = _hotkeyService.TrayMessageId,
            hIcon = Win32.LoadIcon(IntPtr.Zero, (IntPtr)Win32.IDI_APPLICATION),
            szTip = "BetterSettings - Ctrl+Space to open"
        };

        _trayAdded = Win32.Shell_NotifyIcon(Win32.NIM_ADD, ref _trayData);
    }

    private void HandleTrayMessage(uint message)
    {
        if (message == Win32.WM_LBUTTONUP || message == Win32.WM_LBUTTONDBLCLK)
        {
            ToggleVisibility();
            return;
        }

        if (message == Win32.WM_RBUTTONUP)
        {
            ExitApp();
        }
    }

    private void ToggleVisibility()
    {
        if (_window == null)
        {
            return;
        }

        if (_isVisible)
        {
            HideWindow();
        }
        else
        {
            ShowWindow();
        }
    }

    private void ShowWindow()
    {
        if (_window == null)
        {
            return;
        }

        _window.ShowWindow();
        _window.FocusSearch();
        _isVisible = true;
    }

    private void HideWindow()
    {
        if (_window == null)
        {
            return;
        }

        _window.HideWindow();
        _isVisible = false;
    }

    private void ExitApp()
    {
        if (_window == null)
        {
            return;
        }

        _window.AllowClose = true;
        _window.Close();
    }

    public void Dispose()
    {
        _hotkeyService.Dispose();
        if (_trayAdded)
        {
            Win32.Shell_NotifyIcon(Win32.NIM_DELETE, ref _trayData);
        }
    }
}
