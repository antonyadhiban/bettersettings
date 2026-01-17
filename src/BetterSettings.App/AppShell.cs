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
    private IntPtr _trayIcon;

    // Menu item IDs
    private const uint MENU_OPEN = 1;
    private const uint MENU_STARTUP = 2;
    private const uint MENU_COMPACT = 3;
    private const uint MENU_DETAILED = 4;
    private const uint MENU_EXIT = 5;

    // Services
    private CatalogLoader? _catalogLoader;
    private PlatformService? _platformService;
    private SettingsDiscoveryService? _discoveryService;
    private IndexCacheService? _cacheService;
    private IndexStore? _indexStore;
    private EmbeddingService? _embeddingService;
    private HybridSearchService? _searchService;
    private LaunchService? _launchService;
    private SettingsService? _settingsService;
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
        _discoveryService = new SettingsDiscoveryService();
        _cacheService = new IndexCacheService();
        _settingsService = new SettingsService();
        _indexStore = new IndexStore(_catalogLoader, _platformService, _discoveryService, _cacheService);

        // Initialize embedding service (with fallback word vectors, no ONNX model for now)
        _embeddingService = new EmbeddingService(_indexStore.Items);

        // Initialize hybrid search combining lexical + semantic
        _searchService = new HybridSearchService(_indexStore, _embeddingService);
        _launchService = new LaunchService();
        _viewModel = new MainViewModel(_searchService, _settingsService);

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

        // Try to load custom icon, fallback to system icon
        _trayIcon = LoadTrayIcon();

        _trayData = new Win32.NOTIFYICONDATA
        {
            cbSize = (uint)Marshal.SizeOf<Win32.NOTIFYICONDATA>(),
            hWnd = hwnd,
            uID = 1,
            uFlags = Win32.NIF_MESSAGE | Win32.NIF_ICON | Win32.NIF_TIP,
            uCallbackMessage = _hotkeyService.TrayMessageId,
            hIcon = _trayIcon,
            szTip = "BetterSettings - Ctrl+Space to open"
        };

        _trayAdded = Win32.Shell_NotifyIcon(Win32.NIM_ADD, ref _trayData);
    }

    private IntPtr LoadTrayIcon()
    {
        // Try to load from app directory first
        var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "icon.ico");
        if (File.Exists(iconPath))
        {
            var icon = Win32.LoadImage(IntPtr.Zero, iconPath, Win32.IMAGE_ICON, 16, 16, Win32.LR_LOADFROMFILE);
            if (icon != IntPtr.Zero) return icon;
        }

        // Create a simple white circle icon programmatically
        return CreateSimpleIcon();
    }

    private IntPtr CreateSimpleIcon()
    {
        const int size = 16;
        var screenDc = Win32.GetDC(IntPtr.Zero);

        try
        {
            // Create color bitmap (white circle)
            var colorDc = Win32.CreateCompatibleDC(screenDc);
            var colorBitmap = Win32.CreateCompatibleBitmap(screenDc, size, size);
            var oldColorBmp = Win32.SelectObject(colorDc, colorBitmap);

            // Fill with white circle
            var whiteBrush = Win32.CreateSolidBrush(Win32.RGB(255, 255, 255));
            var oldBrush = Win32.SelectObject(colorDc, whiteBrush);
            Win32.Ellipse(colorDc, 1, 1, size - 1, size - 1);
            Win32.SelectObject(colorDc, oldBrush);
            Win32.DeleteObject(whiteBrush);

            Win32.SelectObject(colorDc, oldColorBmp);
            Win32.DeleteDC(colorDc);

            // Create mask bitmap (black circle on white = transparent outside)
            var maskDc = Win32.CreateCompatibleDC(screenDc);
            var maskBitmap = Win32.CreateCompatibleBitmap(screenDc, size, size);
            var oldMaskBmp = Win32.SelectObject(maskDc, maskBitmap);

            // Fill background white (transparent), circle black (opaque)
            var blackBrush = Win32.CreateSolidBrush(Win32.RGB(0, 0, 0));
            whiteBrush = Win32.CreateSolidBrush(Win32.RGB(255, 255, 255));

            // Fill all white first
            oldBrush = Win32.SelectObject(maskDc, whiteBrush);
            Win32.Ellipse(maskDc, -size, -size, size * 2, size * 2); // Fill whole area

            // Draw black circle (opaque area)
            Win32.SelectObject(maskDc, blackBrush);
            Win32.Ellipse(maskDc, 1, 1, size - 1, size - 1);

            Win32.SelectObject(maskDc, oldBrush);
            Win32.DeleteObject(blackBrush);
            Win32.DeleteObject(whiteBrush);

            Win32.SelectObject(maskDc, oldMaskBmp);
            Win32.DeleteDC(maskDc);

            // Create icon
            var iconInfo = new Win32.ICONINFO
            {
                fIcon = true,
                xHotspot = 0,
                yHotspot = 0,
                hbmMask = maskBitmap,
                hbmColor = colorBitmap
            };

            var icon = Win32.CreateIconIndirect(ref iconInfo);

            // Cleanup bitmaps
            Win32.DeleteObject(colorBitmap);
            Win32.DeleteObject(maskBitmap);

            return icon;
        }
        finally
        {
            Win32.ReleaseDC(IntPtr.Zero, screenDc);
        }
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
            ShowTrayMenu();
        }
    }

    private void ShowTrayMenu()
    {
        var hwnd = _hotkeyService.WindowHandle;
        var menu = Win32.CreatePopupMenu();

        if (menu == IntPtr.Zero) return;

        try
        {
            // Open
            Win32.AppendMenu(menu, Win32.MF_STRING, MENU_OPEN, "Open BetterSettings");
            Win32.AppendMenu(menu, Win32.MF_SEPARATOR, 0, string.Empty);

            // Display mode submenu items
            var compactFlag = _settingsService?.DisplayMode == DisplayMode.Compact ? Win32.MF_CHECKED : Win32.MF_UNCHECKED;
            var detailedFlag = _settingsService?.DisplayMode == DisplayMode.Detailed ? Win32.MF_CHECKED : Win32.MF_UNCHECKED;
            Win32.AppendMenu(menu, Win32.MF_STRING | compactFlag, MENU_COMPACT, "Compact View");
            Win32.AppendMenu(menu, Win32.MF_STRING | detailedFlag, MENU_DETAILED, "Detailed View");
            Win32.AppendMenu(menu, Win32.MF_SEPARATOR, 0, string.Empty);

            // Load on startup
            var startupFlag = _settingsService?.LoadOnStartup == true ? Win32.MF_CHECKED : Win32.MF_UNCHECKED;
            Win32.AppendMenu(menu, Win32.MF_STRING | startupFlag, MENU_STARTUP, "Load on Startup");
            Win32.AppendMenu(menu, Win32.MF_SEPARATOR, 0, string.Empty);

            // Exit
            Win32.AppendMenu(menu, Win32.MF_STRING, MENU_EXIT, "Exit");

            // Get cursor position
            Win32.GetCursorPos(out var point);

            // Need to set foreground window for the menu to work properly
            Win32.SetForegroundWindow(hwnd);

            // Show menu and get selection
            var cmd = Win32.TrackPopupMenu(menu, 
                Win32.TPM_RETURNCMD | Win32.TPM_NONOTIFY | Win32.TPM_RIGHTBUTTON | Win32.TPM_BOTTOMALIGN,
                point.X, point.Y, 0, hwnd, IntPtr.Zero);

            HandleMenuCommand((uint)cmd);
        }
        finally
        {
            Win32.DestroyMenu(menu);
        }
    }

    private void HandleMenuCommand(uint cmd)
    {
        switch (cmd)
        {
            case MENU_OPEN:
                ShowWindow();
                break;
            case MENU_STARTUP:
                if (_settingsService != null)
                {
                    _settingsService.LoadOnStartup = !_settingsService.LoadOnStartup;
                }
                break;
            case MENU_COMPACT:
                if (_settingsService != null)
                {
                    _settingsService.DisplayMode = DisplayMode.Compact;
                    _viewModel?.NotifyDisplayModeChanged();
                }
                break;
            case MENU_DETAILED:
                if (_settingsService != null)
                {
                    _settingsService.DisplayMode = DisplayMode.Detailed;
                    _viewModel?.NotifyDisplayModeChanged();
                }
                break;
            case MENU_EXIT:
                ExitApp();
                break;
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
        _searchService?.Dispose();
        if (_trayAdded)
        {
            Win32.Shell_NotifyIcon(Win32.NIM_DELETE, ref _trayData);
        }
    }
}
