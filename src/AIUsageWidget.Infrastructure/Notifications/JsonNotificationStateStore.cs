using AIUsageWidget.Core.Interfaces;
using AIUsageWidget.Core.Models;
using AIUsageWidget.Infrastructure.Json;

namespace AIUsageWidget.Infrastructure.Notifications;

public sealed class JsonNotificationStateStore : INotificationStateStore
{
    private readonly LocalAppPaths _paths;
    private readonly AtomicJsonFileStore _store;

    public JsonNotificationStateStore(LocalAppPaths paths, AtomicJsonFileStore store)
    {
        _paths = paths;
        _store = store;
    }

    public async Task<NotificationState> LoadAsync(CancellationToken cancellationToken = default)
    {
        _paths.EnsureCreated();
        return await _store.ReadAsync<NotificationState>(_paths.NotificationsFile, cancellationToken).ConfigureAwait(false)
               ?? new NotificationState();
    }

    public Task SaveAsync(NotificationState state, CancellationToken cancellationToken = default)
    {
        _paths.EnsureCreated();
        return _store.WriteAsync(_paths.NotificationsFile, state, cancellationToken: cancellationToken);
    }
}
