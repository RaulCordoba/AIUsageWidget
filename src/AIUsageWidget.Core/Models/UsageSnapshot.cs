namespace AIUsageWidget.Core.Models;

public sealed class UsageSnapshot
{
    public required string ProviderId { get; init; }
    public required string ProviderName { get; init; }
    public string? ModelName { get; init; }
    public double? SessionUsagePercentage { get; init; }
    public double? DailyUsagePercentage { get; init; }
    public double? WeeklyUsagePercentage { get; init; }
    public DateTimeOffset? NextResetAt { get; init; }
    public TimeSpan? TimeUntilReset { get; init; }
    public long? RequestsUsed { get; init; }
    public long? RequestsLimit { get; init; }
    public long? TokensUsed { get; init; }
    public long? TokensLimit { get; init; }
    public decimal? EstimatedCost { get; init; }
    public bool IsConnected { get; init; }
    public string? StatusMessage { get; init; }
    public DateTimeOffset RetrievedAt { get; init; }

    public double? PrimaryUsagePercentage =>
        SessionUsagePercentage ?? DailyUsagePercentage ?? WeeklyUsagePercentage;
}
