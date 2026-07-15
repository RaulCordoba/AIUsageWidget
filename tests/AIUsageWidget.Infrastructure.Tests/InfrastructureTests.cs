using AIUsageWidget.Core.Models;
using AIUsageWidget.Core.Services;
using AIUsageWidget.Infrastructure;
using AIUsageWidget.Infrastructure.History;
using AIUsageWidget.Infrastructure.Json;
using AIUsageWidget.Infrastructure.Settings;
using Microsoft.Extensions.Logging.Abstractions;

namespace AIUsageWidget.Infrastructure.Tests;

public sealed class InfrastructureTests
{
    [Fact]
    public async Task AtomicJsonFileStore_WritesAndReadsJson()
    {
        var dir = CreateTempDir();
        var file = Path.Combine(dir, "settings.json");
        var store = new AtomicJsonFileStore(NullLogger<AtomicJsonFileStore>.Instance);

        await store.WriteAsync(file, new AppSettings { SelectedProviderId = "demo" });
        var result = await store.ReadAsync<AppSettings>(file);

        Assert.Equal("demo", result?.SelectedProviderId);
        Assert.False(File.Exists(file + ".tmp"));
    }

    [Fact]
    public async Task AtomicJsonFileStore_RecoversCorruptJson()
    {
        var dir = CreateTempDir();
        var file = Path.Combine(dir, "settings.json");
        await File.WriteAllTextAsync(file, "{ broken");
        var store = new AtomicJsonFileStore(NullLogger<AtomicJsonFileStore>.Instance);

        var result = await store.ReadAsync<AppSettings>(file);

        Assert.Null(result);
        Assert.Contains(Directory.EnumerateFiles(dir), x => x.Contains(".corrupt-", StringComparison.Ordinal));
    }

    [Fact]
    public async Task JsonSettingsStore_CreatesDefaultsWhenMissing()
    {
        var paths = new LocalAppPaths(CreateTempDir());
        var store = new JsonSettingsStore(
            paths,
            new AtomicJsonFileStore(NullLogger<AtomicJsonFileStore>.Instance),
            new SettingsMigrator());

        var settings = await store.LoadAsync();

        Assert.Equal("demo", settings.SelectedProviderId);
        Assert.True(File.Exists(paths.SettingsFile));
    }

    [Fact]
    public async Task JsonHistoryStore_DoesNotStoreRedundantSnapshots()
    {
        var paths = new LocalAppPaths(CreateTempDir());
        var store = new JsonHistoryStore(
            paths,
            new AtomicJsonFileStore(NullLogger<AtomicJsonFileStore>.Instance),
            new HistoryReductionService(),
            NullLogger<JsonHistoryStore>.Instance);
        var snapshot = Snapshot(17, DateTimeOffset.Parse("2026-07-14T10:00:00+02:00"));

        await store.AddAsync("demo", snapshot);
        await store.AddAsync("demo", Snapshot(17, snapshot.RetrievedAt.AddMinutes(1)));
        var entries = await store.ReadAsync("demo", new DateOnly(2026, 7, 1));

        Assert.Single(entries);
    }

    private static UsageSnapshot Snapshot(double usage, DateTimeOffset retrievedAt) => new()
    {
        ProviderId = "demo",
        ProviderName = "Demo",
        SessionUsagePercentage = usage,
        IsConnected = true,
        RetrievedAt = retrievedAt
    };

    private static string CreateTempDir()
    {
        var path = Path.Combine(Path.GetTempPath(), "AIUsageWidgetTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
