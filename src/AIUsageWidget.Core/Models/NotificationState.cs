namespace AIUsageWidget.Core.Models;

public sealed class NotificationState
{
    public int SchemaVersion { get; init; } = 1;
    public string? CycleKey { get; init; }
    public IReadOnlyList<int> ShownThresholds { get; init; } = [];
}
