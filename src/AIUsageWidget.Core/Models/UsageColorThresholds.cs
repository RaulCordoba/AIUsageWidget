namespace AIUsageWidget.Core.Models;

public sealed record UsageColorThresholds
{
    public double WarningAt { get; set; } = 40;
    public double HighAt { get; set; } = 80;
    public double CriticalAt { get; set; } = 100;
    public string NormalColor { get; set; } = "#36D399";
    public string WarningColor { get; set; } = "#3B82F6";
    public string HighColor { get; set; } = "#FB923C";
    public string CriticalColor { get; set; } = "#EF4444";
}
