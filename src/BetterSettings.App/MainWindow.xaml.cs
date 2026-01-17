using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.Graphics;
using Windows.System;
using WinRT.Interop;
using BetterSettings.App.Models;
using System.Runtime.InteropServices;

namespace BetterSettings.App;

public sealed partial class MainWindow : Window
{
    private AppWindow? _appWindow;
    private readonly LaunchService _launchService;
    private Action? _requestHide;

    public MainViewModel ViewModel { get; }

    public MainWindow(MainViewModel viewModel, LaunchService launchService)
    {
        ViewModel = viewModel;
        _launchService = launchService;

        InitializeComponent();
        Activated += OnActivated;
        ConfigureWindow();
    }

    public bool AllowClose { get; set; }

    public void SetHideCallback(Action requestHide)
    {
        _requestHide = requestHide;
    }

    private void OnActivated(object sender, WindowActivatedEventArgs e)
    {
        if (e.WindowActivationState != WindowActivationState.Deactivated)
        {
            FocusSearch();
        }
    }

    public void FocusSearch()
    {
        SearchBox.Focus(FocusState.Programmatic);
        SearchBox.SelectAll();
    }

    public void ShowWindow()
    {
        // Re-center before showing
        CenterWindow(580, 420);

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
        _appWindow = AppWindow.GetFromWindowId(windowId);
        _appWindow.Title = "BetterSettings";

        // Get the presenter and configure for borderless look
        var presenter = OverlappedPresenter.Create();
        presenter.IsResizable = false;
        presenter.IsMaximizable = false;
        presenter.IsMinimizable = false;
        presenter.SetBorderAndTitleBar(false, false);
        _appWindow.SetPresenter(presenter);

        // Set window size and position
        var windowWidth = 580;
        var windowHeight = 420;
        _appWindow.Resize(new SizeInt32(windowWidth, windowHeight));

        // Center on primary display, slightly above center
        CenterWindow(windowWidth, windowHeight);

        _appWindow.Closing += (_, e) =>
        {
            if (!AllowClose)
            {
                e.Cancel = true;
                HideWindow();
            }
        };
    }

    private void CenterWindow(int width, int height)
    {
        var displayArea = DisplayArea.GetFromWindowId(
            Win32Interop.GetWindowIdFromWindow(WindowNative.GetWindowHandle(this)),
            DisplayAreaFallback.Primary);

        if (displayArea != null)
        {
            var workArea = displayArea.WorkArea;
            var centerX = (workArea.Width - width) / 2 + workArea.X;
            // Slightly above center (30% from top instead of 50%)
            var centerY = (int)((workArea.Height - height) * 0.30) + workArea.Y;
            _appWindow?.Move(new PointInt32(centerX, centerY));
        }
    }

    private void Root_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        switch (e.Key)
        {
            case VirtualKey.Down:
                ViewModel.SelectNext();
                e.Handled = true;
                ScrollToSelected();
                break;
            case VirtualKey.Up:
                ViewModel.SelectPrevious();
                e.Handled = true;
                ScrollToSelected();
                break;
            case VirtualKey.Enter:
                e.Handled = true;
                _ = LaunchSelectedAsync();
                break;
            case VirtualKey.Escape:
                e.Handled = true;
                if (ViewModel.HasQuery)
                {
                    ViewModel.ClearQuery();
                }
                else
                {
                    _requestHide?.Invoke();
                }
                break;
        }
    }

    private void ScrollToSelected()
    {
        if (ViewModel.SelectedResult != null)
        {
            ResultsList.ScrollIntoView(ViewModel.SelectedResult);
        }
    }

    private void ResultsList_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is SearchResult result)
        {
            ViewModel.SelectedResult = result;
            _ = LaunchSelectedAsync();
        }
    }

    private async Task LaunchSelectedAsync()
    {
        var selected = ViewModel.SelectedResult;
        if (selected == null)
        {
            return;
        }

        var result = await _launchService.LaunchAsync(selected.Item);
        ViewModel.StatusMessage = result.ErrorMessage ?? string.Empty;

        if (result.Succeeded)
        {
            _requestHide?.Invoke();
        }
    }
}
