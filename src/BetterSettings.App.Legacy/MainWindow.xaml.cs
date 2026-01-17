using System;
using System.Threading.Tasks;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Windows.System;
using WinRT.Interop;

namespace BetterSettings;

public sealed partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private readonly LaunchService _launchService;
    private readonly Action _requestHide;

    public MainWindow(MainViewModel viewModel, LaunchService launchService, Action requestHide)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _launchService = launchService;
        _requestHide = requestHide;
        DataContext = _viewModel;

        Activated += (_, _) => FocusSearch();
        ConfigureWindow();
    }

    public void FocusSearch()
    {
        SearchBox.Focus(FocusState.Programmatic);
        SearchBox.SelectAll();
    }

    public void ShowWindow()
    {
        var hwnd = WindowNative.GetWindowHandle(this);
        Win32.ShowWindow(hwnd, Win32.SW_SHOW);
        Win32.SetForegroundWindow(hwnd);
    }

    public void HideWindow()
    {
        var hwnd = WindowNative.GetWindowHandle(this);
        Win32.ShowWindow(hwnd, Win32.SW_HIDE);
    }

    private void ConfigureWindow()
    {
        var hwnd = WindowNative.GetWindowHandle(this);
        var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
        var appWindow = AppWindow.GetFromWindowId(windowId);
        appWindow.Resize(new Windows.Graphics.SizeInt32(720, 420));
        appWindow.Title = "BetterSettings";
    }

    private async void Root_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        switch (e.Key)
        {
            case VirtualKey.Down:
                _viewModel.SelectNext();
                e.Handled = true;
                break;
            case VirtualKey.Up:
                _viewModel.SelectPrevious();
                e.Handled = true;
                break;
            case VirtualKey.Enter:
                e.Handled = true;
                await LaunchSelectedAsync();
                break;
            case VirtualKey.Escape:
                e.Handled = true;
                if (_viewModel.HasQuery)
                {
                    _viewModel.ClearQuery();
                }
                else
                {
                    _requestHide();
                }
                break;
        }
    }

    private async Task LaunchSelectedAsync()
    {
        var selected = _viewModel.SelectedResult;
        if (selected == null)
        {
            return;
        }

        var result = await _launchService.LaunchAsync(selected.Item);
        _viewModel.StatusMessage = result.ErrorMessage ?? string.Empty;

        if (result.Succeeded)
        {
            _requestHide();
        }
    }
}
