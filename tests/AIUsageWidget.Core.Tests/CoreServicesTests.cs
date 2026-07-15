using AIUsageWidget.Core.Models;
using AIUsageWidget.Core.Services;

namespace AIUsageWidget.Core.Tests;

public sealed class CoreServicesTests
{
    [Fact]
    public void ResetCalculator_ReturnsZero_WhenResetIsPast()
    {
        var now = DateTimeOffset.Parse("2026-07-14T10:00:00+02:00");

        var result = ResetCalculator.Calculate(now.AddMinutes(-1), now);

        Assert.Equal(TimeSpan.Zero, result);
    }

    [Fact]
    public void SettingsMigrator_ClampsUnsafeValues()
    {
        var migrator = new SettingsMigrator();
        var settings = new AppSettings
        {
            RefreshIntervalSeconds = 1,
            Opacity = 2,
            Scale = 9,
            HistoryRetentionDays = -2,
            AlertThresholds = [0, 90, 90, 101]
        };

        var result = migrator.Migrate(settings);

        Assert.Equal(10, result.RefreshIntervalSeconds);
        Assert.Equal(1, result.Opacity);
        Assert.Equal(2, result.Scale);
        Assert.Equal(1, result.HistoryRetentionDays);
        Assert.Equal([90], result.AlertThresholds);
    }

    [Theory]
    [InlineData(17, "#36D399")]
    [InlineData(39, "#36D399")]
    [InlineData(40, "#3B82F6")]
    [InlineData(79, "#3B82F6")]
    [InlineData(80, "#FB923C")]
    [InlineData(99, "#FB923C")]
    [InlineData(100, "#EF4444")]
    public void UsageColorService_SelectsExpectedColor(double usage, string expected)
    {
        var result = new UsageColorService().GetColor(usage, new UsageColorThresholds());

        Assert.Equal(expected, result);
    }

    [Fact]
    public void AlertService_PreventsDuplicateAlertsInSameCycle()
    {
        var service = new AlertService();
        var snapshot = Snapshot(91);
        var settings = new AppSettings { AlertThresholds = [80, 90] };

        var first = service.Evaluate(snapshot, settings, new NotificationState());
        var state = service.MarkShown(new NotificationState(), first);
        var second = service.Evaluate(snapshot, settings, state);

        Assert.True(first.ShouldNotify);
        Assert.Equal(90, first.Threshold);
        Assert.False(second.ShouldNotify);
    }

    [Fact]
    public void HistoryReduction_SkipsRedundantEntriesBeforeFiveMinutes()
    {
        var service = new HistoryReductionService();
        var previous = new UsageHistoryEntry
        {
            Timestamp = DateTimeOffset.Parse("2026-07-14T10:00:00+02:00"),
            UsagePercentage = 25
        };

        var result = service.ShouldStore(previous, Snapshot(25, previous.Timestamp.AddMinutes(2)), TimeSpan.FromMinutes(5));

        Assert.False(result);
    }

    [Fact]
    public void HistoryReduction_StoresChangedPercentage()
    {
        var service = new HistoryReductionService();
        var previous = new UsageHistoryEntry
        {
            Timestamp = DateTimeOffset.Parse("2026-07-14T10:00:00+02:00"),
            UsagePercentage = 25
        };

        var result = service.ShouldStore(previous, Snapshot(26, previous.Timestamp.AddMinutes(2)), TimeSpan.FromMinutes(5));

        Assert.True(result);
    }

    private static UsageSnapshot Snapshot(double usage, DateTimeOffset? retrievedAt = null) => new()
    {
        ProviderId = "demo",
        ProviderName = "Demo",
        SessionUsagePercentage = usage,
        NextResetAt = DateTimeOffset.Parse("2026-07-14T15:00:00+02:00"),
        IsConnected = true,
        RetrievedAt = retrievedAt ?? DateTimeOffset.Parse("2026-07-14T10:00:00+02:00")
    };
}
