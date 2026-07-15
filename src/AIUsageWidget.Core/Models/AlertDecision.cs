namespace AIUsageWidget.Core.Models;

public sealed record AlertDecision(bool ShouldNotify, int? Threshold, string? CycleKey);
