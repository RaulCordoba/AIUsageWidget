namespace AIUsageWidget.Core.Services;

public static class ResetCalculator
{
    public static TimeSpan? Calculate(DateTimeOffset? nextResetAt, DateTimeOffset now)
    {
        if (nextResetAt is null)
        {
            return null;
        }

        var remaining = nextResetAt.Value - now;
        return remaining <= TimeSpan.Zero ? TimeSpan.Zero : remaining;
    }

    public static string FormatRemaining(DateTimeOffset? nextResetAt, DateTimeOffset now)
    {
        if (Calculate(nextResetAt, now) is not { } remaining) return "N/D";
        // Do not show zero minutes while there is still time left, or retain
        // a duration calculated when an older usage snapshot was fetched.
        var rounded = TimeSpan.FromMinutes(Math.Ceiling(remaining.TotalMinutes));
        if (rounded.TotalDays >= 1) return $"{(int)rounded.TotalDays} d {rounded.Hours} h";
        if (rounded.TotalHours >= 1) return $"{(int)rounded.TotalHours} h {rounded.Minutes} min";
        return $"{rounded.Minutes} min";
    }
}
