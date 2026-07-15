using AIUsageWidget.Core.Models;

namespace AIUsageWidget.Core.Services;

public sealed class HistoryReductionService
{
    public bool ShouldStore(UsageHistoryEntry? previous, UsageSnapshot snapshot, TimeSpan maxInterval)
    {
        if (previous is null)
        {
            return true;
        }

        if (snapshot.RetrievedAt - previous.Timestamp >= maxInterval)
        {
            return true;
        }

        return !NullableEquals(previous.UsagePercentage, snapshot.PrimaryUsagePercentage)
               || previous.RequestsUsed != snapshot.RequestsUsed
               || previous.TokensUsed != snapshot.TokensUsed
               || previous.EstimatedCost != snapshot.EstimatedCost
               || !string.Equals(previous.ModelName, snapshot.ModelName, StringComparison.Ordinal)
               || !string.Equals(previous.StatusMessage, snapshot.StatusMessage, StringComparison.Ordinal);
    }

    public UsageHistoryEntry ToEntry(UsageSnapshot snapshot) => new()
    {
        Timestamp = snapshot.RetrievedAt,
        UsagePercentage = snapshot.PrimaryUsagePercentage,
        RequestsUsed = snapshot.RequestsUsed,
        TokensUsed = snapshot.TokensUsed,
        EstimatedCost = snapshot.EstimatedCost,
        ModelName = snapshot.ModelName,
        StatusMessage = snapshot.StatusMessage
    };

    private static bool NullableEquals(double? left, double? right)
    {
        if (left is null || right is null)
        {
            return left == right;
        }

        return Math.Abs(left.Value - right.Value) < 0.01;
    }
}
