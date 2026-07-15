using System.Drawing;
using System.Windows;
using AIUsageWidget.Core.Models;
using Application = System.Windows.Application;
using Forms = System.Windows.Forms;

namespace AIUsageWidget.App.Services;

public sealed class TrayIconService : IDisposable
{
    private readonly UsageMonitorService _monitorService;
    private readonly WindowPlacementService _placementService;
    private Forms.NotifyIcon? _notifyIcon;
    private Window? _window;

    public TrayIconService(UsageMonitorService monitorService, WindowPlacementService placementService)
    {
        _monitorService = monitorService;
        _placementService = placementService;
        _monitorService.SnapshotUpdated += OnSnapshotUpdated;
        _monitorService.AlertRaised += OnAlertRaised;
    }

    public void Attach(Window window)
    {
        _window = window;
        _notifyIcon = new Forms.NotifyIcon
        {
            Icon = SystemIcons.Application,
            Text = "AI Usage Widget",
            Visible = true,
            ContextMenuStrip = BuildMenu()
        };
        _notifyIcon.DoubleClick += (_, _) => ShowWindow();
    }

    public void Dispose()
    {
        _monitorService.SnapshotUpdated -= OnSnapshotUpdated;
        _monitorService.AlertRaised -= OnAlertRaised;
        _notifyIcon?.Dispose();
    }

    private Forms.ContextMenuStrip BuildMenu()
    {
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Mostrar widget", null, (_, _) => ShowWindow());
        menu.Items.Add("Ocultar widget", null, (_, _) => Application.Current.Dispatcher.Invoke(() => _window?.Hide()));
        menu.Items.Add("Colocar junto a barra de tareas", null, (_, _) => DockToTaskbarWidget());
        menu.Items.Add("Actualizar ahora", null, (_, _) => _monitorService.RequestRefresh());
        menu.Items.Add("Salir", null, (_, _) => Application.Current.Dispatcher.Invoke(Application.Current.Shutdown));
        return menu;
    }

    private void ShowWindow()
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            _window?.Show();
            _window?.Activate();
        });
    }

    private void DockToTaskbarWidget()
    {
        Application.Current.Dispatcher.Invoke(async () =>
        {
            if (_window is null)
            {
                return;
            }

            _window.Show();
            await _placementService.DockToTaskbarWidgetAsync(_window);
            _window.Activate();
        });
    }

    private void OnSnapshotUpdated(object? sender, UsageSnapshot snapshot)
    {
        if (_notifyIcon is null)
        {
            return;
        }

        var percentage = snapshot.PrimaryUsagePercentage is { } value ? $"{value:0.#} %" : "N/D";
        var reset = snapshot.TimeUntilReset is { } remaining ? $"Reset en {FormatRemaining(remaining)}" : "Reset no disponible";
        _notifyIcon.Text = $"{snapshot.ProviderName}: {percentage}\n{reset}"[..Math.Min(63, $"{snapshot.ProviderName}: {percentage}\n{reset}".Length)];
    }

    private void OnAlertRaised(object? sender, (UsageSnapshot Snapshot, int Threshold) alert)
    {
        _notifyIcon?.ShowBalloonTip(
            7000,
            "AI Usage Widget",
            $"{alert.Snapshot.ProviderName} ha alcanzado el {alert.Threshold} % del limite actual.",
            Forms.ToolTipIcon.Warning);
    }

    private static string FormatRemaining(TimeSpan remaining)
    {
        if (remaining.TotalDays >= 1)
        {
            return $"{(int)remaining.TotalDays} d {remaining.Hours} h";
        }

        if (remaining.TotalHours >= 1)
        {
            return $"{(int)remaining.TotalHours} h {remaining.Minutes} min";
        }

        return $"{Math.Max(0, remaining.Minutes)} min";
    }
}
