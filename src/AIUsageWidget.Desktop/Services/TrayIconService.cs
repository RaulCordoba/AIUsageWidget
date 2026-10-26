using AIUsageWidget.Core.Models;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform;
using Avalonia.Threading;
using Microsoft.Extensions.Logging;

namespace AIUsageWidget.App.Services;
public sealed class TrayIconService : IDisposable
{
    private readonly TrayIcon _icon;
    private readonly MainWindow _window;
    private readonly UsageMonitorService _monitor;
    private readonly MacHelperClient _helper;
    private readonly ILogger _logger;
    private readonly WindowPlacementService _placement;
    public TrayIconService(MainWindow window, UsageMonitorService monitor, MacHelperClient helper,
        ILogger<TrayIconService> logger, IClassicDesktopStyleApplicationLifetime desktop, WindowPlacementService placement)
    {
        _window = window; _monitor = monitor; _helper = helper; _logger = logger; _placement = placement;
        var menu = new NativeMenu();
        Add("Mostrar widget", () => { window.Show(); window.Activate(); });
        Add("Ocultar widget", window.Hide);
        Add("Alternar compacto/expandido", () => ((ViewModels.MainViewModel)window.DataContext!).ToggleModeCommand.Execute(null));
        Add("Colocar junto al Dock", () => DockAsync());
        Add("Actualizar ahora", monitor.RequestRefresh);
        Add("Configuracion", () => ((ViewModels.MainViewModel)window.DataContext!).OpenSettingsCommand.Execute(null));
        menu.Items.Add(new NativeMenuItemSeparator());
        Add("Salir", () => desktop.Shutdown());
        using var stream = AssetLoader.Open(new Uri("avares://AIUsageWidget.Desktop/Assets/tray.png"));
        _icon = new TrayIcon { Icon = new WindowIcon(stream), ToolTipText = "AI Usage Widget", Menu = menu };
        TrayIcon.SetIcons(App.Current!, new TrayIcons { _icon });
        _monitor.SnapshotUpdated += OnSnapshot;
        _monitor.AlertRaised += OnAlert;

        void Add(string text, Action action)
        {
            var item = new NativeMenuItem(text);
            item.Click += (_, _) => action();
            menu.Items.Add(item);
        }
    }

    private async void DockAsync()
    {
        try
        {
            _window.Show();
            // The placement command is also exposed through the window context menu.
            await _placement.DockAsync(_window);
            _window.Activate();
        }
        catch (Exception) { _logger.LogWarning("No se pudo guardar la posición del widget."); }
    }

    private void OnSnapshot(object? sender, UsageSnapshot snapshot) => Dispatcher.UIThread.Post(() =>
        _icon.ToolTipText = $"{snapshot.ProviderName}: {snapshot.PrimaryUsagePercentage:0.#} % — {snapshot.StatusMessage}");

    private async void OnAlert(object? sender, (UsageSnapshot Snapshot, int Threshold) alert)
    {
        try
        {
            await _helper.InvokeAsync(new { operation = "notify", title = "AI Usage Widget",
                body = $"{alert.Snapshot.ProviderName} ha alcanzado el {alert.Threshold} % del limite actual." });
        }
        catch (Exception) { _logger.LogWarning("No se pudo mostrar la notificación de macOS. Comprueba los permisos de notificaciones."); }
    }

    public void Dispose()
    {
        _monitor.SnapshotUpdated -= OnSnapshot;
        _monitor.AlertRaised -= OnAlert;
        _icon.Dispose();
    }
}
