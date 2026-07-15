namespace AIUsageWidget.Core.Models;

public sealed class UsageHistoryEntry
{
    public DateTimeOffset Timestamp { get; init; }
    public double? UsagePercentage { get; init; }
    public long? RequestsUsed { get; init; }
    public long? TokensUsed { get; init; }
    public decimal? EstimatedCost { get; init; }
    public string? ModelName { get; init; }
    public string? StatusMessage { get; init; }
}
