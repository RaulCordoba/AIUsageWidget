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
}
