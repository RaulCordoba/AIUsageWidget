using AIUsageWidget.Core.Models;

namespace AIUsageWidget.Core.Interfaces;

public interface INotificationStateStore
{
    Task<NotificationState> LoadAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(NotificationState state, CancellationToken cancellationToken = default);
}
