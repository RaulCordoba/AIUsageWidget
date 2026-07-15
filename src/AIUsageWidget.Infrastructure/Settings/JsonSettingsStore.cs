using AIUsageWidget.Core.Interfaces;
using AIUsageWidget.Core.Models;
using AIUsageWidget.Core.Services;
using AIUsageWidget.Infrastructure.Json;

namespace AIUsageWidget.Infrastructure.Settings;

public sealed class JsonSettingsStore : ISettingsStore
{
    private readonly LocalAppPaths _paths;
    private readonly AtomicJsonFileStore _store;
    private readonly SettingsMigrator _migrator;

    public JsonSettingsStore(LocalAppPaths paths, AtomicJsonFileStore store, SettingsMigrator migrator)
    {
        _paths = paths;
        _store = store;
        _migrator = migrator;
    }

    public async Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        _paths.EnsureCreated();
        var settings = await _store.ReadAsync<AppSettings>(_paths.SettingsFile, cancellationToken).ConfigureAwait(false);
        var migrated = _migrator.Migrate(settings);
        await SaveAsync(migrated, cancellationToken).ConfigureAwait(false);
        return migrated;
    }

    public Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        _paths.EnsureCreated();
        return _store.WriteAsync(_paths.SettingsFile, settings.Validate(), cancellationToken: cancellationToken);
    }
}
