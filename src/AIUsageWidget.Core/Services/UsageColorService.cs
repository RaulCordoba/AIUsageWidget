using AIUsageWidget.Core.Models;

namespace AIUsageWidget.Core.Services;

public sealed class UsageColorService
{
    public string GetColor(double? percentage, UsageColorThresholds thresholds)
    {
        if (percentage is null)
        {
            return "#6B7280";
        }

        if (percentage >= 100)
        {
            return thresholds.CriticalColor;
        }

        if (percentage >= 80)
        {
            return thresholds.HighColor;
        }

        if (percentage >= 40)
        {
            return thresholds.WarningColor;
        }

        return thresholds.NormalColor;
    }
}
