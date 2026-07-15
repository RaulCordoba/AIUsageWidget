using AIUsageWidget.Core.Models;

namespace AIUsageWidget.Core.Interfaces;

public interface IUsageProvider
{
    string Id { get; }
    string DisplayName { get; }

    Task<ProviderAvailabilityResult> CheckAvailabilityAsync(CancellationToken cancellationToken = default);

    Task<UsageSnapshot> GetUsageAsync(CancellationToken cancellationToken = default);
}
