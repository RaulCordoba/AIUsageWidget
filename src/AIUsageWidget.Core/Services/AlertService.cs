using AIUsageWidget.Core.Models;

namespace AIUsageWidget.Core.Services;

public sealed class AlertService
{
    public AlertDecision Evaluate(UsageSnapshot snapshot, AppSettings settings, NotificationState state)
    {
        if (!settings.NotificationsEnabled || snapshot.PrimaryUsagePercentage is not { } percentage)
        {
            return new AlertDecision(false, null, BuildCycleKey(snapshot));
        }

        var cycleKey = BuildCycleKey(snapshot);
        var shown = state.CycleKey == cycleKey
            ? state.ShownThresholds.ToHashSet()
            : [];

        var threshold = settings.AlertThresholds
            .Where(x => percentage >= x && !shown.Contains(x))
            .OrderDescending()
            .FirstOrDefault();

        return threshold == 0
            ? new AlertDecision(false, null, cycleKey)
            : new AlertDecision(true, threshold, cycleKey);
    }

    public NotificationState MarkShown(NotificationState state, AlertDecision decision)
    {
        if (!decision.ShouldNotify || decision.Threshold is null)
        {
            return state;
        }

        var shown = state.CycleKey == decision.CycleKey
            ? state.ShownThresholds.ToHashSet()
            : [];

        foreach (var threshold in Enumerable.Range(1, decision.Threshold.Value))
        {
            shown.Add(threshold);
        }
        return new NotificationState
        {
            CycleKey = decision.CycleKey,
            ShownThresholds = shown.Order().ToArray()
        };
    }

    private static string BuildCycleKey(UsageSnapshot snapshot)
    {
        var reset = snapshot.NextResetAt?.ToUnixTimeSeconds().ToString() ?? "unknown";
        return $"{snapshot.ProviderId}:{reset}";
    }
}
