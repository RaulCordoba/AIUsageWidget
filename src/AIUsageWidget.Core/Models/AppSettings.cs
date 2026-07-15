namespace AIUsageWidget.Core.Models;

public sealed record AppSettings
{
    public int SchemaVersion { get; set; } = 1;
    public string SelectedProviderId { get; set; } = "demo";
    public int RefreshIntervalSeconds { get; set; } = 30;
    public string Theme { get; set; } = "Dark";
    public double Opacity { get; set; } = 0.95;
    public double Scale { get; set; } = 1.0;
    public bool AlwaysOnTop { get; set; } = true;
    public bool CompactMode { get; set; } = true;
    public bool ShowInTaskbar { get; set; }
    public bool ShowTrayIcon { get; set; } = true;
    public bool MinimizeToTray { get; set; } = true;
    public bool StartWithWindows { get; set; }
    public bool NotificationsEnabled { get; set; } = true;
    public bool HistoryEnabled { get; set; } = true;
    public int HistoryRetentionDays { get; set; } = 90;
    public string Language { get; set; } = "es-ES";
    public string TimeFormat { get; set; } = "HH:mm";
    public double? WindowLeft { get; set; }
    public double? WindowTop { get; set; }
    public string? MonitorDeviceName { get; set; }
    public IReadOnlyList<int> AlertThresholds { get; set; } = [80, 90, 95, 100];
    public UsageColorThresholds ColorThresholds { get; set; } = new();

    public AppSettings Validate()
    {
        var interval = Math.Clamp(RefreshIntervalSeconds, 10, 3600);
        var opacity = Math.Clamp(Opacity, 0.35, 1.0);
        var scale = Math.Clamp(Scale, 0.75, 2.0);
        var retention = Math.Clamp(HistoryRetentionDays, 1, 3650);
        var thresholds = AlertThresholds
            .Where(x => x is > 0 and <= 100)
            .Distinct()
            .Order()
            .ToArray();

        return this with
        {
            RefreshIntervalSeconds = interval,
            Opacity = opacity,
            Scale = scale,
            HistoryRetentionDays = retention,
            AlertThresholds = thresholds.Length == 0 ? [80, 90, 95, 100] : thresholds
        };
    }

}
