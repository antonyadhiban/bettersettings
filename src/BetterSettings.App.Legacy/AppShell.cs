using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;

namespace BetterSettings;

public sealed class AppShell : IDisposable
{
    private readonly IServiceProvider _services;
    private readonly HotkeyService _hotkeyService;
    private MainWindow? _window;
    private bool _isVisible;

    public AppShell(IServiceProvider services, HotkeyService hotkeyService)
    {
        _services = services;
        _hotkeyService = hotkeyService;
    }

    public void Initialize()
    {
        if (_window != null)
        {
            return;
        }

        var viewModel = _services.GetRequiredService<MainViewModel>();
        var launchService = _services.GetRequiredService<LaunchService>();

        _window = new MainWindow(viewModel, launchService, HideWindow);
        _window.Closed += (_, _) => Dispose();

        _window.Activate();
        _isVisible = true;

        _hotkeyService.RegisterToggleHotkey(_window, ToggleVisibility);
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
        _isVisible = true;
        _window.FocusSearch();
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

    public void Dispose()
    {
        _hotkeyService.Dispose();
    }
}
