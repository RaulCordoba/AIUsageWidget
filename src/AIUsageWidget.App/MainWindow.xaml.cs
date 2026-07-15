using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Input;
using System.Windows.Threading;
using AIUsageWidget.App.Services;
using AIUsageWidget.App.ViewModels;

namespace AIUsageWidget.App;

public partial class MainWindow : Window
{
    private const int GwlExStyle = -20;
    private const int WsExToolWindow = 0x00000080;
    private const uint SwpNoMove = 0x0002;
    private const uint SwpNoSize = 0x0001;
    private const uint SwpNoActivate = 0x0010;
    private static readonly IntPtr HwndTopmost = new(-1);

    private readonly MainViewModel _viewModel;
    private readonly WindowPlacementService _placementService;
    private readonly DispatcherTimer _topmostTimer = new();
    private bool _isDragging;
    private bool _placementRestored;

    public MainWindow(MainViewModel viewModel, WindowPlacementService placementService)
    {
        _viewModel = viewModel;
        _placementService = placementService;
        DataContext = viewModel;
        InitializeComponent();
        _topmostTimer.Interval = TimeSpan.FromSeconds(1);
        _topmostTimer.Tick += (_, _) => ReassertTopmost();
    }

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 1 && e.ButtonState == MouseButtonState.Pressed)
        {
            _isDragging = true;
            DragMove();
            _isDragging = false;
            _ = SavePlacementAsync();
        }
    }

    private void Window_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        _isDragging = false;
        _ = SavePlacementAsync();
    }

    private void Window_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        _viewModel.ToggleModeCommand.Execute(null);
    }

    private async void Window_Closing(object? sender, CancelEventArgs e)
    {
        var minimizeToTray = await _viewModel.HandleClosingAsync();
        e.Cancel = minimizeToTray;
        if (minimizeToTray)
        {
            Hide();
        }
        else
        {
            _topmostTimer.Stop();
        }
    }

    private async void Window_SourceInitialized(object sender, EventArgs e)
    {
        ApplyToolWindowStyle();
        ReassertTopmost();
        _topmostTimer.Start();
        await _placementService.RestoreAsync(this);
        _placementRestored = true;
    }

    private async void DockToTaskbarWidget_Click(object sender, RoutedEventArgs e)
    {
        await _placementService.DockToTaskbarWidgetAsync(this);
        ReassertTopmost();
    }

    private void Window_LocationChanged(object? sender, EventArgs e)
    {
        if (_placementRestored && !_isDragging)
        {
            _ = SavePlacementAsync();
        }
    }

    private void Window_Deactivated(object? sender, EventArgs e)
    {
        ReassertTopmost();
    }

    private void Window_StateChanged(object? sender, EventArgs e)
    {
        if (WindowState == WindowState.Minimized)
        {
            WindowState = WindowState.Normal;
        }

        ReassertTopmost();
    }

    private void ApplyToolWindowStyle()
    {
        var handle = new WindowInteropHelper(this).Handle;
        if (handle == IntPtr.Zero)
        {
            return;
        }

        var style = GetWindowLong(handle, GwlExStyle);
        SetWindowLong(handle, GwlExStyle, style | WsExToolWindow);
    }

    private void ReassertTopmost()
    {
        Topmost = true;
        var handle = new WindowInteropHelper(this).Handle;
        if (handle != IntPtr.Zero)
        {
            SetWindowPos(handle, HwndTopmost, 0, 0, 0, 0, SwpNoMove | SwpNoSize | SwpNoActivate);
        }
    }

    private async Task SavePlacementAsync()
    {
        if (!_placementRestored || WindowState != WindowState.Normal)
        {
            return;
        }

        await _viewModel.SaveWindowPlacementAsync(Left, Top);
    }

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint flags);
}
