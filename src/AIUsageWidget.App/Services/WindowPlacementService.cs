using System.Windows;
using AIUsageWidget.Core.Interfaces;
using AIUsageWidget.Core.Models;
using Forms = System.Windows.Forms;
using Rectangle = System.Drawing.Rectangle;

namespace AIUsageWidget.App.Services;

public sealed class WindowPlacementService
{
    private const double TaskbarGap = 10;
    private readonly ISettingsStore _settingsStore;

    public WindowPlacementService(ISettingsStore settingsStore)
    {
        _settingsStore = settingsStore;
    }

    public async Task RestoreAsync(Window window)
    {
        var settings = await _settingsStore.LoadAsync();
        if (settings.WindowLeft is { } left && settings.WindowTop is { } top)
        {
            window.Left = left;
            window.Top = top;
        }
        else
        {
            var area = SystemParameters.WorkArea;
            window.Left = area.Right - window.Width - 8;
            window.Top = area.Bottom - window.Height - TaskbarGap;
        }

        KeepVisible(window);
    }

    public async Task SaveAsync(Window window, AppSettings settings)
    {
        await _settingsStore.SaveAsync(settings with
        {
            WindowLeft = window.Left,
            WindowTop = window.Top
        });
    }

    public async Task DockToTaskbarWidgetAsync(Window window)
    {
        var bounds = Forms.Screen.PrimaryScreen?.WorkingArea ?? new System.Drawing.Rectangle(0, 0, 1280, 720);
        window.Left = Math.Max(bounds.Left, bounds.Right - window.Width - 8);
        window.Top = Math.Max(bounds.Top, bounds.Bottom - window.Height - TaskbarGap);
        KeepVisible(window);

        var settings = await _settingsStore.LoadAsync();
        await SaveAsync(window, settings);
    }

    private static void KeepVisible(Window window)
    {
        var bounds = Forms.Screen.AllScreens.Select(x => x.WorkingArea).FirstOrDefault(rect =>
            rect.Contains((int)window.Left, (int)window.Top));
        if (bounds == Rectangle.Empty)
        {
            bounds = Forms.Screen.PrimaryScreen?.WorkingArea ?? new System.Drawing.Rectangle(0, 0, 1280, 720);
        }

        window.Left = Math.Clamp(window.Left, bounds.Left, Math.Max(bounds.Left, bounds.Right - window.Width));
        window.Top = Math.Clamp(window.Top, bounds.Top, Math.Max(bounds.Top, bounds.Bottom - window.Height));
    }
}
