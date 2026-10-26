using AIUsageWidget.App.Services;
using AIUsageWidget.App.ViewModels;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Avalonia.Threading;

namespace AIUsageWidget.App;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel = null!;
    private readonly WindowPlacementService _placement = null!;
    private bool _restored;
    private readonly DispatcherTimer _positionSaveTimer = new() { Interval = TimeSpan.FromMilliseconds(300) };
    public MainWindow() => InitializeComponent();
    public MainWindow(MainViewModel viewModel, WindowPlacementService placement) : this()
    {
        _viewModel = viewModel;
        _placement = placement;
        DataContext = viewModel;
        Opened += async (_, _) => { await _placement.RestoreAsync(this); _restored = true; };
        _positionSaveTimer.Tick += async (_, _) =>
        {
            _positionSaveTimer.Stop();
            try { await _viewModel.SaveWindowPlacementAsync(Position.X, Position.Y); }
            catch (Exception) { System.Diagnostics.Trace.TraceWarning("No se pudo guardar la posición del widget."); }
        };
        PositionChanged += (_, _) =>
        {
            if (!_restored) return;
            _positionSaveTimer.Stop();
            _positionSaveTimer.Start();
        };
        Screens.Changed += OnScreensChanged;
        PropertyChanged += (_, args) =>
        {
            if (_restored && (args.Property == WidthProperty || args.Property == HeightProperty))
                Dispatcher.UIThread.Post(() => WindowPlacementService.KeepVisible(this));
        };
        Closed += (_, _) => { _positionSaveTimer.Stop(); Screens.Changed -= OnScreensChanged; };
        Closing += (_, e) =>
        {
            if (e.CloseReason != WindowCloseReason.ApplicationShutdown && viewModel.Settings.MinimizeToTray)
            {
                e.Cancel = true;
                Hide();
            }
            else if (e.CloseReason != WindowCloseReason.ApplicationShutdown &&
                     App.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.Shutdown();
            }
        };
    }

    private void OnScreensChanged(object? sender, EventArgs e)
    {
        if (_restored) WindowPlacementService.KeepVisible(this);
    }

    private void Window_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        // Buttons, sliders and menus keep their normal click behavior.
        for (var control = e.Source as Avalonia.Visual; control is not null; control = control.GetVisualParent())
            if (control is Button or SelectingItemsControl or TextBox or RangeBase) return;
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        if (e.ClickCount == 2) _viewModel.ToggleModeCommand.Execute(null);
        else BeginMoveDrag(e);
    }

    private async void DockToTaskbarWidget_Click(object? sender, RoutedEventArgs e) => await _placement.DockAsync(this);
}
