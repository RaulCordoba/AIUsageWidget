using AIUsageWidget.Core.Interfaces;
using Avalonia;
using Avalonia.Controls;

namespace AIUsageWidget.App.Services;
public sealed class WindowPlacementService(ISettingsStore settingsStore)
{
    public async Task RestoreAsync(Window window)
    {
        var settings = await settingsStore.LoadAsync();
        if (settings.WindowLeft is { } x && settings.WindowTop is { } y && double.IsFinite(x) && double.IsFinite(y))
            window.Position = new PixelPoint((int)x, (int)y);
        else
            Dock(window);
        KeepVisible(window);
    }

    public async Task DockAsync(Window window)
    {
        Dock(window);
        var settings = await settingsStore.LoadAsync();
        await settingsStore.SaveAsync(settings with { WindowLeft = window.Position.X, WindowTop = window.Position.Y });
    }

    private static void Dock(Window window)
    {
        var screen = window.Screens.ScreenFromWindow(window) ?? window.Screens.Primary;
        if (screen is null) return;
        var area = screen.WorkingArea;
        window.Position = new PixelPoint(area.Right - (int)(window.Width * screen.Scaling) - 8,
            area.Bottom - (int)(window.Height * screen.Scaling) - 10);
        KeepVisible(window);
    }

    internal static void KeepVisible(Window window)
    {
        var screen = window.Screens.ScreenFromPoint(window.Position) ?? window.Screens.Primary;
        if (screen is null) return;
        var area = screen.WorkingArea;
        window.Position = new PixelPoint(
            Math.Clamp(window.Position.X, area.X, Math.Max(area.X, area.Right - (int)(window.Width * screen.Scaling))),
            Math.Clamp(window.Position.Y, area.Y, Math.Max(area.Y, area.Bottom - (int)(window.Height * screen.Scaling))));
    }
}
